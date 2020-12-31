using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.FusionCaching.Serialization;

namespace ZiggyCreatures.FusionCaching.Internals
{

	internal class DistributedCacheAccessor
	{

		private const int CircuitStateClosed = 0;
		private const int CircuitStateOpen = 1;

		public DistributedCacheAccessor(IDistributedCache? distributedCache, IFusionCacheSerializer? serializer, FusionCacheOptions options, ILogger? logger)
		{
			Cache = distributedCache;
			Serializer = serializer;

			_options = options;
			_breakDuration = distributedCache is null ? TimeSpan.Zero : options.DistributedCacheCircuitBreakerDuration;
			_breakDurationTicks = _breakDuration.Ticks;
			_gatewayTicks = DateTimeOffset.MinValue.Ticks;

			_logger = logger;
		}

		private DistributedCacheAccessor()
			: this(null, null, new FusionCacheOptions(), null)
		{
		}

		private readonly FusionCacheOptions _options;
		private int _circuitState;
		private long _gatewayTicks;
		private readonly TimeSpan _breakDuration;
		private readonly long _breakDurationTicks;

		private readonly ILogger? _logger;

		public IDistributedCache? Cache { get; }
		public IFusionCacheSerializer? Serializer { get; }

		public void UpdateLastError(string key, string operationId)
		{
			// NO DISTRIBUTEC CACHE
			if (Cache is null)
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
			}
		}

		public bool IsCurrentlyUsable()
		{
			// NO DISTRIBUTEC CACHE
			if (Cache is null)
				return false;

			// NO CIRCUIT-BREAKER DURATION
			if (_breakDurationTicks == 0)
				return true;

			long _gatewayTicksLocal = Interlocked.Read(ref _gatewayTicks);

			// NOT ENOUGH TIME IS PASSED
			if (DateTimeOffset.UtcNow.Ticks < _gatewayTicksLocal)
				return false;

			if (_circuitState == CircuitStateOpen)
			{
				var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateClosed);
				if (oldCircuitState == CircuitStateOpen)
				{
					if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
						_logger.LogWarning("FUSION: distributed cache activated again");
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDistributedCacheError(string operationId, string key, Exception exc, string actionDescription)
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public async Task ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
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
					exc => ProcessDistributedCacheError(operationId, key, exc, actionDescriptionInner),
					false,
					token
				)
				.ConfigureAwait(false)
			;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, DistributedCacheEntryOptions? distributedOptions, CancellationToken token)
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
				exc => ProcessDistributedCacheError(operationId, key, exc, actionDescriptionInner),
				false,
				token
			);
		}

		public async Task SetDistributedEntryAsync<TValue>(string operationId, string key, FusionCacheEntry<TValue> entry, FusionCacheEntryOptions options, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();
			options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.Value);

			await ExecuteOperationAsync(
				operationId,
				key,
				async ct =>
				{
					byte[]? data;
					try
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): serializing the entry {Entry}", key, operationId, entry.ToLogString());

						data = await Serializer!.SerializeAsync(entry).ConfigureAwait(false);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while serializing an entry {Entry}", key, operationId, entry.ToLogString());

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): setting the entry in distributed {Entry}", key, operationId, entry.ToLogString());

					await Cache!.SetAsync(key, data, distributedOptions, token).ConfigureAwait(false);
				},
				"saving entry in distributed",
				options,
				distributedOptions,
				token
			).ConfigureAwait(false);
		}

		public void SetDistributedEntry<TValue>(string operationId, string key, FusionCacheEntry<TValue> entry, FusionCacheEntryOptions options, CancellationToken token = default)
		{
			if (IsCurrentlyUsable() == false)
				return;

			token.ThrowIfCancellationRequested();

			var distributedOptions = options.ToDistributedCacheEntryOptions();
			options.DistributedOptionsModifier?.Invoke(distributedOptions, entry.Value);

			ExecuteOperation(
				operationId,
				key,
				ct =>
				{
					byte[]? data;
					try
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): serializing the entry {Entry}", key, operationId, entry.ToLogString());

						data = Serializer!.Serialize(entry);
					}
					catch (Exception exc)
					{
						if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
							_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while serializing an entry {Entry}", key, operationId, entry.ToLogString());

						data = null;
					}

					if (data is null)
						return;

					ct.ThrowIfCancellationRequested();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION (K={CacheKey} OP={CacheOperationId}): setting the entry in distributed {Entry}", key, operationId, entry.ToLogString());

					Cache!.Set(key, data, distributedOptions);
				},
				"saving entry in distributed",
				options,
				distributedOptions,
				token
			);
		}

		public async Task<(FusionCacheEntry<TValue>? entry, bool isValid)> TryGetDistributedEntryAsync<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get entry from distributed", key, operationId);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync<byte[]?>(async ct => await Cache!.GetAsync(key, ct).ConfigureAwait(false), timeout, true, token: token).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				ProcessDistributedCacheError(operationId, key, exc, "getting entry from distributed");
				data = null;
			}

			if (data is null)
				return (null, false);

			try
			{
				var _entry = await Serializer!.DeserializeAsync<FusionCacheEntry<TValue>>(data).ConfigureAwait(false);
				var _isValid = false;
				if (_entry is null)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry not found", key, operationId);
				}
				else
				{
					if (_entry.IsLogicallyExpired())
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found (expired) {Entry}", key, operationId, _entry.ToLogString());
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found {Entry}", key, operationId, _entry.ToLogString());

						_isValid = true;
					}
				}
				return (_entry, _isValid);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
					_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while deserializing an entry", key, operationId);
			}

			return (null, false);
		}

		public (FusionCacheEntry<TValue>? entry, bool isValid) TryGetDistributedEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, CancellationToken token)
		{
			if (IsCurrentlyUsable() == false)
				return (null, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get entry from distributed", key, operationId);

			byte[]? data;
			try
			{
				var timeout = options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
				data = FusionCacheExecutionUtils.RunSyncFuncWithTimeout<byte[]?>(ct => Cache!.Get(key), timeout, true, token: token);
			}
			catch (Exception exc)
			{
				ProcessDistributedCacheError(operationId, key, exc, "getting entry from distributed");
				data = null;
			}

			if (data is null)
				return (null, false);

			try
			{
				var _entry = Serializer!.Deserialize<FusionCacheEntry<TValue>>(data);
				var _isValid = false;
				if (_entry is null)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry not found", key, operationId);
				}
				else
				{
					if (_entry.IsLogicallyExpired())
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found (expired) {Entry}", key, operationId, _entry.ToLogString());
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): distributed entry found {Entry}", key, operationId, _entry.ToLogString());

						_isValid = true;
					}
				}
				return (_entry, _isValid);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
					_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while deserializing an entry", key, operationId);
			}

			return (null, false);
		}

		public async Task RemoveDistributedEntryAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			await ExecuteOperationAsync(operationId, key, ct => Cache!.RemoveAsync(key, ct), "removing entry from distributed", options, null, token).ConfigureAwait(false);
		}

		public void RemoveDistributedEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
		{
			ExecuteOperation(operationId, key, _ => Cache!.Remove(key), "removing entry from distributed", options, null, token);
		}

	}

}