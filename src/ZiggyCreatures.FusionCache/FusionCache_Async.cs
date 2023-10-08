using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion;

public partial class FusionCache
	: IFusionCache
{
	private async ValueTask<FusionCacheMemoryEntry?> GetOrSetEntryInternalAsync<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue?>> factory, bool isRealFactory, MaybeValue<TValue?> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		token.ThrowIfCancellationRequested();

		FusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;
		object? lockObj = null;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
		{
			(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry(operationId, key);
		}

		FusionCacheMemoryEntry? entry;
		bool isStale = false;
		var hasNewValue = false;

		if (memoryEntryIsValid)
		{
			// VALID CACHE ENTRY

			// CHECK FOR EAGER REFRESH
			if (isRealFactory && (memoryEntry!.Metadata?.ShouldEagerlyRefresh() ?? false))
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): should eagerly refresh", CacheName, operationId, key);

				// TRY TO GET THE LOCK WITHOUT WAITING, SO THAT ONLY THE FIRST ONE WILL ACTUALLY REFRESH THE ENTRY
				lockObj = await _reactor.AcquireLockAsync(CacheName, key, operationId, TimeSpan.Zero, _logger, token).ConfigureAwait(false);
				if (lockObj is null)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): eager refresh already occurring", CacheName, operationId, key);
				}
				else
				{
					// EXECUTE EAGER REFRESH
					await ExecuteEagerRefreshAsync<TValue>(operationId, key, factory, options, memoryEntry, lockObj, token);
				}
			}

			// RETURN THE ENTRY
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, operationId, key);

			// EVENT
			_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry!.Metadata?.IsFromFailSafe ?? false));

			return memoryEntry;
		}

		try
		{
			// LOCK
			lockObj = await _reactor.AcquireLockAsync(CacheName, key, operationId, options.GetAppropriateLockTimeout(memoryEntry is not null), _logger, token).ConfigureAwait(false);

			if (lockObj is null && options.IsFailSafeEnabled && memoryEntry is not null)
			{
				// IF THE LOCK HAS NOT BEEN ACQUIRED

				// + THERE IS A FALLBACK ENTRY
				// + FAIL-SAFE IS ENABLED
				// --> USE IT (WITHOUT SAVING IT, SINCE THE ALREADY RUNNING FACTORY WILL DO IT ANYWAY)

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

				return memoryEntry;
			}

			// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
			if (mca is not null)
			{
				(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry(operationId, key);
			}

			if (memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

				return memoryEntry;
			}

			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			FusionCacheDistributedEntry<TValue>? distributedEntry = null;
			bool distributedEntryIsValid = false;

			var dca = GetCurrentDistributedAccessor(options);
			if (dca.CanBeUsed(operationId, key))
			{
				if ((memoryEntry is not null && options.SkipDistributedCacheReadWhenStale) == false)
				{
					(distributedEntry, distributedEntryIsValid) = await dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, null, token).ConfigureAwait(false);
				}
			}

			DateTimeOffset? lastModified = null;
			string? etag = null;
			long? timestamp = null;

			if (distributedEntryIsValid)
			{
				isStale = false;
				entry = FusionCacheMemoryEntry.CreateFromOtherEntry<TValue>(distributedEntry!, options);
			}
			else
			{
				// FACTORY
				TValue? value;

				if (isRealFactory == false)
				{
					value = await factory(null!, token).ConfigureAwait(false);
					hasNewValue = true;
				}
				else
				{
					Task<TValue?>? factoryTask = null;

					var timeout = options.GetAppropriateFactoryTimeout(memoryEntry is not null || distributedEntry is not null);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", CacheName, operationId, key, timeout.ToLogString_Timeout());

					var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(options, distributedEntry, memoryEntry);

					try
					{
						if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
						{
							value = await factory(ctx, CancellationToken.None).ConfigureAwait(false);
						}
						else
						{
							value = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync(ct => factory(ctx, ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token).ConfigureAwait(false);
						}

						hasNewValue = true;

						// UPDATE ADAPTIVE OPTIONS
						var maybeNewOptions = ctx.GetOptions();
						if (maybeNewOptions is not null && options != maybeNewOptions)
						{
							options = maybeNewOptions;

							dca = GetCurrentDistributedAccessor(options);
							mca = GetCurrentMemoryAccessor(options);
						}

						// UPDATE LASTMODIFIED/ETAG
						lastModified = ctx.LastModified;
						etag = ctx.ETag;

						// EVENTS
						_events.OnFactorySuccess(operationId, key);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception exc)
					{
						ProcessFactoryError(operationId, key, exc);

						MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, ctx, factoryTask, options, token);

						if (TryPickFailSafeFallbackValue(operationId, key, distributedEntry, memoryEntry, failSafeDefaultValue, options, out var maybeFallbackValue, out timestamp, out isStale))
						{
							value = maybeFallbackValue.Value;
						}
						else
						{
							throw;
						}
					}
				}

				entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, isStale, lastModified, etag, timestamp, typeof(TValue));
			}

			// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
			if (entry is not null)
			{
				if (mca is not null)
				{
					mca.SetEntry<TValue>(operationId, key, entry.AsMemoryEntry<TValue>(options), options);
				}
			}
		}
		finally
		{
			if (lockObj is not null)
				ReleaseLock(operationId, key, lockObj);
		}

		// EVENT
		if (hasNewValue)
		{
			if (isStale == false)
				await DistributedSetEntryAsync<TValue>(operationId, key, entry!, options, token).ConfigureAwait(false);

			_events.OnMiss(operationId, key);
			_events.OnSet(operationId, key);
		}
		else if (entry is not null)
		{
			_events.OnHit(operationId, key, isStale || (entry?.Metadata?.IsFromFailSafe ?? false));
		}
		else
		{
			_events.OnMiss(operationId, key);
		}

		return entry;
	}

	private async Task ExecuteEagerRefreshAsync<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue?>> factory, FusionCacheEntryOptions options, FusionCacheMemoryEntry memoryEntry, object lockObj, CancellationToken token)
	{
		// TRY WITH DISTRIBUTED CACHE (IF ANY)
		try
		{
			var dca = GetCurrentDistributedAccessor(options);
			if (dca.CanBeUsed(operationId, key))
			{
				FusionCacheDistributedEntry<TValue>? distributedEntry;
				bool distributedEntryIsValid;

				(distributedEntry, distributedEntryIsValid) = await dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
				if (distributedEntryIsValid)
				{
					if ((distributedEntry?.Timestamp ?? 0) > (memoryEntry?.Timestamp ?? 0))
					{
						try
						{
							// THE DISTRIBUTED ENTRY IS MORE RECENT THAN THE MEMORY ENTRY -> USE IT
							var mca = GetCurrentMemoryAccessor(options);
							if (mca is not null)
							{
								if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
									_logger.LogTrace("FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): distributed entry found ({DistributedTimestamp}) is more recent than the current memory entry ({MemoryTimestamp}): using it", CacheName, operationId, key, distributedEntry?.Timestamp, memoryEntry?.Timestamp);

								mca.SetEntry<TValue>(operationId, key, FusionCacheMemoryEntry.CreateFromOtherEntry<TValue>(distributedEntry!, options), options);
							}
						}
						finally
						{
							ReleaseLock(operationId, key, lockObj);
						}

						return;
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.LogTrace("FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): distributed entry found ({DistributedTimestamp}) is less recent than the current memory entry ({MemoryTimestamp}): ignoring it", CacheName, operationId, key, distributedEntry?.Timestamp, memoryEntry?.Timestamp);
					}
				}
			}
		}
		catch
		{
			// EMPTY
		}

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): eagerly refreshing", CacheName, operationId, key);

		// EVENT
		_events.OnEagerRefresh(operationId, key);

		var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(options, null, memoryEntry);

		var factoryTask = factory(ctx, token);

		CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, lockObj, token);
	}

	/// <inheritdoc/>
	public async ValueTask<TValue?> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue?>> factory, MaybeValue<TValue?> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (factory is null)
			throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, factory, true, failSafeDefaultValue, options, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, operationId, key, entry.ToLogString());

		return entry.GetValue<TValue>();
	}

	/// <inheritdoc/>
	public async ValueTask<TValue?> GetOrSetAsync<TValue>(string key, TValue? defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, (_, _) => Task.FromResult(defaultValue), false, default, options, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, operationId, key, entry.ToLogString());

		return entry.GetValue<TValue>();
	}

	private async ValueTask<IFusionCacheEntry?> TryGetEntryInternalAsync<TValue>(string operationId, string key, FusionCacheEntryOptions? options, CancellationToken token)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		token.ThrowIfCancellationRequested();

		FusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
		{
			(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry(operationId, key);
		}

		if (memoryEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, operationId, key);

			// EVENT
			_events.OnHit(operationId, key, memoryEntry!.Metadata?.IsFromFailSafe ?? false);

			return memoryEntry;
		}

		var dca = GetCurrentDistributedAccessor(options);

		// SHORT-CIRCUIT: NO USABLE DISTRIBUTED CACHE
		if (options.SkipDistributedCacheReadWhenStale || dca.CanBeUsed(operationId, key) == false)
		{
			if (options.IsFailSafeEnabled && memoryEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", CacheName, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, true);

				return memoryEntry;
			}

			// EVENT
			_events.OnMiss(operationId, key);

			return null;
		}

		// TRY WITH DISTRIBUTED CACHE
		FusionCacheDistributedEntry<TValue>? distributedEntry;
		bool distributedEntryIsValid;

		(distributedEntry, distributedEntryIsValid) = await dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, null, token).ConfigureAwait(false);
		if (distributedEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using distributed entry", CacheName, operationId, key);

			memoryEntry = distributedEntry!.AsMemoryEntry<TValue>(options);

			// SAVING THE DATA IN THE MEMORY CACHE
			if (mca is not null)
			{
				mca.SetEntry<TValue>(operationId, key, memoryEntry, options);
			}

			// EVENT
			_events.OnHit(operationId, key, distributedEntry!.Metadata?.IsFromFailSafe ?? false);

			return memoryEntry;
		}

		if (options.IsFailSafeEnabled)
		{
			// FAIL-SAFE IS ENABLE -> CAN USE STALE ENTRY

			// IF DISTRIBUTED ENTRY IS THERE -> USE IT
			if (distributedEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using distributed entry (expired)", CacheName, operationId, key);

				memoryEntry = distributedEntry.AsMemoryEntry<TValue>(options);

				// SAVING THE DATA IN THE MEMORY CACHE
				if (mca is not null)
				{
					mca.SetEntry<TValue>(operationId, key, memoryEntry, options);
				}

				// EVENT
				_events.OnHit(operationId, key, true);

				return memoryEntry;
			}

			// IF MEMORY ENTRY IS THERE -> USE IT
			if (memoryEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", CacheName, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, true);

				return memoryEntry;
			}
		}

		// EVENT
		_events.OnMiss(operationId, key);

		return null;
	}

	/// <inheritdoc/>
	public async ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling TryGetAsync<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = await TryGetEntryInternalAsync<TValue>(operationId, key, options, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return NO SUCCESS", CacheName, operationId, key);

			return default;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return SUCCESS", CacheName, operationId, key);

		return entry.GetValue<TValue>();
	}

	/// <inheritdoc/>
	public async ValueTask<TValue?> GetOrDefaultAsync<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrDefaultAsync<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = await TryGetEntryInternalAsync<TValue>(operationId, key, options, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", CacheName, operationId, key);
			return defaultValue;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, operationId, key, entry.ToLogString());

		return entry.GetValue<TValue>();
	}

	/// <inheritdoc/>
	public async ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling SetAsync<T> {Options}", CacheName, operationId, key, options.ToLogString());

		// TODO: MAYBE FIND A WAY TO PASS LASTMODIFIED/ETAG HERE
		var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false, null, null, null, typeof(TValue));

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
		{
			mca.SetEntry<TValue>(operationId, key, entry, options);
		}

		await DistributedSetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);

		// EVENT
		_events.OnSet(operationId, key);
	}

	/// <inheritdoc/>
	public async ValueTask RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling RemoveAsync {Options}", CacheName, operationId, key, options.ToLogString());

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
		{
			mca.RemoveEntry(operationId, key, options);
		}

		await DistributedRemoveEntryAsync(operationId, key, options, token).ConfigureAwait(false);

		// EVENT
		_events.OnRemove(operationId, key);
	}

	/// <inheritdoc/>
	public async ValueTask ExpireAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling ExpireAsync {Options}", CacheName, operationId, key, options.ToLogString());

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
		{
			mca.ExpireEntry(operationId, key, options.IsFailSafeEnabled, null);
		}

		await DistributedExpireEntryAsync(operationId, key, options, token).ConfigureAwait(false);

		// EVENT
		_events.OnExpire(operationId, key);
	}

	private async ValueTask ExecuteDistributedActionAsync(string operationId, string key, FusionCacheAction action, Func<DistributedCacheAccessor, bool, CancellationToken, Task<bool>> distributedCacheAction, Func<BackplaneAccessor, bool, CancellationToken, Task<bool>> backplaneAction, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (RequiresDistributedOperations(options) == false)
			return;

		var mustAwaitCompletion = MustAwaitDistributedOperations(options);
		var isBackground = !mustAwaitCompletion;

		// TODO: MAYBE USE A SLIMMER UTIL
		await FusionCacheExecutionUtils.RunAsyncActionAdvancedAsync(
			async ct =>
			{
				// DISTRIBUTED CACHE
				var dca = GetCurrentDistributedAccessor(options);
				if (dca is not null)
				{
					var dcaSuccess = false;
					try
					{
						if (dca.IsCurrentlyUsable(operationId, key))
						{
							dcaSuccess = await distributedCacheAction(dca, isBackground, ct).ConfigureAwait(false);
						}
					}
					catch
					{
						//dcaSuccess = false;
						TryAddAutoRecoveryItem(operationId, key, action, options, null);
						throw;
					}

					if (dcaSuccess == false)
					{
						TryAddAutoRecoveryItem(operationId, key, action, options, null);
						return;
					}
				}

				// TODO: HANDLE BACKGROUND PROCESSING HERE... MAYBE...

				// BACKPLANE
				var bpa = GetCurrentBackplaneAccessor(options);
				if (bpa is not null)
				{
					var bpaSuccess = false;
					try
					{
						if (bpa.IsCurrentlyUsable(operationId, key))
						{
							bpaSuccess = await backplaneAction(bpa, isBackground, ct).ConfigureAwait(false);
						}
					}
					catch
					{
						bpaSuccess = false;
					}

					if (bpaSuccess == false)
					{
						TryAddAutoRecoveryItem(operationId, key, action, options, null);
					}
				}
			},
			Timeout.InfiniteTimeSpan,
			false,
			mustAwaitCompletion,
			null,
			true,
			token
		).ConfigureAwait(false);
	}

	private ValueTask DistributedSetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token)
	{
		return ExecuteDistributedActionAsync(
			operationId,
			key,
			FusionCacheAction.EntrySet,
			async (dca, isBackground, ct) =>
			{
				return await dca!.SetEntryAsync<TValue>(operationId, key, entry, options, isBackground, ct).ConfigureAwait(false);
			},
			async (bpa, isBackground, ct) =>
			{
				// TODO: MAYBE USE A BACKPLANE-SPECIFIC TIMEOUT
				return await bpa.PublishSetAsync(operationId, key, entry.Timestamp, options, false, isBackground, ct).ConfigureAwait(false);
			},
			options,
			token
		);
	}

	private ValueTask DistributedRemoveEntryAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
	{
		return ExecuteDistributedActionAsync(
			operationId,
			key,
			FusionCacheAction.EntrySet,
			async (dca, isBackground, ct) =>
			{
				return await dca.RemoveEntryAsync(operationId, key, options, isBackground, ct).ConfigureAwait(false);
			},
			async (bpa, isBackground, ct) =>
			{
				// TODO: MAYBE USE A BACKPLANE-SPECIFIC TIMEOUT
				return await bpa.PublishRemoveAsync(operationId, key, null, options, false, isBackground, ct).ConfigureAwait(false);
			},
			options,
			token
		);
	}

	private ValueTask DistributedExpireEntryAsync(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
	{
		return ExecuteDistributedActionAsync(
			operationId,
			key,
			FusionCacheAction.EntrySet,
			async (dca, isBackground, ct) =>
			{
				return await dca.RemoveEntryAsync(operationId, key, options, isBackground, ct).ConfigureAwait(false);
			},
			async (bpa, isBackground, ct) =>
			{
				// TODO: MAYBE USE A BACKPLANE-SPECIFIC TIMEOUT
				return await bpa.PublishExpireAsync(operationId, key, null, options, false, isBackground, ct).ConfigureAwait(false);
			},
			options,
			token
		);
	}
}
