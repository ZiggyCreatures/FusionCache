using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion
{
	public partial class FusionCache
		: IFusionCache
	{
		private IFusionCacheEntry? GetOrSetEntryInternal<TValue>(string operationId, string key, Func<CancellationToken, TValue>? factory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
		{
			if (options is null)
				options = _options.DefaultEntryOptions;

			token.ThrowIfCancellationRequested();

			FusionCacheMemoryEntry? memoryEntry;
			bool memoryEntryIsValid;

			// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
			if (memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false);

				return memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor();

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable(operationId, key) ?? false) == false)
			{
				if (options.IsFailSafeEnabled && memoryEntry is object)
				{
					// CREATE A NEW (THROTTLED) ENTRY
					memoryEntry = FusionCacheMemoryEntry.CreateFromOptions(memoryEntry.Value, options, true);

					// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
					_mca.SetEntry<TValue>(operationId, key, memoryEntry, options);

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, memoryEntryIsValid == false);

					return memoryEntry;
				}

				// EVENT
				_events.OnMiss(operationId, key);

				return null;
			}

			IFusionCacheEntry? entry;

			// LOCK
			var lto = options.LockTimeout;
			if (lto == Timeout.InfiniteTimeSpan && memoryEntry is object && options.IsFailSafeEnabled && options.FactorySoftTimeout != Timeout.InfiniteTimeSpan)
			{
				// IF THERE IS NO SPECIFIC LOCK TIMEOUT
				// + THERE IS A FALLBACK ENTRY
				// + FAILSAFE IS ENABLED
				// + THERE IS A FACTORY SOFT TIMEOUT
				// --> USE IT AS A LOCK TIMEOUT
				lto = options.FactorySoftTimeout;
			}
			var lockObj = _reactor.AcquireLock(key, operationId, options.LockTimeout, _logger);

			if (lockObj is null && memoryEntry is object)
			{
				// IF THE LOCK HAS NOT BEEN ACQUIRED
				// + THERE IS A FALLBACK ENTRY
				// --> USE IT (WITHOUT SAVING IT, SINCE THE ALREADY RUNNING FACTORY WILL DO IT ANYWAY)

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false);

				return memoryEntry;
			}

			bool isStale;
			bool factoryCompletedSuccessfully = false;

			try
			{
				// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
				(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
				if (memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, memoryEntryIsValid == false);

					return memoryEntry;
				}

				// TRY WITH DISTRIBUTED CACHE (IF ANY)
				FusionCacheDistributedEntry<TValue>? distributedEntry = null;
				bool distributedEntryIsValid = false;

				if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
				{
					(distributedEntry, distributedEntryIsValid) = dca.TryGetEntry<TValue>(operationId, key, options, memoryEntry is object, token);
				}

				if (distributedEntryIsValid)
				{
					isStale = false;
					entry = FusionCacheMemoryEntry.CreateFromOptions(distributedEntry!.Value, options, false);
				}
				else
				{
					TValue value;
					bool failSafeActivated = false;

					if (factory is null)
					{
						// NO FACTORY

						var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, out failSafeActivated);
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

						Task<TValue>? factoryTask = null;

						try
						{
							var timeout = options.GetAppropriateFactoryTimeout(memoryEntry is object || distributedEntry is object);

							if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
								_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", operationId, key, timeout.ToLogString_Timeout());

							if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
							{
								value = factory(CancellationToken.None);
							}
							else
							{
								value = FusionCacheExecutionUtils.RunSyncFuncWithTimeout(ct => factory(ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token);
							}
							factoryCompletedSuccessfully = true;
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception exc)
						{
							ProcessFactoryError(operationId, key, exc);

							MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, factoryTask, options, dca, token);

							var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, out failSafeActivated);
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

					entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, failSafeActivated);
					isStale = failSafeActivated;

					if ((dca?.IsCurrentlyUsable(operationId, key) ?? false) && failSafeActivated == false)
					{
						// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
						dca.SetEntry<TValue>(operationId, key, entry, options);
					}
				}

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				if (entry is object)
				{
					_mca.SetEntry<TValue>(operationId, key, entry.AsMemoryEntry(), options);
				}
			}
			finally
			{
				if (lockObj is object)
					ReleaseLock(operationId, key, lockObj);
			}

			// EVENT
			if (factoryCompletedSuccessfully)
			{
				_events.OnSet(operationId, key);
			}
			else if (entry is object)
			{
				_events.OnHit(operationId, key, isStale);
			}
			else
			{
				_events.OnMiss(operationId, key);
			}

			return entry;
		}

		/// <inheritdoc/>
		public TValue GetOrSet<TValue>(string key, Func<CancellationToken, TValue> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			if (factory is null)
				throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, factory, failSafeDefaultValue, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public TValue GetOrSet<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

			// TODO: MAYBE WE SHOULD AVOID ALLOCATING A LAMBDA HERE, BY CHANGING THE INTERNAL LOGIC OF THE GetOrSetEntryInternal METHOD
			var entry = GetOrSetEntryInternal<TValue>(operationId, key, _ => defaultValue, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError("FUSION (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
				throw new InvalidOperationException("The resulting fusion cache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling TryGet<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return NO SUCCESS", operationId, key);

				return default;
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return SUCCESS", operationId, key);

			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public TValue GetOrDefault<TValue>(string key, TValue defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrDefault<T> {Options}", operationId, key, options.ToLogString());

			var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, default, options, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", operationId, key);
#pragma warning disable CS8603 // Possible null reference return.
				return defaultValue;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());
			return entry.GetValue<TValue>();
		}

		/// <inheritdoc/>
		public void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling Set<T> {Options}", operationId, key, options.ToLogString());

			var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false);

			_mca.SetEntry<TValue>(operationId, key, entry, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
			{
				dca.SetEntry<TValue>(operationId, key, entry, options, token);
			}

			// EVENT
			_events.OnSet(operationId, key);
		}

		/// <inheritdoc/>
		public void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
		{
			ValidateCacheKey(key);

			token.ThrowIfCancellationRequested();

			if (options is null)
				options = _options.DefaultEntryOptions;

			var operationId = GenerateOperationId();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling Remove<T> {Options}", operationId, key, options.ToLogString());

			_mca.RemoveEntry(operationId, key, options);

			var dca = GetCurrentDistributedAccessor();

			if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
			{
				dca.RemoveEntry(operationId, key, options, token);
			}

			// EVENT
			_events.OnRemove(operationId, key);
		}
	}
}
