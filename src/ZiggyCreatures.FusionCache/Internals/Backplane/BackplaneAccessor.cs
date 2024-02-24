using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal sealed partial class BackplaneAccessor
{
	private readonly FusionCache _cache;
	private readonly IFusionCacheBackplane _backplane;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheBackplaneEventsHub _events;
	private readonly SimpleCircuitBreaker _breaker;

	public BackplaneAccessor(FusionCache cache, IFusionCacheBackplane backplane, FusionCacheOptions options, ILogger? logger)
	{
		if (cache is null)
			throw new ArgumentNullException(nameof(cache));

		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		_cache = cache;
		_backplane = backplane;

		_options = options;

		_logger = logger;
		_events = _cache.Events.Backplane;

		// CIRCUIT-BREAKER
		_breaker = new SimpleCircuitBreaker(options.BackplaneCircuitBreakerDuration);
	}

	private void UpdateLastError(string operationId, string key)
	{
		// NO DISTRIBUTEC CACHE
		if (_backplane is null)
			return;

		var res = _breaker.TryOpen(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] backplane temporarily de-activated for {BreakDuration}", _cache.CacheName, _cache.InstanceId, operationId, key, _breaker.BreakDuration);

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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] backplane activated again", _cache.CacheName, _cache.InstanceId, operationId, key);

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
				_logger.Log(_options.BackplaneSyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a synthetic timeout occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);

			return;
		}

		UpdateLastError(operationId, key);

		if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] an error occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);
	}

	private bool CheckMessage(string operationId, BackplaneMessage message, bool isAutoRecovery)
	{
		// CHECK: IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] cannot send a null backplane message (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return false;
		}

		// CHECK: IS VALID
		if (message.IsValid() == false)
		{
			// IGNORE INVALID MESSAGES
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] cannot send an invalid backplane message" + isAutoRecovery.ToString(" (auto-recovery)"), _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		// CHECK: WRONG SOURCE ID
		if (message.SourceId != _cache.InstanceId)
		{
			// IGNORE MESSAGES -NOT- FROM THIS SOURCE
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] cannot send a backplane message" + isAutoRecovery.ToString(" (auto-recovery)") + " with a SourceId different than the local one (IFusionCache.InstanceId)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		return true;
	}

	public void Subscribe()
	{
		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] before subscribing to backplane", _cache.CacheName, _cache.InstanceId);

			_backplane.Subscribe(
				new BackplaneSubscriptionOptions(
					_cache.CacheName,
					_cache.InstanceId,
					_options.GetBackplaneChannelName(),
					HandleConnect,
					HandleIncomingMessage
				)
			);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] after subscribing to backplane", _cache.CacheName, _cache.InstanceId);
		}
		catch (Exception exc)
		{
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

			ProcessError(operationId, "", exc, $"subscribing to a backplane of type {_backplane.GetType().FullName}");

			//if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			//	_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while subscribing to a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	public void Unsubscribe()
	{
		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] before unsubscribing to backplane", _cache.CacheName, _cache.InstanceId);

			_backplane.Unsubscribe();

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] after unsubscribing to backplane", _cache.CacheName, _cache.InstanceId);
		}
		catch (Exception exc)
		{
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

			ProcessError(operationId, "", exc, $"unsubscribing from a backplane of type {_backplane.GetType().FullName}");

			//if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			//	_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while unsubscribing from a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	private void HandleConnect(BackplaneConnectionInfo info)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] backplane " + (info.IsReconnection ? "re-connected" : "connected"), _cache.CacheName, _cache.InstanceId, operationId);

		if (info.IsReconnection)
		{
			_cache.AutoRecovery.TryUpdateBarrier(operationId);
		}
	}

	private void HandleIncomingMessage(BackplaneMessage message)
	{
		if (_options.IgnoreIncomingBackplaneNotifications)
			return;

		_ = Task.Run(async () =>
		{
			await HandleIncomingMessageAsync(message).ConfigureAwait(false);
		});
	}

	private async ValueTask HandleIncomingMessageAsync(BackplaneMessage message)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		// IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return;
		}

		// IGNORE MESSAGES FROM THIS SOURCE
		if (message.SourceId == _cache.InstanceId)
		{
			return;
		}

		// CHECK CIRCUIT BREAKER
		_breaker.Close(out var hasChanged);
		if (hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] backplane activated again", _cache.CacheName, _cache.InstanceId, operationId);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, message.CacheKey, true);
		}

		// ACTIVITY
		using var activity = Activities.SourceBackplane.StartActivityWithCommonTags(Activities.Names.BackplaneReceive, _options.CacheName, _options.InstanceId!, message.CacheKey!, operationId);
		activity?.SetTag("fusioncache.backplane.message_action", message.Action.ToString());

		// EVENT
		_events.OnMessageReceived(operationId, message);

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstantTimestamp})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action, message.Timestamp);

			return;
		}

		// AUTO-RECOVERY
		if (_options.EnableAutoRecovery)
		{
			if (_cache.AutoRecovery.CheckIncomingMessageForConflicts(operationId, message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been ignored since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				return;
			}
		}

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// HANDLE SET
				await HandleIncomingMessageSetAsync(operationId, message).ConfigureAwait(false);
				break;
			case BackplaneMessageAction.EntryRemove:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// HANDLE REMOVE: CALLING MaybeExpireMemoryEntryInternal() WITH allowFailSafe SET TO FALSE -> LOCAL REMOVE
				_cache.MaybeExpireMemoryEntryInternal(operationId, message.CacheKey!, false, null);
				break;
			case BackplaneMessageAction.EntryExpire:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// HANDLE EXPIRE: CALLING MaybeExpireMemoryEntryInternal() WITH allowFailSafe SET TO TRUE -> LOCAL EXPIRE
				_cache.MaybeExpireMemoryEntryInternal(operationId, message.CacheKey!, true, message.Timestamp);
				break;
			default:
				// HANDLE UNKNOWN: DO NOTHING
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action);
				break;
		}
	}

	private async ValueTask HandleIncomingMessageSetAsync(string operationId, BackplaneMessage message)
	{
		var cacheKey = message.CacheKey!;

		var mca = _cache.GetCurrentMemoryAccessor();

		if (mca is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] no memory cache, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		var memoryEntry = mca.GetEntryOrNull(operationId, cacheKey);

		// IF NO MEMORY ENTRY -> DO NOTHING
		if (memoryEntry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] no memory entry, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		//// IF NO VALUE -> EXPIRE LOCALLY
		//if (memoryEntry.Value is null)
		//{
		//	_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);
		//	return;
		//}

		// IF MEMORY ENTRY SAME AS REMOTE ENTRY (VIA MESSAGE TIMESTAMP) -> DO NOTHING
		if (memoryEntry.Timestamp == message.Timestamp)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] memory entry same as the incoming backplane message, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		// IF MEMORY ENTRY MORE FRESH THAN REMOTE ENTRY (VIA MESSAGE TIMESTAMP) -> DO NOTHING
		if (memoryEntry.Timestamp > message.Timestamp)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] memory entry more fresh than the incoming backplane message, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		if (_cache.HasDistributedCache)
		{
			var dca = _cache.GetCurrentDistributedAccessor(null);
			if (dca.CanBeUsed(operationId, cacheKey) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] distributed cache not currently usable, expiring local memory entry", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

				_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);

				return;
			}

			var (error, isSame, hasUpdated) = await memoryEntry.TryUpdateMemoryEntryFromDistributedEntryAsync(operationId, cacheKey, _cache).ConfigureAwait(false);

			if (error == false)
			{
				if (isSame)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] memory entry is the same as the distributed entry, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
					return;
				}

				if (hasUpdated)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] memory entry updated from the distributed entry, ignoring incoming backplane message", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
					return;
				}
			}
		}

		_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);
	}
}
