﻿using System;
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

	// AUTO-RECOVERY
	private readonly SemaphoreSlim _autoRecoveryProcessingLock = new SemaphoreSlim(1, 1);
	private readonly ConcurrentDictionary<string, BackplaneAutoRecoveryItem> _autoRecoveryQueue = new ConcurrentDictionary<string, BackplaneAutoRecoveryItem>();
	private readonly FusionCacheEntryOptions _autoRecoveryEntryOptions;

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

		// AUTO-RECOVERY
		_autoRecoveryEntryOptions = new FusionCacheEntryOptions().SetSkipMemoryCache().SetSkipBackplaneNotifications(true);

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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): backplane temporarily de-activated for {BreakDuration}", _cache.CacheName, _cache.InstanceId, operationId, key, _breaker.BreakDuration);

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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): backplane activated again", _cache.CacheName, _cache.InstanceId, operationId, key);

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
				_logger.Log(_options.BackplaneSyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);

			return;
		}

		UpdateLastError(key, operationId);

		if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);
	}

	private bool TryAddAutoRecoveryItem(string? operationId, BackplaneMessage message, FusionCacheEntryOptions options)
	{
		if (message.CacheKey is null)
			return false;

		var expirationTicks = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(options.DistributedCacheDuration.GetValueOrDefault(options.Duration), options, false).Ticks;

		if (_options.BackplaneAutoRecoveryMaxItems.HasValue && _autoRecoveryQueue.Count >= _options.BackplaneAutoRecoveryMaxItems.Value && _autoRecoveryQueue.ContainsKey(message.CacheKey) == false)
		{
			// IF:
			// - A LIMIT HAS BEEN SET
			// - THE LIMIT HAS BEEN REACHED OR SURPASSED
			// - THE ITEM TO BE ADDED IS NOT ALREADY THERE (OTHERWISE IT WILL BE AN OVERWRITE AND SIZE WILL NOT GROW)
			// THEN:
			// - FIND THE ITEM THAT WILL EXPIRE SOONER AND REMOVE IT
			// - OR, IF NEW ITEM WILL EXPIRE SOONER, DO NOT ADD IT
			try
			{
				var earlierToExpire = _autoRecoveryQueue.Values.OrderBy(x => x.ExpirationTicks).FirstOrDefault();
				if (earlierToExpire.Message is not null)
				{
					if (earlierToExpire.ExpirationTicks < expirationTicks)
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an item with cache key {CacheKeyToRemove} has been removed from the backplane auto-recovery queue to make space for the new one", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey, earlierToExpire.Message.CacheKey);

						// REMOVE THE QUEUED ITEM
						_autoRecoveryQueue.TryRemove(earlierToExpire.Message.CacheKey!, out _);
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
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while deciding which item in the backplane auto-recovery queue to remove to make space for a new one", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey);
			}
		}

		if (message is null)
			return false;

		_autoRecoveryQueue[message.CacheKey] = new BackplaneAutoRecoveryItem(message, options, expirationTicks);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): added (or overwrote) an item to the backplane auto-recovery queue", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

		return true;
	}

	private bool TryRemoveAutoRecoveryItemByCacheKey(string? operationId, string? cacheKey)
	{
		if (cacheKey is null)
			return false;

		if (_autoRecoveryQueue.ContainsKey(cacheKey) == false)
			return false;

		if (_autoRecoveryQueue.TryRemove(cacheKey, out _))
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the backplane auto-recovery queue because a new one is about to be sent", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

			return true;
		}

		return false;
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

	private bool TryProcessAutoRecoveryQueue(string operationId)
	{
		if (IsCurrentlyUsable(null, null) == false)
			return false;

		if (_options.EnableBackplaneAutoRecovery == false)
			return false;

		var _count = _autoRecoveryQueue.Count;
		if (_count == 0)
			return false;

		// ACQUIRE THE LOCK
		if (_autoRecoveryProcessingLock.Wait(0) == false)
		{
			// IF THE LOCK HAS NOT BEEN ACQUIRED IMMEDIATELY, SOMEONE ELSE IS ALREADY PROCESSING THE QUEUE, SO WE JUST RETURN
			return false;
		}

		FusionCacheExecutionUtils.RunSyncActionAdvanced(
			(ct) =>
			{
				try
				{
					// NOTE: THE COUNT VALUE HERE IS JUST AN APPROXIMATION: PER THE MULTI-THREADED NATURE OF THIS THING IT'S
					// OK IF THE NUMBER IS SINCE CHANGED AND IN THE FOREACH LOOP WE WILL ITERATE OVER MORE (OR LESS) ITEMS
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): starting backplane auto-recovery of about {Count} pending notifications", _cache.CacheName, _cache.InstanceId, operationId, _count);

					_count = 0;
					foreach (var item in _autoRecoveryQueue)
					{
						if (Publish(operationId, item.Value.Message, item.Value.Options, true))
						{
							// IF A PUBLISH GO THROUGH -> REMOVE FROM THE QUEUE
							_autoRecoveryQueue.TryRemove(item.Key, out _);

							_count++;
						}
						else
						{
							// IF A PUBLISH DOESN'T GO THROUGH -> STOP PROCESSING THE QUEUE
							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): stopped backplane auto-recovery because of an error after {Count} processed items", _cache.CacheName, _cache.InstanceId, operationId, item.Value.Message.CacheKey, _count);

							return;
						}
					}

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): completed backplane auto-recovery of {Count} items", _cache.CacheName, _cache.InstanceId, operationId, _count);
				}
				finally
				{
					// RELEASE THE LOCK
					_autoRecoveryProcessingLock.Release();
				}
			},
			Timeout.InfiniteTimeSpan,
			false,
			_options.DefaultEntryOptions.AllowBackgroundBackplaneOperations == false
		);

		return true;
	}

	public void Subscribe()
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		try
		{
			_backplane.Subscribe(
				new BackplaneSubscriptionOptions(
					_options.GetBackplaneChannelName(),
					HandleConnect,
					HandleIncomingMessage
				)
			);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, "", exc, $"subscribing to a backplane of type {_backplane.GetType().FullName}");

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while subscribing to a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	public void Unsubscribe()
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		_autoRecoveryQueue.Clear();

		try
		{
			_backplane.Unsubscribe();
		}
		catch (Exception exc)
		{
			ProcessError(operationId, "", exc, $"unsubscribing from a backplane of type {_backplane.GetType().FullName}");

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while unsubscribing from a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	private void HandleConnect(BackplaneConnectionInfo info)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane " + (info.IsReconnection ? "re-connected" : "connected"), _cache.CacheName, _cache.InstanceId, operationId);

		if (info.IsReconnection && _options.EnableBackplaneAutoRecovery)
		{
			Task.Run(async () =>
			{
				if (_options.BackplaneAutoRecoveryReconnectDelay > TimeSpan.Zero)
				{
					if (_logger?.IsEnabled(LogLevel.Information) ?? false)
						_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting {AutoRecoveryDelay} to let the other nodes reconnect, to better handle backpressure", _cache.CacheName, _cache.InstanceId, operationId, _options.BackplaneAutoRecoveryReconnectDelay);

					await Task.Delay(_options.BackplaneAutoRecoveryReconnectDelay).ConfigureAwait(false);
				}

				_breaker.Close(out var hasChanged);

				return TryProcessAutoRecoveryQueue(operationId);
			});
		}
	}

	private void HandleIncomingMessage(BackplaneMessage message)
	{
		_breaker.Close(out var hasChanged);

		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		if (hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane activated again", _cache.CacheName, _cache.InstanceId, operationId);

			// EVENT
			_events.OnCircuitBreakerChange(null, null, true);
		}

		// IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return;
		}

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstanceTicks})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action, message.InstantTicks);

			TryProcessAutoRecoveryQueue(operationId);
			return;
		}

		// IGNORE MESSAGES FROM THIS SOURCE
		if (message.SourceId == _cache.InstanceId)
		{
			//TryProcessAutoRecoveryQueue(operationId);
			return;
		}

		// AUTO-RECOVERY
		if (_options.EnableBackplaneAutoRecovery)
		{
			if (CheckIncomingMessageForAutoRecoveryConflicts(message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been discarded since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				TryProcessAutoRecoveryQueue(operationId);
				return;
			}

			TryProcessAutoRecoveryQueue(operationId);
		}

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, true);
				break;
			case BackplaneMessageAction.EntryRemove:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, false);
				break;
			case BackplaneMessageAction.EntryExpire:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, true);
				break;
			default:
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action);
				break;
		}

		// EVENT
		_events.OnMessageReceived(operationId, message);
	}
}
