using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal sealed partial class BackplaneAccessor
{
	private readonly FusionCache _cache;
	private readonly IFusionCacheBackplane _backplane;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheBackplaneEventsHub _events;
	private readonly SimpleCircuitBreaker _breaker;
	private readonly SemaphoreSlim _autoRecoveryLock = new SemaphoreSlim(1, 1);
	private ConcurrentDictionary<string, (BackplaneMessage Message, FusionCacheEntryOptions Options)> _autoRecoveryQueue = new ConcurrentDictionary<string, (BackplaneMessage Message, FusionCacheEntryOptions Options)>();

	public BackplaneAccessor(FusionCache cache, IFusionCacheBackplane backplane, FusionCacheOptions options, ILogger? logger, FusionCacheBackplaneEventsHub events)
	{
		if (cache is null)
			throw new ArgumentNullException(nameof(cache));

		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		_cache = cache;
		_backplane = backplane;

		_options = options;

		_logger = logger;
		_events = events;

		// CIRCUIT-BREAKER
		_breaker = new SimpleCircuitBreaker(options.BackplaneCircuitBreakerDuration);
	}

	private void UpdateLastError(string key, string operationId)
	{
		// NO DISTRIBUTEC CACHE
		if (_backplane is null)
			return;

		var res = _breaker.TryOpen(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.LogWarning("FUSION (O={CacheOperationId} K={CacheKey}): backplane temporarily de-activated for {BreakDuration}", operationId, key, _breaker.BreakDuration);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, false);
		}
	}

	public bool IsCurrentlyUsable(string? operationId, string? key)
	{
		var res = _breaker.IsClosed(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.LogWarning("FUSION (O={CacheOperationId} K={CacheKey}): backplane activated again", operationId, key);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, true);
		}

		return res;
	}

	private void ProcessError(string operationId, string key, Exception exc, string actionDescription)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.BackplaneSyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.BackplaneSyntheticTimeoutsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, operationId, key);

			return;
		}

		UpdateLastError(key, operationId);

		if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, operationId, key);
	}

	private bool TryAddAutoRecoveryItem(BackplaneMessage message, FusionCacheEntryOptions options)
	{
		if (message.CacheKey is null)
			return false;

		if (_options.BackplaneAutoRecoveryMaxItems.HasValue && _autoRecoveryQueue.Count >= _options.BackplaneAutoRecoveryMaxItems.Value && _autoRecoveryQueue.ContainsKey(message.CacheKey) == false)
		{
			// IF:
			// - A LIMIT HAS BEEN SET
			// - THE LIMIT HAS BEEN REACHED OR SURPASSED
			// - THE ITEM TO BE ADDED IS NOT ALREADY THERE (OTHERWISE IT WILL BE AN OVERWRITE AND SIZE WILL NOT GROW)
			// THEN FIND THE ITEM THAT WILL EXPIRE SOONER AND REMOVE IT OR, IF NEW ITEM WILL EXPIRE SOONER, DO NOT ADD IT
			try
			{
				var soonerToExpire = _autoRecoveryQueue.Values.OrderBy(x => x.Message.InstantTicks + x.Options.Duration.Ticks).FirstOrDefault();
				if (soonerToExpire.Message is not null)
				{
					if ((soonerToExpire.Message.InstantTicks + soonerToExpire.Options.Duration.Ticks) < (message.InstantTicks + options.Duration.Ticks))
					{
						// REMOVE THE QUEUED ITEM
						_autoRecoveryQueue.TryRemove(soonerToExpire.Message.CacheKey!, out _);
					}
					else
					{
						// IGNORE THE NEW ITEM
						return false;
					}
				}

			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION: an error occurred while deciding which item in the backplane auto-recovery queue to remove to make space for a new one");
			}
		}

		_autoRecoveryQueue[message.CacheKey] = (message, options);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey}): added (or overwrote) an item to the backplane auto-recovery queue", message.CacheKey);

		return true;
	}

	private void ProcessAutoRecoveryQueue()
	{
		var _count = _autoRecoveryQueue.Count;
		if (_count == 0)
			return;

		// ACQUIRE THE LOCK
		if (_autoRecoveryLock.Wait(0) == false)
		{
			// IF THE LOCK HAS NOT BEEN ACQUIRED IMMEDIATELY, SOMEONE ELSE IS ALREADY PROCESSING THE QUEUE, SO WE JUST RETURN
			return;
		}

		_ = Task.Run(async () =>
		{
			try
			{
				// NOTE: THE COUNT USAGE HERE IN THE LOG IS JUST AN INDICATION: PER THE MULTI-THREADED NATURE OF THIS THING
				// IT'S OK IF THE NUMBER IS SINCE CHANGED AND IN THE FOREACH LOOP WE WILL ITERATE OVER MORE (OR LESS) ITEMS
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION: starting backplane auto-recovery of about {Count} pending notifications", _count);

				_count = 0;
				foreach (var item in _autoRecoveryQueue)
				{
					var _operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
					if (await PublishAsync(_operationId, item.Value.Message, item.Value.Options, true).ConfigureAwait(false))
					{
						// IF A PUBLISH GO THROUGH -> REMOVE FROM THE QUEUE
						_autoRecoveryQueue.TryRemove(item.Key, out _);

						_count++;
					}
					else
					{
						// IF A PUBLISH DOESN'T GO THROUGH -> STOP PROCESSING THE QUEUE
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION (O={CacheOperationId} K={CacheKey}): stopped backplane auto-recovery because of an error after {Count} processed items", _operationId, item.Value.Message.CacheKey, _count);

						return;
					}
				}

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION: completed backplane auto-recovery of {Count} items", _count);
			}
			finally
			{
				// RELEASE THE LOCK
				_autoRecoveryLock.Release();
			}
		});
	}

	private bool CheckIncomingMessageForAutoRecoveryConflicts(BackplaneMessage message)
	{
		if (message.CacheKey is null)
		{
			return true;
		}

		if (_autoRecoveryQueue.TryGetValue(message.CacheKey, out var pendingLocal) == false)
		{
			// NO PENDING LOCAL MESSAGE WITH THE SAME KEY
			return true;
		}

		if (pendingLocal.Message.InstantTicks <= message.InstantTicks)
		{
			// PENDING LOCAL MESSAGE IS -OLDER- THAN THE INCOMING ONE -> REMOVE THE LOCAL ONE
			_autoRecoveryQueue.TryRemove(message.CacheKey, out _);
			return true;
		}

		// PENDING LOCAL MESSAGE IS -NEWER- THAN THE INCOMING ONE -> DO NOT PROCESS THE INCOMING ONE
		return false;
	}

	public void Subscribe()
	{
		_backplane.Subscribe(
			new BackplaneSubscriptionOptions
			{
				ChannelName = _options.GetBackplaneChannelName(),
				Handler = ProcessMessage
			}
		);
	}

	public void Unsubscribe()
	{
		_backplane.Unsubscribe();
	}

	private void ProcessMessage(BackplaneMessage message)
	{
		// IGNORE INVALID MESSAGES
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [{CacheName} - {CacheInstanceId}]: a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId);

			return;
		}

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstanceTicks})", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId, message.Action, message.InstantTicks);

			return;
		}

		// IGNORE MESSAGES FROM THIS SOURCE
		if (message.SourceId == _cache.InstanceId)
			return;

		// AUTO-RECOVERY
		if (_options.EnableBackplaneAutoRecovery)
		{
			if (CheckIncomingMessageForAutoRecoveryConflicts(message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been discarded since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId);

				ProcessAutoRecoveryQueue();
				return;
			}

			ProcessAutoRecoveryQueue();
		}

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				_cache.ExpireMemoryEntryInternal(message.CacheKey!, true);

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId);
				break;
			case BackplaneMessageAction.EntryRemove:
				_cache.ExpireMemoryEntryInternal(message.CacheKey!, false);

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId);
				break;
			case BackplaneMessageAction.EntryExpire:
				_cache.ExpireMemoryEntryInternal(message.CacheKey!, true);

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId);
				break;
			default:
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [{CacheName} - {CacheInstanceId}] (K={CacheKey}): an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, message.CacheKey, message.SourceId, message.Action);
				break;
		}

		// EVENT
		_events.OnMessageReceived("", message);
	}
}
