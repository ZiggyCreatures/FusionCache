using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal abstract partial class DistributedCacheAccessor<TSerialized>
{
	protected abstract TSerialized? Serialize<T>(FusionCacheDistributedEntry<T> obj);

	protected abstract T? Deserialize<T>(TSerialized data);

	protected abstract TSerialized? GetCacheEntry(string key);
	
	protected abstract void SetCacheEntry(string key, TSerialized data, DistributedCacheEntryOptions distributedOptions);

	protected abstract void RemoveCacheEntry(string key);
	
	private bool ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, CancellationToken token)
	{
		//if (IsCurrentlyUsable(operationId, key) == false)
		//	return false;

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);

			RunUtils.RunSyncActionWithTimeout(action, Timeout.InfiniteTimeSpan, true, token: token);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, key, exc, actionDescription);

			// ACTIVITY
			Activity.Current?.SetStatus(ActivityStatusCode.Error, exc.Message);
			Activity.Current?.AddException(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
				}
			}

			return false;
		}

		return true;
	}

	public bool SetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return false;

		token.ThrowIfCancellationRequested();

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedSet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		// IF FAIL-SAFE IS DISABLED AND DURATION IS <= ZERO -> REMOVE ENTRY (WILL SAVE RESOURCES)
		if (options.IsFailSafeEnabled == false && options.DistributedCacheDuration.GetValueOrDefault(options.Duration) <= TimeSpan.Zero)
		{
			RemoveEntry(operationId, key, options, isBackground, token);
			return true;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] saving distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

		var distributedEntry = entry.AsDistributedEntry<TValue>(options);

		// SERIALIZATION
		TSerialized? data;
		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] serializing the entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			data = Serialize(distributedEntry);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while serializing an entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			// EVENT
			_events.OnSerializationError(operationId, key);

			//if (options.ReThrowSerializationExceptions)
			//{
			if (_options.ReThrowOriginalExceptions)
			{
				throw;
			}
			else
			{
				throw new FusionCacheSerializationException("An error occurred while serializing a cache value", exc);
			}
			//}

			//data = null;
		}

		if (data is null)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] the entry {Entry} has been serialized to null, skipping", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			return false;
		}

		// SAVE TO DISTRIBUTED CACHE
		return ExecuteOperation(
			operationId,
			key,
			_ =>
			{
				var distributedOptions = options.ToDistributedCacheEntryOptions(_options, _logger, operationId, key);

				SetCacheEntry(MaybeProcessCacheKey(key), data, distributedOptions);

				// EVENT
				_events.OnSet(operationId, key);
			},
			"setting entry in distributed" + isBackground.ToString(" (background)"),
			options,
			token
		);
	}

	public (FusionCacheDistributedEntry<TValue>? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, TimeSpan? timeout, CancellationToken token)
	{
		// METRIC
		Metrics.CounterDistributedGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		if (IsCurrentlyUsable(operationId, key) == false)
			return (null, false);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] trying to get entry from distributed", _options.CacheName, _options.InstanceId, operationId, key);

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedGet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		// GET FROM DISTRIBUTED CACHE
		TSerialized? data;
		try
		{
			timeout ??= options.GetAppropriateDistributedCacheTimeout(_options, hasFallbackValue);
			data = RunUtils.RunSyncFuncWithTimeout<TSerialized?>(
				_ => GetCacheEntry(MaybeProcessCacheKey(key)),
				timeout.Value,
				true,
				token: token
			);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, key, exc, "getting entry from distributed");

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
				}
			}

			data = default;
		}

		if (data is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);

			_events.OnMiss(operationId, key, activity);

			return (null, false);
		}

		// DESERIALIZATION
		try
		{
			var entry = Deserialize<FusionCacheDistributedEntry<TValue>>(data);
			var isValid = false;
			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);
			}
			else
			{
				if (entry.IsLogicallyExpired())
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found (expired) {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

					isValid = true;
				}
			}

			// EVENT
			if (entry is not null)
			{
				_events.OnHit(operationId, key, isValid == false, activity);
			}
			else
			{
				_events.OnMiss(operationId, key, activity);
			}

			return (entry, isValid);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while deserializing an entry", _options.CacheName, _options.InstanceId, operationId, key);

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			// EVENT
			_events.OnDeserializationError(operationId, key);

			if (options.ReThrowSerializationExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheSerializationException("An error occurred while deserializing a cache value", exc);
				}
			}
		}

		// EVENT
		_events.OnMiss(operationId, key, activity);

		return (null, false);
	}

	public bool RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return false;

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedRemove, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] removing distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

		return ExecuteOperation(
			operationId,
			key,
			_ =>
			{
				RemoveCacheEntry(MaybeProcessCacheKey(key));

				// EVENT
				_events.OnRemove(operationId, key);
			},
			"removing entry from distributed" + isBackground.ToString(" (background)"),
			options,
			token
		);
	}
}
