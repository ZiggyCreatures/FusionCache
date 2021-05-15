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

		private const int CircuitStateClosed = 0;
		private const int CircuitStateOpen = 1;
		private const string WireFormatVersionPrefix = "v1:";

		public DistributedCacheAccessor(IDistributedCache distributedCache, IFusionCacheSerializer serializer, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
		{
			if (distributedCache == null)
				throw new ArgumentNullException(nameof(distributedCache));

			if (serializer == null)
				throw new ArgumentNullException(nameof(serializer));

			_cache = distributedCache;
			_serializer = serializer;

			_options = options;
			_breakDuration = distributedCache is null ? TimeSpan.Zero : options.DistributedCacheCircuitBreakerDuration;
			_breakDurationTicks = _breakDuration.Ticks;
			_gatewayTicks = DateTimeOffset.MinValue.Ticks;

			_logger = logger;
			_events = events;
		}

		private int _circuitState;
		private long _gatewayTicks;
		private readonly TimeSpan _breakDuration;
		private readonly long _breakDurationTicks;

		private IDistributedCache _cache;
		private IFusionCacheSerializer _serializer;
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly FusionCacheDistributedEventsHub _events;


		private void UpdateLastError(string key, string operationId)
		{
			// NO DISTRIBUTEC CACHE
			if (_cache is null)
				return;

			// NO CIRCUIT-BREAKER DURATION
			if (_breakDurationTicks == 0)
				return;

			Interlocked.Exchange(ref _gatewayTicks, DateTimeOffset.UtcNow.Ticks + _breakDurationTicks);

			// DETECT CIRCUIT STATE CHANGE
			var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateOpen);
			if (oldCircuitState == CircuitStateClosed)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION (K={CacheKey} OP={CacheOperationId}): distributed cache temporarily de-activated for {BreakDuration}", key, operationId, _breakDuration);

				// EVENT
				_events.OnCircuitBreakerChange(operationId, key, false);
			}
		}

		public bool IsCurrentlyUsable()
		{
			// NO DISTRIBUTEC CACHE
			if (_cache is null)
				return false;

			// NO CIRCUIT-BREAKER DURATION
			if (_breakDurationTicks == 0)
				return true;

			long gatewayTicksLocal = Interlocked.Read(ref _gatewayTicks);

			// NOT ENOUGH TIME IS PASSED
			if (DateTimeOffset.UtcNow.Ticks < gatewayTicksLocal)
				return false;

			if (_circuitState == CircuitStateOpen)
			{
				var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateClosed);
				if (oldCircuitState == CircuitStateOpen)
				{
					if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
						_logger.LogWarning("FUSION: distributed cache activated again");

					// EVENT
					_events.OnCircuitBreakerChange(null, null, true);
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessCacheError(string operationId, string key, Exception exc, string actionDescription)
		{
			if (exc is SyntheticTimeoutException)
			{
				if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
					_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): a synthetic timeout occurred while " + actionDescription, key, operationId);

				return;
			}

			// TODO: MAYBE ALWAYS CALL UpdateLastError(...) ? MAYBE NOT?
			UpdateLastError(key, operationId);

			if (_logger?.IsEnabled(_options.DistributedCacheErrorsLogLevel) ?? false)
				_logger.Log(_options.DistributedCacheErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while " + actionDescription, key, operationId);
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var actionDescriptionInner = actionDescription + (options.AllowBackgroundDistributedCacheOperations ? " (background)" : null);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): " + actionDescriptionInner + " {DistributedOptions}", key, operationId, distributedOptions.ToLogString());

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
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var actionDescriptionInner = actionDescription + (options.AllowBackgroundDistributedCacheOperations ? " (background)" : null);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): " + actionDescriptionInner + " {DistributedOptions}", key, operationId, distributedOptions.ToLogString());

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

		public async Task SetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();
			options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.GetValue<TValue>());

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
							_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): serializing the entry {Entry}", key, operationId, distributedEntry.ToLogString());

						data = await _serializer.SerializeAsync(distributedEntry).ConfigureAwait(false);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while serializing an entry {Entry}", key, operationId, distributedEntry.ToLogString());

						// EVENT
						_events.OnSerializationError(operationId, key);

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): setting the entry in distributed {Entry}", key, operationId, distributedEntry.ToLogString());

					await _cache.SetAsync(WireFormatVersionPrefix + key, data, distributedOptions, token).ConfigureAwait(false);

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
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();
			options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.GetValue<TValue>());

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
							_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): serializing the entry {Entry}", key, operationId, distributedEntry.ToLogString());

						data = _serializer.Serialize(distributedEntry);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while serializing an entry {Entry}", key, operationId, distributedEntry.ToLogString());

						// EVENT
						_events.OnSerializationError(operationId, key);

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): setting the entry in distributed {Entry}", key, operationId, distributedEntry.ToLogString());

					_cache.Set(WireFormatVersionPrefix + key, data, distributedOptions);

					// EVENT
					_events.OnSet(operationId, key);
				},
				"saving entry in distributed",
				options,
				distributedOptions,
				token
			);
		}

		public async Task<(FusionCacheDistributedEntry<TValue>? entry, bool isValid)> TryGetEntryAsync<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get entry from distributed", key, operationId);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync<byte[]?>(async ct => await _cache.GetAsync(WireFormatVersionPrefix + key, ct).ConfigureAwait(false), timeout, true, token: token).ConfigureAwait(false);
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
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry not found", key, operationId);
				}
				else
				{
					if (entry.IsLogicallyExpired())
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found (expired) {Entry}", key, operationId, entry.ToLogString());
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found {Entry}", key, operationId, entry.ToLogString());

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
					_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while deserializing an entry", key, operationId);

				// EVENT
				_events.OnDeserializationError(operationId, key);
			}

			// EVENT
			_events.OnMiss(operationId, key);

			return (null, false);
		}

		public (FusionCacheDistributedEntry<TValue>? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get entry from distributed", key, operationId);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = FusionCacheExecutionUtils.RunSyncFuncWithTimeout<byte[]?>(ct => _cache.Get(WireFormatVersionPrefix + key), timeout, true, token: token);
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
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry not found", key, operationId);
				}
				else
				{
					if (entry.IsLogicallyExpired())
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found (expired) {Entry}", key, operationId, entry.ToLogString());
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found {Entry}", key, operationId, entry.ToLogString());

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
					_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while deserializing an entry", key, operationId);

				// EVENT
				_events.OnDeserializationError(operationId, key);
			}

			// EVENT
			_events.OnMiss(operationId, key);

			return (null, false);
		}

		public async Task RemoveEntryAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			await ExecuteOperationAsync(operationId, key, ct => _cache.RemoveAsync(WireFormatVersionPrefix + key, ct), "removing entry from distributed", options, null, token);

			// EVENT
			_events.OnRemove(operationId, key);
		}

		public void RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			ExecuteOperation(operationId, key, _ => _cache.Remove(WireFormatVersionPrefix + key), "removing entry from distributed", options, null, token);

			// EVENT
			_events.OnRemove(operationId, key);
		}

	}

}
