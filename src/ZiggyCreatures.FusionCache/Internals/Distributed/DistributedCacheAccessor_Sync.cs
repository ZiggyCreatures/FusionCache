using System;
using System.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed
{
	internal partial class DistributedCacheAccessor
	{
		private void ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var actionDescriptionInner = actionDescription + (options.AllowBackgroundDistributedCacheOperations ? " (background)" : null);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			{
				if (distributedOptions is null)
					_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner, operationId, key);
				else
					_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner + " {DistributedOptions}", operationId, key, distributedOptions.ToLogString());
			}

			FusionCacheExecutionUtils
				.RunSyncActionAdvanced(
					action,
					options.DistributedCacheHardTimeout,
					false,
					options.AllowBackgroundDistributedCacheOperations == false,
					exc => ProcessError(operationId, key, exc, actionDescriptionInner),
					options.ReThrowDistributedCacheExceptions && options.AllowBackgroundDistributedCacheOperations == false && options.DistributedCacheHardTimeout == Timeout.InfiniteTimeSpan,
					token
				)
			;
		}

		public void SetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token = default)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();

			ExecuteOperation(
				operationId,
				key,
				ct =>
				{
					var distributedEntry = entry.AsDistributedEntry<TValue>(options);

					byte[]? data;
					try
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION (O={CacheOperationId} K={CacheKey}): serializing the entry {Entry}", operationId, key, distributedEntry.ToLogString());

						data = _serializer.Serialize(distributedEntry);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while serializing an entry {Entry}", operationId, key, distributedEntry.ToLogString());

						if (options.ReThrowSerializationExceptions)
							throw;

						// EVENT
						_events.OnSerializationError(operationId, key);

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (O={CacheOperationId} K={CacheKey}): setting the entry in distributed {Entry}", operationId, key, distributedEntry.ToLogString());

					_cache.Set(MaybeProcessCacheKey(key), data, distributedOptions);

					// EVENT
					_events.OnSet(operationId, key);
				},
				"saving entry in distributed",
				options,
				distributedOptions,
				token
			);
		}

		public (FusionCacheDistributedEntry<TValue>? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): trying to get entry from distributed", operationId, key);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = FusionCacheExecutionUtils.RunSyncFuncWithTimeout<byte[]?>(_ => _cache.Get(MaybeProcessCacheKey(key)), timeout, true, token: token);
			}
			catch (Exception exc)
			{
				ProcessError(operationId, key, exc, "getting entry from distributed");
				data = null;

				if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
				{
					throw;
				}
			}

			if (data is null)
				return (null, false);

			try
			{
				var entry = _serializer.Deserialize<FusionCacheDistributedEntry<TValue>>(data);
				var isValid = false;
				if (entry is null)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): distributed entry not found", operationId, key);
				}
				else
				{
					if (entry.IsLogicallyExpired())
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): distributed entry found (expired) {Entry}", operationId, key, entry.ToLogString());
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): distributed entry found {Entry}", operationId, key, entry.ToLogString());

						isValid = true;
					}
				}

				// EVENT
				if (entry is not null)
				{
					_events.OnHit(operationId, key, isValid == false);
				}
				else
				{
					_events.OnMiss(operationId, key);
				}

				return (entry, isValid);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
					_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while deserializing an entry", operationId, key);

				if (options.ReThrowSerializationExceptions)
					throw;

				// EVENT
				_events.OnDeserializationError(operationId, key);
			}

			// EVENT
			_events.OnMiss(operationId, key);

			return (null, false);
		}

		public void RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			ExecuteOperation(operationId, key, _ => _cache.Remove(MaybeProcessCacheKey(key)), "removing entry from distributed", options, null, token);

			// EVENT
			_events.OnRemove(operationId, key);
		}
	}
}
