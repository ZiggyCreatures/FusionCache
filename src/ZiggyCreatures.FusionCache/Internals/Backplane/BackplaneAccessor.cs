using System;
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

	private bool CheckMessage(string operationId, BackplaneMessage message, bool isAutoRecovery)
	{
		// CHECK: IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return false;
		}

		// CHECK: IS VALID
		if (message.IsValid() == false)
		{
			// IGNORE INVALID MESSAGES
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send an invalid backplane message" + isAutoRecovery.ToString(" (auto-recovery)"), _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		//// CHECK: EMPTY SOURCE ID
		//if (string.IsNullOrEmpty(message.SourceId))
		//{
		//	//// AUTO-ASSIGN LOCAL SOURCE ID
		//	//message.SourceId = _cache.InstanceId;

		//	if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
		//		_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send a backplane message" + isAutoRecovery.ToString(" (auto-recovery)") + " with a null/empty SourceId", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

		//	return false;
		//}

		// CHECK: WRONG SOURCE ID
		if (message.SourceId != _cache.InstanceId)
		{
			// IGNORE MESSAGES -NOT- FROM THIS SOURCE
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send a backplane message" + isAutoRecovery.ToString(" (auto-recovery)") + " with a SourceId different than the local one (IFusionCache.InstanceId)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		return true;
	}

	public void Subscribe()
	{
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
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

			ProcessError(operationId, "", exc, $"subscribing to a backplane of type {_backplane.GetType().FullName}");

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while subscribing to a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	public void Unsubscribe()
	{
		try
		{
			_backplane.Unsubscribe();
		}
		catch (Exception exc)
		{
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

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
			_cache.TryUpdateAutoRecoveryBarrier(operationId);
		}
	}

	private void HandleIncomingMessage(BackplaneMessage message)
	{
		_ = Task.Run(async () =>
		{
			await HandleIncomingMessageAsync(message).ConfigureAwait(false);
		});
	}

	private async ValueTask HandleIncomingMessageAsync(BackplaneMessage message)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		// CHECK CIRCUIT BREAKER
		_breaker.Close(out var hasChanged);
		if (hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane activated again", _cache.CacheName, _cache.InstanceId, operationId);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, null, true);
		}

		// IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return;
		}

		// IGNORE MESSAGES FROM THIS SOURCE
		if (message.SourceId == _cache.InstanceId)
		{
			return;
		}

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstantTimestamp})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action, message.Timestamp);

			return;
		}

		// AUTO-RECOVERY
		if (_options.EnableBackplaneAutoRecovery)
		{
			if (_cache.CheckIncomingMessageForAutoRecoveryConflicts(operationId, message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been discarded since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				return;
			}
		}

		// EVENT
		_events.OnMessageReceived(operationId, message);

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				await HandleIncomingMessageSetAsync(operationId, message).ConfigureAwait(false);
				break;
			case BackplaneMessageAction.EntryRemove:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.MaybeExpireMemoryEntryInternal(operationId, message.CacheKey!, false, null);
				break;
			case BackplaneMessageAction.EntryExpire:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.MaybeExpireMemoryEntryInternal(operationId, message.CacheKey!, true, message.Timestamp);
				break;
			case BackplaneMessageAction.EntrySentinel:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SENTINEL)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// DO NOTHING
				break;
			default:
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action);
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
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 0", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		var memoryEntry = mca.GetEntryOrNull(operationId, cacheKey);

		// IF NO MEMORY ENTRY -> DO NOTHING
		if (memoryEntry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 1", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		//// IF NO VALUE -> EXPIRE LOCALLY
		//if (memoryEntry.Value is null)
		//{
		//	_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);
		//	return;
		//}

		// IF MEMORY ENTRY FRESHER THAN REMOTE ENTRY (VIA MESSAGE TIMESTAMP) -> DO NOTHING
		if (memoryEntry.Timestamp >= message.Timestamp)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 2", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		if (_cache.HasDistributedCache == false)
		{
			_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 3", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		var dca = _cache.GetCurrentDistributedAccessor(null);
		if (dca.CanBeUsed(operationId, cacheKey) == false)
		{
			_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, message.Timestamp);
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 4", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		//if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): during backplane auto-recovery of an item, the distributed cache was necessary (because of the EnableDistributedExpireOnBackplaneAutoRecovery option) but was not available", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 5", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		(var isSame, var hasUpdated) = await _cache.TryUpdateMemoryEntryFromDistributedEntryAsync(operationId, cacheKey, memoryEntry).ConfigureAwait(false);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 6", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		if (isSame)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 7", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		if (hasUpdated)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 8", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			return;
		}

		_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, null);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): AAA 9", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		//// TODO: CACHE THE METHOD INFO
		//var methodInfo = typeof(BackplaneAccessor).GetMethod(nameof(Foo), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).MakeGenericMethod(memoryEntry.ValueType);
		//var task = (Task)methodInfo.Invoke(this, new object[] { operationId, cacheKey, dca!, options, memoryEntry });

		//await task.ConfigureAwait(false);
	}

	public async ValueTask<bool> PublishSentinelAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
	{
		var message = BackplaneMessage.CreateForEntrySentinel(_cache.InstanceId, key);

		return await PublishAsync(operationId, FusionCacheAction.Unknown, message, options, true, true, token).ConfigureAwait(false);
	}
}
