using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

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
				_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner, _options.CacheName, operationId, key);
			else
				_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner + " {DistributedOptions}", _options.CacheName, operationId, key, distributedOptions.ToLogString());
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

		var distributedEntry = entry.AsDistributedEntry<TValue>(options);

		// SERIALIZATION
		byte[]? data;
		try
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): serializing the entry {Entry}", _options.CacheName, operationId, key, distributedEntry.ToLogString());

			data = await _serializer.SerializeAsync(distributedEntry).ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while serializing an entry {Entry}", _options.CacheName, operationId, key, distributedEntry.ToLogString());

			if (options.ReThrowSerializationExceptions)
				throw;

			// EVENT
			_events.OnSerializationError(operationId, key);

			data = null;
		}

		if (data is null)
			return;

		// SAVE TO DISTRIBUTED CACHE
		var distributedOptions = options.ToDistributedCacheEntryOptions(_options, _logger, operationId, key);
		await ExecuteOperationAsync(
			operationId,
			key,
			async ct =>
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): setting the entry in distributed {Entry}", _options.CacheName, operationId, key, distributedEntry.ToLogString());

				await _cache.SetAsync(MaybeProcessCacheKey(key), data, distributedOptions, ct).ConfigureAwait(false);

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
			_logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): trying to get entry from distributed", _options.CacheName, operationId, key);

		// GET FROM DISTRIBUTED CACHE
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

		// DESERIALIZATION
		try
		{
			var entry = await _serializer.DeserializeAsync<FusionCacheDistributedEntry<TValue>>(data).ConfigureAwait(false);
			var isValid = false;
			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): distributed entry not found", _options.CacheName, operationId, key);
			}
			else
			{
				if (entry.IsLogicallyExpired())
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): distributed entry found (expired) {Entry}", _options.CacheName, operationId, key, entry.ToLogString());
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): distributed entry found {Entry}", _options.CacheName, operationId, key, entry.ToLogString());

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
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while deserializing an entry", _options.CacheName, operationId, key);

			if (options.ReThrowSerializationExceptions)
				throw;

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
