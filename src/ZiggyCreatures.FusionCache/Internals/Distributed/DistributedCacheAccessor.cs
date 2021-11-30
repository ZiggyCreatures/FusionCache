using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed
{
	internal class DistributedCacheAccessor
	{
		private const string WireFormatVersion = "v1";
		private const char WireFormatSeparator = ':';

		public DistributedCacheAccessor(IDistributedCache distributedCache, IFusionCacheSerializer serializer, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
		{
			if (distributedCache is null)
				throw new ArgumentNullException(nameof(distributedCache));

			if (serializer is null)
				throw new ArgumentNullException(nameof(serializer));

			_cache = distributedCache;
			_serializer = serializer;

			_options = options;

			_logger = logger;
			_events = events;

			// CIRCUIT-BREAKER
			_breaker = new SimpleCircuitBreaker(distributedCache is null ? TimeSpan.Zero : options.DistributedCacheCircuitBreakerDuration);

			// WIRE FORMAT SETUP
			_wireFormatToken = _options.DistributedCacheKeyModifierMode == CacheKeyModifierMode.Prefix
				? (WireFormatVersion + WireFormatSeparator)
				: _options.DistributedCacheKeyModifierMode == CacheKeyModifierMode.Suffix
					? WireFormatSeparator + WireFormatVersion
					: string.Empty;
		}

		private readonly IDistributedCache _cache;
		private readonly IFusionCacheSerializer _serializer;
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly FusionCacheDistributedEventsHub _events;
		private readonly SimpleCircuitBreaker _breaker;
		private readonly string _wireFormatToken;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private string MaybeProcessCacheKey(string key)
		{
			switch (_options.DistributedCacheKeyModifierMode)
			{
				case CacheKeyModifierMode.Prefix:
					return _wireFormatToken + key;
				case CacheKeyModifierMode.Suffix:
					return key + _wireFormatToken;
				default:
					return key;
			}
		}

		private void UpdateLastError(string key, string operationId)
		{
			// NO DISTRIBUTEC CACHE
			if (_cache is null)
				return;

			var res = _breaker.TryOpen(out var hasChanged);

			if (res && hasChanged)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION (O={CacheOperationId} K={CacheKey}): distributed cache temporarily de-activated for {BreakDuration}", operationId, key, _breaker.BreakDuration);

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
					_logger.LogWarning("FUSION: distributed cache activated again");

				// EVENT
				_events.OnCircuitBreakerChange(operationId, key, true);
			}

			return res;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessCacheError(string operationId, string key, Exception exc, string actionDescription)
		{
			if (exc is SyntheticTimeoutException)
			{
				if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
					_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, operationId, key);

				return;
			}

			// TODO: MAYBE ALWAYS CALL UpdateLastError(...) ? MAYBE NOT?
			UpdateLastError(key, operationId);

			if (_logger?.IsEnabled(_options.DistributedCacheErrorsLogLevel) ?? false)
				_logger.Log(_options.DistributedCacheErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, operationId, key);
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async ValueTask ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var actionDescriptionInner = actionDescription + (options.AllowBackgroundDistributedCacheOperations ? " (background)" : null);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner + " {DistributedOptions}", operationId, key, distributedOptions.ToLogString());

			await FusionCacheExecutionUtils
				.RunAsyncActionAdvancedAsync(
					action,
					options.DistributedCacheHardTimeout,
					false,
					options.AllowBackgroundDistributedCacheOperations == false,
					exc => ProcessCacheError(operationId, key, exc, actionDescriptionInner),
					false,
					token
				)
				.ConfigureAwait(false)
			;
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var actionDescriptionInner = actionDescription + (options.AllowBackgroundDistributedCacheOperations ? " (background)" : null);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner + " {DistributedOptions}", operationId, key, distributedOptions.ToLogString());

			FusionCacheExecutionUtils.RunSyncActionAdvanced(
				action,
				options.DistributedCacheHardTimeout,
				false,
				options.AllowBackgroundDistributedCacheOperations == false,
				exc => ProcessCacheError(operationId, key, exc, actionDescriptionInner),
				false,
				token
			);
		}

		public async ValueTask SetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();

			//options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.GetValue<TValue>());

			await ExecuteOperationAsync(
				operationId,
				key,
				async ct =>
				{
					var distributedEntry = entry.AsDistributedEntry<TValue>();

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

		public void SetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token = default)
		{
			if (IsCurrentlyUsable(operationId, key) == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();

			//options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.GetValue<TValue>());

			ExecuteOperation(
				operationId,
				key,
				ct =>
				{
					var distributedEntry = entry.AsDistributedEntry<TValue>();

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
				ProcessCacheError(operationId, key, exc, "getting entry from distributed");
				data = null;
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
				data = FusionCacheExecutionUtils.RunSyncFuncWithTimeout<byte[]?>(ct => _cache.Get(MaybeProcessCacheKey(key)), timeout, true, token: token);
			}
			catch (Exception exc)
			{
				ProcessCacheError(operationId, key, exc, "getting entry from distributed");
				data = null;
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

		public void RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			ExecuteOperation(operationId, key, _ => _cache.Remove(MaybeProcessCacheKey(key)), "removing entry from distributed", options, null, token);

			// EVENT
			_events.OnRemove(operationId, key);
		}
	}
}
