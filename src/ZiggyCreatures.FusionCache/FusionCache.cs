using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.FusionCaching.Internals;
using ZiggyCreatures.FusionCaching.Reactors;
using ZiggyCreatures.FusionCaching.Serialization;

namespace ZiggyCreatures.FusionCaching
{

	/// <inheritdoc/>
	public class FusionCache
		: IFusionCache
	{

		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly IFusionCacheReactor _reactor;
		private IMemoryCache _memoryCache;
		private bool _memoryCacheShouldBeDisposed;
		private DistributedCacheAccessor? _dca;

		/// <summary>
		/// Creates a new <see cref="FusionCache"/> instance.
		/// </summary>
		/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
		/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use. If null, one will be automatically created and managed.</param>
		/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
		/// <param name="reactor">The <see cref="IFusionCacheReactor"/> instance to use (advanced). If null, a standard one will be automatically created and managed.</param>
		public FusionCache(IOptions<FusionCacheOptions> optionsAccessor, IMemoryCache? memoryCache = null, ILogger<FusionCache>? logger = null, IFusionCacheReactor? reactor = null)
		{
			if (optionsAccessor is null)
				throw new ArgumentNullException(nameof(optionsAccessor));

			// OPTIONS
			_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

			// LOGGING
			if (logger is NullLogger<FusionCache>)
			{
				// IGNORE NULL LOGGER (FOR BETTER PERF)
				_logger = null;
			}
			else
			{
				_logger = logger;
			}

			// REACTOR
			_reactor = reactor ?? new FusionCacheReactorStandard();

			// MEMORY CACHE
			if (memoryCache is object)
			{
				_memoryCache = memoryCache;
			}
			else
			{
				_memoryCache = new MemoryCache(new MemoryCacheOptions { });
				_memoryCacheShouldBeDisposed = true;
			}

			// DISTRIBUTED CACHE
			_dca = null;
		}

		/// <inheritdoc/>
		public FusionCacheEntryOptions DefaultEntryOptions
		{
			get { return _options.DefaultEntryOptions; }
		}

		/// <inheritdoc/>
		public IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer)
		{
			if (distributedCache is null)
				throw new ArgumentNullException(nameof(distributedCache));

			if (serializer is null)
				throw new ArgumentNullException(nameof(serializer));

			_dca = new DistributedCacheAccessor(distributedCache, serializer, _options, _logger);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION: setup distributed cache (CACHE={DistributedCacheType} SERIALIZER={SerializerType})", distributedCache.GetType().FullName, serializer.GetType().FullName);

			return this;
		}

		/// <inheritdoc/>
		public IFusionCache RemoveDistributedCache()
		{
			_dca = null;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION: distributed cache removed");

			return this;
		}

		/// <inheritdoc/>
		public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null, bool includeOptionsModifiers = true)
		{
			var _res = _options.DefaultEntryOptions.Duplicate(duration, includeOptionsModifiers);
			setupAction?.Invoke(_res);
			return _res;
		}

		private void ValidateCacheKey(string key)
		{
			if (key is null)
				throw new ArgumentNullException(nameof(key));
		}

		private void MaybeProcessCacheKey(ref string key)
		{
			if (string.IsNullOrEmpty(_options.CacheKeyPrefix))
				return;

			key = _options.CacheKeyPrefix + key;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private string GenerateOperationId()
		{
			if (_logger is null)
				return string.Empty;

			return Guid.NewGuid().ToString("N");
		}

		private DistributedCacheAccessor? GetCurrentDistributedAccessor()
		{
			return _dca;
		}

		private void SetMemoryEntry<TValue>(string operationId, string key, FusionCacheEntry<TValue> entry, FusionCacheEntryOptions options)
		{
			var memoryOptions = options.ToMemoryCacheEntryOptions();

			options.MemoryOptionsModifier?.Invoke(memoryOptions, entry.Value);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): saving entry in memory {Options} {Entry}", key, operationId, memoryOptions.ToLogString(), entry.ToLogString());

			_memoryCache.Set<FusionCacheEntry<TValue>>(key, entry, memoryOptions);
		}

		private FusionCacheEntry<TValue>? MaybeGetFallbackEntry<TValue>(string operationId, string key, FusionCacheEntry<TValue>? distributedEntry, FusionCacheEntry<TValue>? memoryEntry, FusionCacheEntryOptions options, out bool failSafeActivated)
		{
			failSafeActivated = false;

			if (options.IsFailSafeEnabled)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to activate FAIL-SAFE", key, operationId);
				if (distributedEntry is object)
				{
					// FAIL SAFE (FROM DISTRIBUTED)
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (K={CacheKey} OP={CacheOperationId}): FAIL-SAFE activated (from distributed)", key, operationId);
					failSafeActivated = true;
					return distributedEntry;
				}
				else if (memoryEntry is object)
				{
					// FAIL SAFE (FROM MEMORY)
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (K={CacheKey} OP={CacheOperationId}): FAIL-SAFE activated (from memory)", key, operationId);
					failSafeActivated = true;
					return memoryEntry;
				}
				else
				{
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (K={CacheKey} OP={CacheOperationId}): unable to activate FAIL-SAFE (no entries in memory or distributed)", key, operationId);
					return null;
				}
			}
			else
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): FAIL-SAFE not enabled", key, operationId);
				return null;
			}
		}

		private (FusionCacheEntry<TValue>? entry, bool isValid) TryGetMemoryEntry<TValue>(string operationId, string key)
		{
			FusionCacheEntry<TValue>? _entry;
			bool _isValid = false;

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get from memory", key, operationId);

			if (_memoryCache.TryGetValue<FusionCacheEntry<TValue>>(key, out _entry) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry not found", key, operationId);
			}
			else
			{
				if (_entry.IsLogicallyExpired())
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry found (expired) {Entry}", key, operationId, _entry.ToLogString());
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry found {Entry}", key, operationId, _entry.ToLogString());

					_isValid = true;
				}
			}

			return (_entry, _isValid);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void MaybeBackgroundCompleteTimedOutFactory<TValue>(string operationId, string key, Task<TValue>? factoryTask, FusionCacheEntryOptions options, DistributedCacheAccessor? dca, CancellationToken token)
		{
			if (options.AllowTimedOutFactoryBackgroundCompletion == false || factoryTask is null)
				return;

			if (factoryTask.IsFaulted)
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, factoryTask.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (K={CacheKey} OP={CacheOperationId}): a timed-out factory thrown an exception", key, operationId);
				return;
			}

			// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): trying to complete the timed-out factory in the background", key, operationId);

			_ = factoryTask.ContinueWith(antecedent =>
			{
				if (antecedent.Status == TaskStatus.Faulted)
				{
					if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
						_logger.Log(_options.FactoryErrorsLogLevel, antecedent.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (K={CacheKey} OP={CacheOperationId}): a timed-out factory thrown an exception", key, operationId);
				}
				else if (antecedent.Status == TaskStatus.RanToCompletion)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): a timed-out factory successfully completed in the background: keeping the result", key, operationId);

					var _lateEntry = FusionCacheEntry<TValue>.CreateFromOptions(antecedent.Result, options, false);
					_ = dca?.SetDistributedEntryAsync(operationId, key, _lateEntry, options, token);
					SetMemoryEntry(operationId, key, _lateEntry, options);
				}
			});
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReleaseLock(string operationId, string key, object? lockObj)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): releasing LOCK", key, operationId);
			try
			{
				_reactor.ReleaseLock(key, operationId, lockObj, _logger);
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK released", key, operationId);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning(exc, "FUSION (K={CacheKey} OP={CacheOperationId}): releasing the LOCK has thrown an exception", key, operationId);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessFactoryError(string operationId, string key, Exception exc)
		{
			if (exc is SyntheticTimeoutException)
			{
				if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
					_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): a synthetic timeout occurred while calling the factory", key, operationId);

				return;
			}

			if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
				_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while calling the factory", key, operationId);
		}

		private async Task<FusionCacheEntry<TValue>?> GetOrSetEntryInternalAsync<TValue>(string operationId, string key, Func<CancellationToken, Task<TValue>>? factory, FusionCacheEntryOptions? options, CancellationToken token)
		{
			if (options is null)
				options = _options.DefaultEntryOptions;

			token.ThrowIfCancellationRequested();

			FusionCacheEntry<TValue>? _memoryEntry;
			bool _memoryEntryIsValid;

			// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
			(_memoryEntry, _memoryEntryIsValid) = TryGetMemoryEntry<TValue>(operationId, key);
			if (_memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry", key, operationId);
				return _memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor();

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable() ?? false) == false)
			{
				if (options.IsFailSafeEnabled && _memoryEntry is object)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry (expired)", key, operationId);
					return _memoryEntry;
				}

				return null;
			}

			FusionCacheEntry<TValue>? _entry;

			// LOCK
			var _lockObj = await _reactor.AcquireLockAsync(key, operationId, options.LockTimeout, _logger, token).ConfigureAwait(false);

			try
			{
				// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
				(_memoryEntry, _memoryEntryIsValid) = TryGetMemoryEntry<TValue>(operationId, key);
				if (_memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry", key, operationId);
					return _memoryEntry;
				}

				// TRY WITH DISTRIBUTED CACHE (IF ANY)
				FusionCacheEntry<TValue>? _distributedEntry = null;
				bool _distributedEntryIsValid = false;

				if (dca?.IsCurrentlyUsable() ?? false)
				{
					(_distributedEntry, _distributedEntryIsValid) = await dca.TryGetDistributedEntryAsync<TValue>(operationId, key, options, _memoryEntry is object, token).ConfigureAwait(false);
				}

				if (_distributedEntryIsValid)
				{
					_entry = FusionCacheEntry<TValue>.CreateFromOptions(_distributedEntry!.Value, options, false);
				}
				else
				{
					TValue _value;
					bool _failSafeActivated = false;

					if (factory is null)
					{
						// NO FACTORY

						var _fallbackEntry = MaybeGetFallbackEntry(operationId, key, _distributedEntry, _memoryEntry, options, out _failSafeActivated);
						if (_fallbackEntry is object)
						{
							_value = _fallbackEntry.Value;
						}
						else
						{
							return null;
						}
					}
					else
					{
						// FACTORY

						Task<TValue>? factoryTask = null;

						try
						{
							var timeout = options.GetAppropriateFactoryTimeout(_memoryEntry is object || _distributedEntry is object);

							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling the factory (timeout={Timeout})", key, operationId, timeout.ToLogString_Timeout());

							_value = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync(ct => factory(ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token).ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception exc)
						{
							ProcessFactoryError(operationId, key, exc);

							MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, factoryTask, options, dca, token);

							var _fallbackEntry = MaybeGetFallbackEntry(operationId, key, _distributedEntry, _memoryEntry, options, out _failSafeActivated);
							if (_fallbackEntry is object)
							{
								_value = _fallbackEntry.Value;
							}
							else
							{
								throw;
							}
						}
					}

					_entry = FusionCacheEntry<TValue>.CreateFromOptions(_value, options, _failSafeActivated);

					if ((dca?.IsCurrentlyUsable() ?? false) && _failSafeActivated == false)
					{
						// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
						await dca.SetDistributedEntryAsync<TValue>(operationId, key, _entry, options, token).ConfigureAwait(false);
					}
				}

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				if (_entry is object)
				{
					SetMemoryEntry<TValue>(operationId, key, _entry, options);
				}
			}
			finally
			{
				ReleaseLock(operationId, key, _lockObj);
			}

			return _entry;
		}

		private FusionCacheEntry<TValue>? GetOrSetEntryInternal<TValue>(string operationId, string key, Func<CancellationToken, TValue>? factory, FusionCacheEntryOptions? options, CancellationToken token)
		{
			if (options is null)
				options = _options.DefaultEntryOptions;

			token.ThrowIfCancellationRequested();

			FusionCacheEntry<TValue>? _memoryEntry;
			bool _memoryEntryIsValid;

			// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
			(_memoryEntry, _memoryEntryIsValid) = TryGetMemoryEntry<TValue>(operationId, key);
			if (_memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry", key, operationId);
				return _memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor();

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable() ?? false) == false)
			{
				if (options.IsFailSafeEnabled && _memoryEntry is object)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry (expired)", key, operationId);
					return _memoryEntry;
				}

				return null;
			}

			FusionCacheEntry<TValue>? _entry;

			// LOCK
			var _lockObj = _reactor.AcquireLock(key, operationId, options.LockTimeout, _logger);

			try
			{
				// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
				(_memoryEntry, _memoryEntryIsValid) = TryGetMemoryEntry<TValue>(operationId, key);
				if (_memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): using memory entry", key, operationId);
					return _memoryEntry;
				}

				// TRY WITH DISTRIBUTED CACHE (IF ANY)
				FusionCacheEntry<TValue>? _distributedEntry = null;
				bool _distributedEntryIsValid = false;

				if (dca?.IsCurrentlyUsable() ?? false)
				{
					(_distributedEntry, _distributedEntryIsValid) = dca.TryGetDistributedEntry<TValue>(operationId, key, options, _memoryEntry is object, token);
				}

				if (_distributedEntryIsValid)
				{
					_entry = FusionCacheEntry<TValue>.CreateFromOptions(_distributedEntry!.Value, options, false);
				}
				else
				{
					TValue _value;
					bool _failSafeActivated = false;

					if (factory is null)
					{
						// NO FACTORY

						var _fallbackEntry = MaybeGetFallbackEntry(operationId, key, _distributedEntry, _memoryEntry, options, out _failSafeActivated);
						if (_fallbackEntry is object)
						{
							_value = _fallbackEntry.Value;
						}
						else
						{
							return null;
						}
					}
					else
					{
						// FACTORY

						Task<TValue>? factoryTask = null;

						try
						{
							var timeout = options.GetAppropriateFactoryTimeout(_memoryEntry is object || _distributedEntry is object);

							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling the factory (timeout={Timeout})", key, operationId, timeout.ToLogString_Timeout());

							_value = FusionCacheExecutionUtils.RunSyncFuncWithTimeout(ct => factory(ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token);
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception exc)
						{
							ProcessFactoryError(operationId, key, exc);

							MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, factoryTask, options, dca, token);

							var _fallbackEntry = MaybeGetFallbackEntry(operationId, key, _distributedEntry, _memoryEntry, options, out _failSafeActivated);
							if (_fallbackEntry is object)
							{
								_value = _fallbackEntry.Value;
							}
							else
							{
								throw;
							}
						}
					}

					_entry = FusionCacheEntry<TValue>.CreateFromOptions(_value, options, _failSafeActivated);

					if ((dca?.IsCurrentlyUsable() ?? false) && _failSafeActivated == false)
					{
						// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
						dca.SetDistributedEntry<TValue>(operationId, key, _entry, options);
					}
				}

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				if (_entry is object)
				{
					SetMemoryEntry<TValue>(operationId, key, _entry, options);
				}
			}
			finally
			{
				ReleaseLock(operationId, key, _lockObj);
			}

			return _entry;
		}

		/// <inheritdoc/>
		public async Task<TValue> GetOrSetAsync<TValue>(string key, Func<CancellationToken, Task<TValue>> factory, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (factory is null)
				throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling GetOrSetAsync<T> {Options}", key, operationId, options.ToLogString());

			var _entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, factory, options, token).ConfigureAwait(false);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (K={CacheKey} OP={CacheOperationId}): something went wrong, the resulting entry is null, and it should not be possible", key, operationId);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return {Entry}", key, operationId, _entry.ToLogString());
			return _entry.Value;
		}

		/// <inheritdoc/>
		public TValue GetOrSet<TValue>(string key, Func<CancellationToken, TValue> factory, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (factory is null)
				throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling GetOrSet<T> {Options}", key, operationId, options.ToLogString());

			var _entry = GetOrSetEntryInternal<TValue>(operationId, key, factory, options, token);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (K={CacheKey} OP={CacheOperationId}): something went wrong, the resulting entry is null, and it should not be possible", key, operationId);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return {Entry}", key, operationId, _entry.ToLogString());
			return _entry.Value;
		}

		/// <inheritdoc/>
		public async Task<TryGetResult<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling TryGetAsync<T> {Options}", key, operationId, options.ToLogString());

			var _entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, options, token).ConfigureAwait(false);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return NO SUCCESS", key, operationId);

				return TryGetResult<TValue>.NoSuccess;
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return SUCCESS", key, operationId);

			return TryGetResult<TValue>.CreateSuccess(_entry.Value);
		}

		/// <inheritdoc/>
		public TryGetResult<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling TryGet<T> {Options}", key, operationId, options.ToLogString());

			var _entry = GetOrSetEntryInternal<TValue>(operationId, key, null, options, token);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return NO SUCCESS", key, operationId);

				return TryGetResult<TValue>.NoSuccess;
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return SUCCESS", key, operationId);

			return TryGetResult<TValue>.CreateSuccess(_entry.Value);
		}

		/// <inheritdoc/>
		public async Task<TValue> GetOrDefaultAsync<TValue>(string key, TValue defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling GetOrDefaultAsync<T> {Options}", key, operationId, options.ToLogString());

			var _entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, options, token).ConfigureAwait(false);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return DEFAULT VALUE", key, operationId);
#pragma warning disable CS8603 // Possible null reference return.
				return defaultValue;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return {Entry}", key, operationId, _entry.ToLogString());
			return _entry.Value;
		}

		/// <inheritdoc/>
		public TValue GetOrDefault<TValue>(string key, TValue defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling GetOrDefault<T> {Options}", key, operationId, options.ToLogString());

			var _entry = GetOrSetEntryInternal<TValue>(operationId, key, null, options, token);

			if (_entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return DEFAULT VALUE", key, operationId);
#pragma warning disable CS8603 // Possible null reference return.
				return defaultValue;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): return {Entry}", key, operationId, _entry.ToLogString());
			return _entry.Value;
		}

		/// <inheritdoc/>
		public async Task SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling SetAsync<T> {Options}", key, operationId, options.ToLogString());

			var _entry = FusionCacheEntry<TValue>.CreateFromOptions(value, options, false);

			SetMemoryEntry<TValue>(operationId, key, _entry, options);

			var dca = GetCurrentDistributedAccessor();

			if ((dca?.IsCurrentlyUsable() ?? false) == false)
				return;

			await dca.SetDistributedEntryAsync<TValue>(operationId, key, _entry, options, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling Set<T> {Options}", key, operationId, options.ToLogString());

			var _entry = FusionCacheEntry<TValue>.CreateFromOptions(value, options, false);

			SetMemoryEntry<TValue>(operationId, key, _entry, options);

			var dca = GetCurrentDistributedAccessor();

			if ((dca?.IsCurrentlyUsable() ?? false) == false)
				return;

			dca.SetDistributedEntry<TValue>(operationId, key, _entry, options, token);
		}

		/// <inheritdoc/>
		public async Task RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling RemoveAsync<T> {Options}", key, operationId, options.ToLogString());

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): removing data (from memory)", key, operationId);

			_memoryCache.Remove(key);

			var dca = GetCurrentDistributedAccessor();

			// MOVE FROM HERE...

			if ((dca?.IsCurrentlyUsable() ?? false) == false)
				return;

			await dca.RemoveDistributedEntryAsync(operationId, key, options, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): calling Remove<T> {Options}", key, operationId, options.ToLogString());

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): removing data (from memory)", key, operationId);

			_memoryCache.Remove(key);

			var dca = GetCurrentDistributedAccessor();

			if ((dca?.IsCurrentlyUsable() ?? false) == false)
				return;

			dca.RemoveDistributedEntry(operationId, key, options, token);
		}

		// IDISPOSABLE
		private bool disposedValue = false;
		/// <summary>
		/// Release all resources managed by FusionCache.
		/// </summary>
		/// <param name="disposing">Indicates if the disposing is happening.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					_reactor.Dispose();
					if (_memoryCacheShouldBeDisposed)
					{
						(_memoryCache as MemoryCache)?.Compact(1);
						_memoryCache.Dispose();
					}
				}
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
				_memoryCache = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
				disposedValue = true;
			}
		}

		/// <summary>
		/// Release all resources managed by FusionCache.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

	}

}