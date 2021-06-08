using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;
using ZiggyCreatures.Caching.Fusion.Reactors;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion
{
	/// <inheritdoc/>
	public class FusionCache
		: IFusionCache
	{
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly IFusionCacheReactor _reactor;
		private MemoryCacheAccessor _mca;
		private DistributedCacheAccessor? _dca;
		private FusionCacheEventsHub _events;

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

			// EVENTS
			_events = new FusionCacheEventsHub(this, _options, _logger);

			// MEMORY CACHE
			_mca = new MemoryCacheAccessor(memoryCache, _options, _logger, _events.Memory);

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

			_dca = new DistributedCacheAccessor(distributedCache, serializer, _options, _logger, _events.Distributed);

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
		public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
		{
			var res = _options.DefaultEntryOptions.Duplicate(duration);
			setupAction?.Invoke(res);
			return res;
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

		private IFusionCacheEntry? MaybeGetFallbackEntry<TValue>(string operationId, string key, FusionCacheDistributedEntry<TValue>? distributedEntry, FusionCacheMemoryEntry? memoryEntry, FusionCacheEntryOptions options, out bool failSafeActivated)
		{
			failSafeActivated = false;

			if (options.IsFailSafeEnabled)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): trying to activate FAIL-SAFE", operationId, key);
				if (distributedEntry is object)
				{
					// FAIL SAFE (FROM DISTRIBUTED)
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (OP={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from distributed)", operationId, key);
					failSafeActivated = true;

					// EVENT
					_events.OnFailSafeActivate(operationId, key);

					return distributedEntry;
				}
				else if (memoryEntry is object)
				{
					// FAIL SAFE (FROM MEMORY)
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (OP={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from memory)", operationId, key);
					failSafeActivated = true;

					// EVENT
					_events.OnFailSafeActivate(operationId, key);

					return memoryEntry;
				}
				else
				{
					if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
						_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (OP={CacheOperationId} K={CacheKey}): unable to activate FAIL-SAFE (no entries in memory or distributed)", operationId, key);
					return null;
				}
			}
			else
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): FAIL-SAFE not enabled", operationId, key);
				return null;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void MaybeBackgroundCompleteTimedOutFactory<TValue>(string operationId, string key, Task<TValue>? factoryTask, FusionCacheEntryOptions options, DistributedCacheAccessor? dca, CancellationToken token)
		{
			if (options.AllowTimedOutFactoryBackgroundCompletion == false || factoryTask is null)
				return;

			if (factoryTask.IsFaulted)
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, factoryTask.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (OP={CacheOperationId} K={CacheKey}): a timed-out factory thrown an exception", operationId, key);
				return;
			}

			// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): trying to complete the timed-out factory in the background", operationId, key);

			_ = factoryTask.ContinueWith(antecedent =>
			{
				if (antecedent.Status == TaskStatus.Faulted)
				{
					if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
						_logger.Log(_options.FactoryErrorsLogLevel, antecedent.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (OP={CacheOperationId} K={CacheKey}): a timed-out factory thrown an exception", operationId, key);

					// EVENT
					_events.OnBackgroundFactoryError(operationId, key);
				}
				else if (antecedent.Status == TaskStatus.RanToCompletion)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): a timed-out factory successfully completed in the background: keeping the result", operationId, key);

					var lateEntry = FusionCacheMemoryEntry.CreateFromOptions(antecedent.Result, options, false);
					_ = dca?.SetEntryAsync<TValue>(operationId, key, lateEntry, options, token);
					_mca.SetEntry<TValue>(operationId, key, lateEntry, options);

					// EVENT
					_events.OnBackgroundFactorySuccess(operationId, key);
				}
			});
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReleaseLock(string operationId, string key, object? lockObj)
		{
			if (lockObj is null)
				return;

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): releasing LOCK", operationId, key);

			try
			{
				_reactor.ReleaseLock(key, operationId, lockObj, _logger);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): LOCK released", operationId, key);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning(exc, "FUSION (OP={CacheOperationId} K={CacheKey}): releasing the LOCK has thrown an exception", operationId, key);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessFactoryError(string operationId, string key, Exception exc)
		{
			if (exc is SyntheticTimeoutException)
			{
				if (_logger?.IsEnabled(_options.FactorySyntheticTimeoutsLogLevel) ?? false)
					_logger.Log(_options.FactorySyntheticTimeoutsLogLevel, exc, "FUSION (OP={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while calling the factory", operationId, key);

				// EVENT
				_events.OnFactorySyntheticTimeout(operationId, key);

				return;
			}

			if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
				_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION (OP={CacheOperationId} K={CacheKey}): an error occurred while calling the factory", operationId, key);

			// EVENT
			_events.OnFactoryError(operationId, key);
		}

		private async Task<IFusionCacheEntry?> GetOrSetEntryInternalAsync<TValue>(string operationId, string key, Func<CancellationToken, Task<TValue>>? factory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
		{
			if (options is null)
				options = _options.DefaultEntryOptions;

			token.ThrowIfCancellationRequested();

			FusionCacheMemoryEntry? _memoryEntry;
			bool _memoryEntryIsValid;

			// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
			(_memoryEntry, _memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
			if (_memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, _memoryEntryIsValid == false);

				return _memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor();

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable() ?? false) == false)
			{
				if (options.IsFailSafeEnabled && _memoryEntry is object)
				{
					// CREATE A NEW (THROTTLED) ENTRY
					_memoryEntry = FusionCacheMemoryEntry.CreateFromOptions(_memoryEntry.Value, options, true);

					// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
					_mca.SetEntry<TValue>(operationId, key, _memoryEntry, options);

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry (expired)", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, _memoryEntryIsValid == false);

					return _memoryEntry;
				}

				// EVENT
				_events.OnMiss(operationId, key);

				return null;
			}

			IFusionCacheEntry? _entry;

			// LOCK
			var lockObj = await _reactor.AcquireLockAsync(key, operationId, options.LockTimeout, _logger, token).ConfigureAwait(false);
			bool isStale;

			try
			{
				// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
				(_memoryEntry, _memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
				if (_memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, _memoryEntryIsValid == false);

					return _memoryEntry;
				}

				// TRY WITH DISTRIBUTED CACHE (IF ANY)
				FusionCacheDistributedEntry<TValue>? distributedEntry = null;
				bool distributedEntryIsValid = false;

				if (dca?.IsCurrentlyUsable() ?? false)
				{
					(distributedEntry, distributedEntryIsValid) = await dca.TryGetEntryAsync<TValue>(operationId, key, options, _memoryEntry is object, token).ConfigureAwait(false);
				}

				if (distributedEntryIsValid)
				{
					isStale = false;
					_entry = FusionCacheMemoryEntry.CreateFromOptions(distributedEntry!.Value, options, false);
				}
				else
				{
					TValue value;
					bool failSafeActivated = false;

					if (factory is null)
					{
						// NO FACTORY

						var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, _memoryEntry, options, out failSafeActivated);
						if (fallbackEntry is object)
						{
							value = fallbackEntry.GetValue<TValue>();
						}
						else
						{
							// EVENT
							_events.OnMiss(operationId, key);

							return null;
						}
					}
					else
					{
						// FACTORY

						// EVENT
						if (_memoryEntry is object || distributedEntry is object)
							_events.OnHit(operationId, key, true);
						else
							_events.OnMiss(operationId, key);

						Task<TValue>? factoryTask = null;

						try
						{
							var timeout = options.GetAppropriateFactoryTimeout(_memoryEntry is object || distributedEntry is object);

							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", operationId, key, timeout.ToLogString_Timeout());

							if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
							{
								value = await factory(CancellationToken.None).ConfigureAwait(false);
							}
							else
							{
								value = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync(ct => factory(ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token).ConfigureAwait(false);
							}
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception exc)
						{
							ProcessFactoryError(operationId, key, exc);

							MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, factoryTask, options, dca, token);

							var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, _memoryEntry, options, out failSafeActivated);
							if (fallbackEntry is object)
							{
								value = fallbackEntry.GetValue<TValue>();
							}
							else if (options.IsFailSafeEnabled && failSafeDefaultValue.HasValue)
							{
								failSafeActivated = true;
								value = failSafeDefaultValue;
							}
							else
							{
								throw;
							}
						}
					}

					_entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, failSafeActivated);
					isStale = failSafeActivated;

					if ((dca?.IsCurrentlyUsable() ?? false) && failSafeActivated == false)
					{
						// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
						await dca.SetEntryAsync<TValue>(operationId, key, _entry, options, token).ConfigureAwait(false);
					}
				}

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				if (_entry is object)
				{
					_mca.SetEntry<TValue>(operationId, key, _entry.AsMemoryEntry(), options);
				}
			}
			finally
			{
				if (lockObj is object)
					ReleaseLock(operationId, key, lockObj);
			}

			// EVENT
			if (_entry is object)
			{
				_events.OnSet(operationId, key);
			}
			else
			{
				_events.OnMiss(operationId, key);
			}

			return _entry;
		}

		private IFusionCacheEntry? GetOrSetEntryInternal<TValue>(string operationId, string key, Func<CancellationToken, TValue>? factory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
		{
			if (options is null)
				options = _options.DefaultEntryOptions;

			token.ThrowIfCancellationRequested();

			FusionCacheMemoryEntry? _memoryEntry;
			bool _memoryEntryIsValid;

			// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
			(_memoryEntry, _memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
			if (_memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, _memoryEntryIsValid == false);

				return _memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor();

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable() ?? false) == false)
			{
				if (options.IsFailSafeEnabled && _memoryEntry is object)
				{
					// CREATE A NEW (THROTTLED) ENTRY
					_memoryEntry = FusionCacheMemoryEntry.CreateFromOptions(_memoryEntry.Value, options, true);

					// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
					_mca.SetEntry<TValue>(operationId, key, _memoryEntry, options);

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry (expired)", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, _memoryEntryIsValid == false);

					return _memoryEntry;
				}

				// EVENT
				_events.OnMiss(operationId, key);

				return null;
			}

			IFusionCacheEntry? _entry;

			// LOCK
			var lockObj = _reactor.AcquireLock(key, operationId, options.LockTimeout, _logger);
			bool isStale;

			try
			{
				// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
				(_memoryEntry, _memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
				if (_memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (OP={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, _memoryEntryIsValid == false);

					return _memoryEntry;
				}

				// TRY WITH DISTRIBUTED CACHE (IF ANY)
				FusionCacheDistributedEntry<TValue>? distributedEntry = null;
				bool distributedEntryIsValid = false;

				if (dca?.IsCurrentlyUsable() ?? false)
				{
					(distributedEntry, distributedEntryIsValid) = dca.TryGetEntry<TValue>(operationId, key, options, _memoryEntry is object, token);
				}

				if (distributedEntryIsValid)
				{
					isStale = false;
					_entry = FusionCacheMemoryEntry.CreateFromOptions(distributedEntry!.Value, options, false);
				}
				else
				{
					TValue value;
					bool failSafeActivated = false;

					if (factory is null)
					{
						// NO FACTORY

						var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, _memoryEntry, options, out failSafeActivated);
						if (fallbackEntry is object)
						{
							value = fallbackEntry.GetValue<TValue>();
						}
						else
						{
							// EVENT
							_events.OnMiss(operationId, key);

							return null;
						}
					}
					else
					{
						// FACTORY

						// EVENT
						if (_memoryEntry is object || distributedEntry is object)
							_events.OnHit(operationId, key, true);
						else
							_events.OnMiss(operationId, key);

						Task<TValue>? factoryTask = null;

						try
						{
							var timeout = options.GetAppropriateFactoryTimeout(_memoryEntry is object || distributedEntry is object);

							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", operationId, key, timeout.ToLogString_Timeout());

							if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
							{
								value = factory(CancellationToken.None);
							}
							else
							{
								value = FusionCacheExecutionUtils.RunSyncFuncWithTimeout(ct => factory(ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token);
							}
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception exc)
						{
							ProcessFactoryError(operationId, key, exc);

							MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, factoryTask, options, dca, token);

							var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, _memoryEntry, options, out failSafeActivated);
							if (fallbackEntry is object)
							{
								value = fallbackEntry.GetValue<TValue>();
							}
							else if (options.IsFailSafeEnabled && failSafeDefaultValue.HasValue)
							{
								failSafeActivated = true;
								value = failSafeDefaultValue;
							}
							else
							{
								throw;
							}
						}
					}

					_entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, failSafeActivated);
					isStale = failSafeActivated;

					if ((dca?.IsCurrentlyUsable() ?? false) && failSafeActivated == false)
					{
						// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
						dca.SetEntry<TValue>(operationId, key, _entry, options);
					}
				}

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				if (_entry is object)
				{
					_mca.SetEntry<TValue>(operationId, key, _entry.AsMemoryEntry(), options);
				}
			}
			finally
			{
				if (lockObj is object)
					ReleaseLock(operationId, key, lockObj);
			}

			// EVENT
			if (_entry is object)
			{
				_events.OnSet(operationId, key);
			}
			else
			{
				_events.OnMiss(operationId, key);
			}

			return _entry;
		}

		/// <inheritdoc/>
		public async Task<TValue> GetOrSetAsync<TValue>(string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (factory is null)
				throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", operationId, key, options.ToLogString());

			var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, factory, failSafeDefaultValue, options, token).ConfigureAwait(false);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (OP={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public TValue GetOrSet<TValue>(string key, Func<CancellationToken, TValue> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			if (factory is null)
				throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, factory, failSafeDefaultValue, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (OP={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public async Task<TValue> GetOrSetAsync<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", operationId, key, options.ToLogString());

			// TODO: MAYBE WE SHOULD AVOID ALLOCATING A LAMBDA HERE, BY CHANGING THE INTERNAL LOGIC OF THE GetOrSetEntryInternalAsync METHOD
			var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, _ => Task.FromResult(defaultValue), default, options, token).ConfigureAwait(false);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (OP={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public TValue GetOrSet<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

			// TODO: MAYBE WE SHOULD AVOID ALLOCATING A LAMBDA HERE, BY CHANGING THE INTERNAL LOGIC OF THE GetOrSetEntryInternal METHOD
			var entry = GetOrSetEntryInternal<TValue>(operationId, key, _ => defaultValue, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (OP={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public async Task<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling TryGetAsync<T> {Options}", operationId, key, options.ToLogString());

			var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, default, options, token).ConfigureAwait(false);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return NO SUCCESS", operationId, key);

				return default;
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return SUCCESS", operationId, key);

			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling TryGet<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return NO SUCCESS", operationId, key);

				return default;
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return SUCCESS", operationId, key);

			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public async Task<TValue> GetOrDefaultAsync<TValue>(string key, TValue defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrDefaultAsync<T> {Options}", operationId, key, options.ToLogString());

			var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, default, options, token).ConfigureAwait(false);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", operationId, key);
#pragma warning disable CS8603 // Possible null reference return.
				return defaultValue;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public TValue GetOrDefault<TValue>(string key, TValue defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			MaybeProcessCacheKey(ref key);

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling GetOrDefault<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", operationId, key);
#pragma warning disable CS8603 // Possible null reference return.
				return defaultValue;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
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
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling SetAsync<T> {Options}", operationId, key, options.ToLogString());

			var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false);

			_mca.SetEntry<TValue>(operationId, key, entry, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable() ?? false)
			{
				await dca.SetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);
			}

			// EVENT
			_events.OnSet(operationId, key);
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
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling Set<T> {Options}", operationId, key, options.ToLogString());

			var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false);

			_mca.SetEntry<TValue>(operationId, key, entry, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable() ?? false)
			{
				dca.SetEntry<TValue>(operationId, key, entry, options, token);
			}

			// EVENT
			_events.OnSet(operationId, key);
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
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling RemoveAsync<T> {Options}", operationId, key, options.ToLogString());

			_mca.RemoveEntry(operationId, key, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable() ?? false)
			{
				await dca.RemoveEntryAsync(operationId, key, options, token).ConfigureAwait(false);
			}

			// EVENT
			_events.OnRemove(operationId, key);
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
				_logger.LogDebug("FUSION (OP={CacheOperationId} K={CacheKey}): calling RemoveAsync<T> {Options}", operationId, key, options.ToLogString());

			_mca.RemoveEntry(operationId, key, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable() ?? false)
			{
				dca.RemoveEntry(operationId, key, options, token);
			}

			// EVENT
			_events.OnRemove(operationId, key);
		}

		/// <inheritdoc/>
		public FusionCacheEventsHub Events { get { return _events; } }

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
					_mca.Dispose();
				}
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
				_mca = null;
				_events = null;
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
