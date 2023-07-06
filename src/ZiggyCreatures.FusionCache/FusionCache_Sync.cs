using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion;

public partial class FusionCache
	: IFusionCache
{
	private IFusionCacheEntry? GetOrSetEntryInternal<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue?> factory, bool isRealFactory, MaybeValue<TValue?> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
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
			(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry<TValue>(operationId, key);

		IFusionCacheEntry? entry;
		bool isStale;
		bool hasNewValue = false;

		var dca = GetCurrentDistributedAccessor(options);

		if (memoryEntryIsValid)
		{
			// VALID CACHE ENTRY

			// CHECK FOR EAGER REFRESH
			if (isRealFactory && (memoryEntry!.Metadata?.ShouldEagerlyRefresh() ?? false))
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): should eagerly refresh", CacheName, operationId, key);

				// TRY TO GET THE LOCK WITHOUT WAITING, SO THAT ONLY THE FIRST ONE WILL ACTUALLY REFRESH THE ENTRY
				lockObj = _reactor.AcquireLock(CacheName, key, operationId, TimeSpan.Zero, _logger);
				if (lockObj is null)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): eager refresh already occurring", CacheName, operationId, key);
				}
				else
				{
					// EXECUTE EAGER REFRESH
					ExecuteEagerRefresh<TValue>(operationId, key, factory, options, memoryEntry, lockObj, token);
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
			lockObj = _reactor.AcquireLock(CacheName, key, operationId, options.GetAppropriateLockTimeout(memoryEntry is not null), _logger);

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
				(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry<TValue>(operationId, key);
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

			if (dca.CanBeUsed(operationId, key))
			{
				if ((memoryEntry is not null && options.SkipDistributedCacheReadWhenStale) == false)
				{
					(distributedEntry, distributedEntryIsValid) = dca!.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, token);
				}
			}

			DateTimeOffset? lastModified = null;
			string? etag = null;

			if (distributedEntryIsValid)
			{
				isStale = false;
				entry = FusionCacheMemoryEntry.CreateFromOtherEntry<TValue>(distributedEntry!, options);
			}
			else
			{
				// FACTORY
				TValue? value;
				bool failSafeActivated = false;

				if (isRealFactory == false)
				{
					value = factory(null!, token);
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
							value = factory(ctx, CancellationToken.None);
						}
						else
						{
							value = FusionCacheExecutionUtils.RunSyncFuncWithTimeout(ct => factory(ctx, ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token);
						}

						hasNewValue = true;

						// UPDATE ADAPTIVE OPTIONS
						var maybeNewOptions = ctx.GetOptions();
						if (maybeNewOptions is not null && options != maybeNewOptions)
							options = maybeNewOptions;

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

						var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, true, out failSafeActivated);
						if (fallbackEntry is not null)
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

				entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, failSafeActivated, lastModified, etag);
				isStale = failSafeActivated;

				if (dca.CanBeUsed(operationId, key) && failSafeActivated == false)
				{
					// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
					dca!.SetEntry<TValue>(operationId, key, entry, options, token);
				}
			}

			// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
			if (entry is not null)
			{
				if (mca is not null)
					mca.SetEntry<TValue>(operationId, key, entry.AsMemoryEntry<TValue>(options), options);
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
			_events.OnMiss(operationId, key);
			_events.OnSet(operationId, key);

			// BACKPLANE
			if (options.SkipBackplaneNotifications == false)
				PublishInternal(operationId, BackplaneMessage.CreateForEntrySet(key), options, token);
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

	private void ExecuteEagerRefresh<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue?> factory, FusionCacheEntryOptions options, FusionCacheMemoryEntry memoryEntry, object lockObj, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): eagerly refreshing", CacheName, operationId, key);

		// EVENT
		_events.OnEagerRefresh(operationId, key);

		var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(options, null, memoryEntry);

		var factoryTask = Task.Run(() => factory(ctx, token));

		CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, lockObj, token);
	}

	/// <inheritdoc/>
	public TValue? GetOrSet<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue?> factory, MaybeValue<TValue?> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (factory is null)
			throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, factory, true, failSafeDefaultValue, options, token);

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
	public TValue? GetOrSet<TValue>(string key, TValue? defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, (_, _) => defaultValue, false, default, options, token);

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

	private IFusionCacheEntry? TryGetEntryInternal<TValue>(string operationId, string key, FusionCacheEntryOptions? options, CancellationToken token)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		token.ThrowIfCancellationRequested();

		FusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
			(memoryEntry, memoryEntryIsValid) = mca.TryGetEntry<TValue>(operationId, key);

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

		(distributedEntry, distributedEntryIsValid) = dca!.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, token);
		if (distributedEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): using distributed entry", CacheName, operationId, key);

			memoryEntry = distributedEntry!.AsMemoryEntry<TValue>(options);

			// SAVING THE DATA IN THE MEMORY CACHE
			if (mca is not null)
				mca.SetEntry<TValue>(operationId, key, memoryEntry, options);

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
					mca.SetEntry<TValue>(operationId, key, memoryEntry, options);

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
	public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling TryGet<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = TryGetEntryInternal<TValue>(operationId, key, options, token);

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
	public TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling GetOrDefault<T> {Options}", CacheName, operationId, key, options.ToLogString());

		var entry = TryGetEntryInternal<TValue>(operationId, key, options, token);

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
	public void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling Set<T> {Options}", CacheName, operationId, key, options.ToLogString());

		// TODO: MAYBE FIND A WAY TO PASS LASTMODIFIED/ETAG HERE
		var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false, null, null);

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
			mca.SetEntry<TValue>(operationId, key, entry, options);

		var dca = GetCurrentDistributedAccessor(options);
		if (dca.CanBeUsed(operationId, key))
		{
			dca!.SetEntry<TValue>(operationId, key, entry, options, token);
		}

		// EVENT
		_events.OnSet(operationId, key);

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
			PublishInternal(operationId, BackplaneMessage.CreateForEntrySet(key), options, token);
	}

	/// <inheritdoc/>
	public void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling Remove {Options}", CacheName, operationId, key, options.ToLogString());

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
			mca.RemoveEntry(operationId, key, options);

		var dca = GetCurrentDistributedAccessor(options);
		if (dca.CanBeUsed(operationId, key))
		{
			dca!.RemoveEntry(operationId, key, options, token);
		}

		// EVENT
		_events.OnRemove(operationId, key);

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
			PublishInternal(operationId, BackplaneMessage.CreateForEntryRemove(key), options, token);
	}

	/// <inheritdoc/>
	public void Expire(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling Expire {Options}", CacheName, operationId, key, options.ToLogString());

		var mca = GetCurrentMemoryAccessor(options);
		if (mca is not null)
			mca.ExpireEntry(operationId, key, options.IsFailSafeEnabled);

		var dca = GetCurrentDistributedAccessor(options);
		if (dca.CanBeUsed(operationId, key))
		{
			dca!.RemoveEntry(operationId, key, options, token);
		}

		// EVENT
		_events.OnExpire(operationId, key);

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
		{
			if (options.IsFailSafeEnabled)
				PublishInternal(operationId, BackplaneMessage.CreateForEntryExpire(key), options, token);
			else
				PublishInternal(operationId, BackplaneMessage.CreateForEntryRemove(key), options, token);
		}
	}

	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool PublishInternal(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (_bpa is null)
			return false;

		return _bpa.Publish(operationId, message, options, false, token);
	}
}
