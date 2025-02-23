using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	public async ValueTask SubscribeAsync()
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		var channelName = _options.GetBackplaneChannelName();

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] before subscribing to backplane on channel {BackplaneChannel}", _cache.CacheName, _cache.InstanceId, operationId, channelName);

			var retriesLeft = 3;
			while (true)
			{
				retriesLeft--;

				try
				{
					await _backplane.SubscribeAsync(
						new BackplaneSubscriptionOptions(
							_cache.CacheName,
							_cache.InstanceId,
							channelName,
							HandleConnect,
							HandleIncomingMessage,
							HandleConnectAsync,
							HandleIncomingMessageAsync
						)
					).ConfigureAwait(false);

					break;
				}
				catch (Exception exc)
				{
					if (_logger?.IsEnabled(LogLevel.Error) ?? false)
						_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] an error occurred while subscribing to a backplane of type {BackplaneType} on channel {BackplaneChannel} ({RetriesLeft} retries left)", _cache.CacheName, _cache.InstanceId, operationId, _backplane.GetType().FullName, channelName, retriesLeft);

					if (retriesLeft <= 0)
						throw;

					await Task.Delay(250).ConfigureAwait(false);
				}
			}

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] after subscribing to backplane on channel {BackplaneChannel}", _cache.CacheName, _cache.InstanceId, operationId, channelName);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, "", exc, $"subscribing to a backplane of type {_backplane.GetType().FullName}");
		}
	}

	public async ValueTask UnsubscribeAsync()
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] before unsubscribing to backplane", _cache.CacheName, _cache.InstanceId, operationId);

			await _backplane.UnsubscribeAsync().ConfigureAwait(false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] after unsubscribing to backplane", _cache.CacheName, _cache.InstanceId, operationId);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, "", exc, $"unsubscribing from a backplane of type {_backplane.GetType().FullName}");
		}
	}

	private async ValueTask<bool> PublishAsync(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (CheckMessage(operationId, message, isAutoRecovery) == false)
			return false;

		var cacheKey = message.CacheKey!;

		// CHECK: CURRENTLY NOT USABLE
		if (IsCurrentlyUsable(operationId, cacheKey) == false)
		{
			return false;
		}

		token.ThrowIfCancellationRequested();

		// ACTIVITY
		using var activity = Activities.SourceBackplane.StartActivityWithCommonTags(Activities.Names.BackplanePublish, _options.CacheName, _options.InstanceId!, message.CacheKey!, operationId);
		activity?.SetTag(Tags.Names.BackplaneMessageAction, message.Action.ToString());

		if (isAutoRecovery == false)
		{
			_cache.AutoRecovery.TryRemoveItemByCacheKey(operationId, cacheKey);
		}

		var actionDescription = "sending a backplane notification" + isAutoRecovery.ToString(" (auto-recovery)") + isBackground.ToString(" (background)");

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);

			await _backplane.PublishAsync(message, options, token).ConfigureAwait(false);

			// EVENT
			_events.OnMessagePublished(operationId, message);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, cacheKey, exc, actionDescription);

			// ACTIVITY
			Activity.Current?.SetStatus(ActivityStatusCode.Error, exc.Message);
			Activity.Current?.AddExceptionInternal(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowBackplaneExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheBackplaneException("An error occurred while working with the backplane", exc);
				}
			}

			return false;
		}

		return true;
	}

	public ValueTask<bool> PublishSetAsync(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] publishing set", _options.CacheName, _options.InstanceId, operationId, key);

		var message = BackplaneMessage.CreateForEntrySet(_cache.InstanceId, key, timestamp);

		return PublishAsync(operationId, message, options, isAutoRecovery, isBackground, token);
	}

	public ValueTask<bool> PublishRemoveAsync(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] publishing remove", _options.CacheName, _options.InstanceId, operationId, key);

		var message = BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return PublishAsync(operationId, message, options, isAutoRecovery, isBackground, token);
	}

	public ValueTask<bool> PublishExpireAsync(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] publishing expire", _options.CacheName, _options.InstanceId, operationId, key);

		var message = options.IsFailSafeEnabled
			? BackplaneMessage.CreateForEntryExpire(_cache.InstanceId, key, timestamp)
			: BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return PublishAsync(operationId, message, options, isAutoRecovery, isBackground, token);
	}

	private async ValueTask HandleConnectAsync(BackplaneConnectionInfo info)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): [BP] backplane " + (info.IsReconnection ? "re-connected" : "connected"), _cache.CacheName, _cache.InstanceId, operationId);

		if (info.IsReconnection)
		{
			_cache.AutoRecovery.TryUpdateBarrier(operationId);
		}
	}

	private async ValueTask HandleIncomingMessageAsync(BackplaneMessage message)
	{
		if (_options.IgnoreIncomingBackplaneNotifications)
		{
			return;
		}

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

		// TEMP SWAP THE CURRENT ACTIVITY TO HAVE THE NEW ONE AS ROOT
		var previous = Activity.Current;
		Activity.Current = null;

		using var activity = Activities.SourceBackplane.StartActivityWithCommonTags(Activities.Names.BackplaneReceive, _options.CacheName, _options.InstanceId!, message.CacheKey!, operationId);
		activity?.SetTag(Tags.Names.BackplaneMessageAction, message.Action.ToString());
		activity?.SetTag(Tags.Names.BackplaneMessageSourceId, message.SourceId);

		// REVERT THE PREVIOUS CURRENT ACTIVITY
		Activity.Current = previous;

		// EVENT
		_events.OnMessageReceived(operationId, message);

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstantTimestamp})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action, message.Timestamp);

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, Activities.EventNames.BackplaneIncomingMessageInvalid);
			activity?.Dispose();

			return;
		}

		// AUTO-RECOVERY
		if (_options.EnableAutoRecovery)
		{
			if (_cache.AutoRecovery.CheckIncomingMessageForConflicts(operationId, message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been ignored since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// ACTIVITY
				activity?.SetStatus(ActivityStatusCode.Error, Activities.EventNames.BackplaneIncomingMessageConflicts);
				activity?.Dispose();

				return;
			}
		}

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				if (message.CacheKey!.StartsWith(_cache.TagInternalCacheKeyPrefix))
				{
					// HANDLE A POTENTIAL UPDATE OF A TAGGING (RemoveByTag/Clear) TIMESTAMP
					await MaybeUpdateTaggingTimestampAsync(operationId, message).ConfigureAwait(false);
				}
				else
				{
					// HANDLE SET
					await HandleIncomingMessageSetAsync(operationId, message).ConfigureAwait(false);
				}

				break;
			case BackplaneMessageAction.EntryRemove:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// HANDLE REMOVE
				_cache.RemoveMemoryEntryInternal(operationId, message.CacheKey!);
				break;
			case BackplaneMessageAction.EntryExpire:
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// HANDLE EXPIRE
				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, message.Timestamp);
				break;
			default:
				// HANDLE UNKNOWN: DO NOTHING
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action);

				// ACTIVITY
				activity?.SetStatus(ActivityStatusCode.Error, Activities.EventNames.BackplaneIncomingMessageUnknownAction);
				activity?.Dispose();

				break;
		}
	}

	private async ValueTask HandleIncomingMessageSetAsync(string operationId, BackplaneMessage message)
	{
		var cacheKey = message.CacheKey!;

		var mca = _cache.MemoryCache;

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

		var dca = _cache.DistributedCache;

		if (dca is not null)
		{
			if (dca.CanBeUsed(operationId, cacheKey) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] distributed cache not currently usable, expiring local memory entry", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

				_cache.ExpireMemoryEntryInternal(operationId, cacheKey, message.Timestamp);

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

		_cache.ExpireMemoryEntryInternal(operationId, cacheKey, message.Timestamp);
	}

	private async ValueTask MaybeUpdateTaggingTimestampAsync(string operationId, BackplaneMessage message)
	{
		//if (message.CacheKey is null)
		if (message.CacheKey is null || message.CacheKey.StartsWith(_cache.TagInternalCacheKeyPrefix) == false)
		{
			return;
		}

		if (message.CacheKey == _cache.ClearRemoveTagInternalCacheKey)
		{
			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification for a CLEAR (REMOVE) has been received from remote cache {RemoteCacheInstanceId}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

			// SET THE CLEAR (REMOVE) TIMESTAMP TO THE ONE FROMTHE BACKPLANE MESSAGE
			Interlocked.Exchange(ref _cache.ClearRemoveTimestamp, message.Timestamp);
		}
		else if (message.CacheKey == _cache.ClearExpireTagInternalCacheKey)
		{
			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification for a CLEAR (EXPIRE) has been received from remote cache {RemoteCacheInstanceId}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

			// SET THE CLEAR (REMOVE) TIMESTAMP TO THE ONE FROMTHE BACKPLANE MESSAGE
			Interlocked.Exchange(ref _cache.ClearExpireTimestamp, message.Timestamp);
		}
		else
		{
			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] a backplane notification for a REMOVE BY TAG has been received from remote cache {RemoteCacheInstanceId}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);
		}

		await _cache.SetTagDataInternalAsync(message.CacheKey.Substring(_cache.TagInternalCacheKeyPrefix.Length), message.Timestamp, _options.TagsDefaultEntryOptions.Duplicate().SetSkipDistributedCacheWrite(true, true), default).ConfigureAwait(false);
	}
}
