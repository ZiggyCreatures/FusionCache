using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed
{
	internal partial class DistributedCacheAccessor
	{
		private async ValueTask ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
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

			await FusionCacheExecutionUtils
				.RunAsyncActionAdvancedAsync(
					action,
					options.DistributedCacheHardTimeout,
					false,
					options.AllowBackgroundDistributedCacheOperations == false,
					exc => ProcessError(operationId, key, exc, actionDescriptionInner),
					options.ReThrowDistributedCacheExceptions && options.AllowBackgroundDistributedCacheOperations == false && options.DistributedCacheHardTimeout == Timeout.InfiniteTimeSpan,
					token
				)
				.ConfigureAwait(false)
			;
		}

		public async ValueTask SetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();

			await ExecuteOperationAsync(
				operationId,
				key,
				async ct =>
				{
					var distributedEntry = entry.AsDistributedEntry<TValue>(options);

					byte[]? data;
					try
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION (O={CacheOperationId} K={CacheKey}): serializing the entry {Entry}", operationId, key, distributedEntry.ToLogString());

						data = await _serializer.SerializeAsync(distributedEntry).ConfigureAwait(false);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while serializing an entry {Entry}", operationId, key, distributedEntry.ToLogString());

						// EVENT
						_events.OnSerializationError(operationId, key);

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (O={CacheOperationId} K={CacheKey}): setting the entry in distributed {Entry}", operationId, key, distributedEntry.ToLogString());

					await _cache.SetAsync(MaybeProcessCacheKey(key), data, distributedOptions, token).ConfigureAwait(false);

					// EVENT
					_events.OnSet(operationId, key);
				},
				"saving entry in distributed",
				options,
				distributedOptions,
				token
			).ConfigureAwait(false);
		}

		public async ValueTask<(FusionCacheDistributedEntry<TValue>? entry, bool isValid)> TryGetEntryAsync<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): trying to get entry from distributed", operationId, key);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync<byte[]?>(async ct => await _cache.GetAsync(MaybeProcessCacheKey(key), ct).ConfigureAwait(false), timeout, true, token: token).ConfigureAwait(false);
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
				var entry = await _serializer.DeserializeAsync<FusionCacheDistributedEntry<TValue>>(data).ConfigureAwait(false);
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
				if (entry is object)
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

				// EVENT
				_events.OnDeserializationError(operationId, key);
			}

			// EVENT
			_events.OnMiss(operationId, key);

			return (null, false);
		}

		public async ValueTask RemoveEntryAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			await ExecuteOperationAsync(operationId, key, ct => _cache.RemoveAsync(MaybeProcessCacheKey(key), ct), "removing entry from distributed", options, null, token);

			// EVENT
			_events.OnRemove(operationId, key);
		}
	}
}
