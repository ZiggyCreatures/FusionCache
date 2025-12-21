using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.DistributedLocker;

internal partial class DistributedLockerAccessor
{
	public async ValueTask<object?> AcquireLockAsync(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] waiting to acquire the DISTRIBUTED LOCK", _options.CacheName, _options.InstanceId, operationId, key);

		try
		{
			var lockObj = await _locker.AcquireLockAsync(_options.CacheName, _options.InstanceId!, operationId, key, GetLockName(key), timeout, _logger, token);

			if (lockObj is not null)
			{
				// LOCK ACQUIRED
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK acquired", _options.CacheName, _options.InstanceId, operationId, key);
			}
			else
			{
				// LOCK TIMEOUT
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK timeout", _options.CacheName, _options.InstanceId, operationId, key);
			}

			return lockObj;
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] acquiring the DISTRIBUTED LOCK has thrown an exception", _options.CacheName, _options.InstanceId, operationId, key);

			return null;
		}
	}

	public async ValueTask ReleaseDistributedLockAsync(string operationId, string key, object? lockObj, CancellationToken token)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing DISTRIBUTED LOCK", _options.CacheName, _options.InstanceId, operationId, key);

		try
		{
			await _locker.ReleaseLockAsync(_options.CacheName, _options.InstanceId!, operationId, key, GetLockName(key), lockObj, _logger, token);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK released", _options.CacheName, _options.InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing the DISTRIBUTED LOCK has thrown an exception", _options.CacheName, _options.InstanceId, operationId, key);
		}
	}

	//private async ValueTask<bool> ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, CancellationToken token)
	//{
	//	//if (IsCurrentlyUsable(operationId, key) == false)
	//	//	return false;

	//	try
	//	{
	//		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
	//			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);

	//		await RunUtils.RunAsyncActionWithTimeoutAsync(action, Timeout.InfiniteTimeSpan, true, token: token).ConfigureAwait(false);

	//		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
	//			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);
	//	}
	//	catch (Exception exc)
	//	{
	//		ProcessError(operationId, key, exc, actionDescription);

	//		// ACTIVITY
	//		Activity.Current?.SetStatus(ActivityStatusCode.Error, exc.Message);
	//		Activity.Current?.AddException(exc);

	//		if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
	//		{
	//			if (_options.ReThrowOriginalExceptions)
	//			{
	//				throw;
	//			}
	//			else
	//			{
	//				throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
	//			}
	//		}

	//		return false;
	//	}

	//	return true;
	//}

	//public async ValueTask<bool> SetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	//{
	//	if (IsCurrentlyUsable(operationId, key) == false)
	//		return false;

	//	token.ThrowIfCancellationRequested();

	//	// ACTIVITY
	//	using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedSet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

	//	// IF FAIL-SAFE IS DISABLED AND DURATION IS <= ZERO -> REMOVE ENTRY (WILL SAVE RESOURCES)
	//	if (options.IsFailSafeEnabled == false && options.DistributedCacheDuration.GetValueOrDefault(options.Duration) <= TimeSpan.Zero)
	//	{
	//		await RemoveEntryAsync(operationId, key, options, isBackground, token).ConfigureAwait(false);
	//		return true;
	//	}

	//	if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//		_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] saving distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

	//	var distributedEntry = entry.AsDistributedEntry<TValue>(options);

	//	// SERIALIZATION
	//	byte[]? data;
	//	try
	//	{
	//		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
	//			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] serializing the entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

	//		if (_options.PreferSyncSerialization)
	//		{
	//			data = _serializer.Serialize(distributedEntry);
	//		}
	//		else
	//		{
	//			data = await _serializer.SerializeAsync(distributedEntry, token).ConfigureAwait(false);
	//		}
	//	}
	//	catch (Exception exc)
	//	{
	//		if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
	//			_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while serializing an entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

	//		// ACTIVITY
	//		activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
	//		activity?.AddException(exc);

	//		// EVENT
	//		_events.OnSerializationError(operationId, key);

	//		//if (options.ReThrowSerializationExceptions)
	//		//{
	//		if (_options.ReThrowOriginalExceptions)
	//		{
	//			throw;
	//		}
	//		else
	//		{
	//			throw new FusionCacheSerializationException("An error occurred while serializing a cache value", exc);
	//		}
	//		//}

	//		//data = null;
	//	}

	//	if (data is null)
	//	{
	//		if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
	//			_logger.Log(_options.SerializationErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] the entry {Entry} has been serialized to null, skipping", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

	//		return false;
	//	}

	//	// SAVE TO DISTRIBUTED CACHE
	//	return await ExecuteOperationAsync(
	//		operationId,
	//		key,
	//		async ct =>
	//		{
	//			var distributedOptions = options.ToDistributedCacheEntryOptions(_options, _logger, operationId, key);

	//			await _locker.SetAsync(MaybeProcessCacheKey(key), data, distributedOptions, ct).ConfigureAwait(false);

	//			// EVENT
	//			_events.OnSet(operationId, key);
	//		},
	//		"setting entry in distributed" + isBackground.ToString(" (background)"),
	//		options,
	//		token
	//	).ConfigureAwait(false);
	//}

	//public async ValueTask<(FusionCacheDistributedEntry<TValue>? entry, bool isValid)> TryGetEntryAsync<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, TimeSpan? timeout, CancellationToken token)
	//{
	//	// METRIC
	//	Metrics.CounterDistributedGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

	//	if (IsCurrentlyUsable(operationId, key) == false)
	//		return (null, false);

	//	if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
	//		_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] trying to get entry from distributed", _options.CacheName, _options.InstanceId, operationId, key);

	//	// ACTIVITY
	//	using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedGet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

	//	// GET FROM DISTRIBUTED CACHE
	//	byte[]? data;
	//	try
	//	{
	//		timeout ??= options.GetAppropriateDistributedCacheTimeout(_options, hasFallbackValue);
	//		data = await RunUtils.RunAsyncFuncWithTimeoutAsync<byte[]?>(
	//			async ct => await _locker.GetAsync(MaybeProcessCacheKey(key), ct).ConfigureAwait(false),
	//			timeout.Value,
	//			true,
	//			token: token
	//		).ConfigureAwait(false);
	//	}
	//	catch (Exception exc)
	//	{
	//		ProcessError(operationId, key, exc, "getting entry from distributed");

	//		// ACTIVITY
	//		activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
	//		activity?.AddException(exc);

	//		if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
	//		{
	//			if (_options.ReThrowOriginalExceptions)
	//			{
	//				throw;
	//			}
	//			else
	//			{
	//				throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
	//			}
	//		}

	//		data = null;
	//	}

	//	if (data is null)
	//	{
	//		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);

	//		_events.OnMiss(operationId, key, activity);

	//		return (null, false);
	//	}

	//	// DESERIALIZATION
	//	try
	//	{
	//		FusionCacheDistributedEntry<TValue>? entry;
	//		if (_options.PreferSyncSerialization)
	//		{
	//			entry = _serializer.Deserialize<FusionCacheDistributedEntry<TValue>>(data);
	//		}
	//		else
	//		{
	//			entry = await _serializer.DeserializeAsync<FusionCacheDistributedEntry<TValue>>(data, token).ConfigureAwait(false);
	//		}

	//		var isValid = false;
	//		if (entry is null)
	//		{
	//			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);
	//		}
	//		else
	//		{
	//			if (entry.IsLogicallyExpired())
	//			{
	//				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found (expired) {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));
	//			}
	//			else
	//			{
	//				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

	//				isValid = true;
	//			}
	//		}

	//		// EVENT
	//		if (entry is not null)
	//		{
	//			_events.OnHit(operationId, key, isValid == false, activity);
	//		}
	//		else
	//		{
	//			_events.OnMiss(operationId, key, activity);
	//		}

	//		return (entry, isValid);
	//	}
	//	catch (Exception exc)
	//	{
	//		if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
	//			_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while deserializing an entry", _options.CacheName, _options.InstanceId, operationId, key);

	//		// ACTIVITY
	//		activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
	//		activity?.AddException(exc);

	//		// EVENT
	//		_events.OnDeserializationError(operationId, key);

	//		if (options.ReThrowSerializationExceptions)
	//		{
	//			if (_options.ReThrowOriginalExceptions)
	//			{
	//				throw;
	//			}
	//			else
	//			{
	//				throw new FusionCacheSerializationException("An error occurred while deserializing a cache value", exc);
	//			}
	//		}
	//	}

	//	// EVENT
	//	_events.OnMiss(operationId, key, activity);

	//	return (null, false);
	//}

	//public async ValueTask<bool> RemoveEntryAsync(string operationId, string key, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	//{
	//	if (IsCurrentlyUsable(operationId, key) == false)
	//		return false;

	//	// ACTIVITY
	//	using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedRemove, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

	//	if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
	//		_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] removing distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

	//	return await ExecuteOperationAsync(
	//		operationId,
	//		key,
	//		async ct =>
	//		{
	//			await _locker.RemoveAsync(MaybeProcessCacheKey(key), ct);

	//			// EVENT
	//			_events.OnRemove(operationId, key);
	//		},
	//		"removing entry from distributed" + isBackground.ToString(" (background)"),
	//		options,
	//		token
	//	).ConfigureAwait(false);
	//}
}
