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
	private IFusionCacheEntry? GetOrSetEntryInternal<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue?>? factory, bool isRealFactory, MaybeValue<TValue?> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		token.ThrowIfCancellationRequested();

		FusionCacheMemoryEntry? memoryEntry;
		bool memoryEntryIsValid;
		object? lockObj = null;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);

		IFusionCacheEntry? entry;
		bool isStale;
		bool hasNewValue = false;
		bool isEagerlyRefreshing = false;

		try
		{
			// CHECK FOR EAGER REFRESH
			if (factory is not null && memoryEntryIsValid && (memoryEntry!.Metadata?.ShouldEagerlyRefresh() ?? false))
			{
				// EAGERLY REFRESH: TRY TO GET THE LOCK WITHOUT WAITING, SO THAT ONLY THE FIRST ONE WILL ACTUALLY REFRESH THE ENTRY
				lockObj = _reactor.AcquireLock(key, operationId, TimeSpan.Zero, _logger);
				if (lockObj is not null)
				{
					isEagerlyRefreshing = true;
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): eagerly refreshing", operationId, key);
				}
			}

			if (memoryEntryIsValid && lockObj is null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry!.Metadata?.IsFromFailSafe ?? false));

				return memoryEntry;
			}

			var dca = GetCurrentDistributedAccessor(options);

			// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
			if (factory is null && (dca?.IsCurrentlyUsable(operationId, key) ?? false) == false)
			{
				if (options.IsFailSafeEnabled && memoryEntry is not null)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, true);

					return memoryEntry;
				}

				// EVENT
				_events.OnMiss(operationId, key);

				return null;
			}

			// LOCK
			var lto = options.LockTimeout;
			if (lto == Timeout.InfiniteTimeSpan && memoryEntry is not null && options.IsFailSafeEnabled && options.FactorySoftTimeout != Timeout.InfiniteTimeSpan)
			{
				// IF THERE IS NO SPECIFIC LOCK TIMEOUT
				// + THERE IS A FALLBACK ENTRY
				// + FAIL-SAFE IS ENABLED
				// + THERE IS A FACTORY SOFT TIMEOUT
				// --> USE IT AS A LOCK TIMEOUT
				lto = options.FactorySoftTimeout;
			}

			if (lockObj is null)
				lockObj = _reactor.AcquireLock(key, operationId, lto, _logger);

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
			if (isEagerlyRefreshing == false)
			{
				(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
				if (memoryEntryIsValid)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

					// EVENT
					_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

					return memoryEntry;
				}
			}

			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			FusionCacheDistributedEntry<TValue>? distributedEntry = null;
			bool distributedEntryIsValid = false;

			if (isEagerlyRefreshing == false && (dca?.IsCurrentlyUsable(operationId, key) ?? false))
			{
				if ((memoryEntry is not null && options.SkipDistributedCacheReadWhenStale) == false)
				{
					(distributedEntry, distributedEntryIsValid) = dca.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, token);
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
				TValue? value;
				bool failSafeActivated = false;

				if (factory is null)
				{
					// NO FACTORY

					var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, false, out failSafeActivated);
					if (fallbackEntry is not null)
					{
						// EVENT
						_events.OnHit(operationId, key, true);

						return fallbackEntry;
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

					Task<TValue?>? factoryTask = null;

					var timeout = options.GetAppropriateFactoryTimeout(memoryEntry is not null || distributedEntry is not null);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", operationId, key, timeout.ToLogString_Timeout());

					MaybeValue<TValue> staleValue;

					if (distributedEntry is not null)
					{
						staleValue = MaybeValue<TValue>.FromValue(distributedEntry.GetValue<TValue>());
						lastModified = distributedEntry.Metadata?.LastModified;
						etag = distributedEntry.Metadata?.ETag;
					}
					else if (memoryEntry is not null)
					{
						staleValue = MaybeValue<TValue>.FromValue(memoryEntry.GetValue<TValue>());
						lastModified = memoryEntry.Metadata?.LastModified;
						etag = memoryEntry.Metadata?.ETag;
					}
					else
					{
						staleValue = default;
					}

					var ctx = new FusionCacheFactoryExecutionContext<TValue>(options, lastModified, etag, staleValue);

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
						if (isRealFactory)
							_events.OnFactorySuccess(operationId, key);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception exc)
					{
						ProcessFactoryError(operationId, key, exc);

						MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, ctx, factoryTask, options, dca, token);

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

				if ((dca?.IsCurrentlyUsable(operationId, key) ?? false) && failSafeActivated == false)
				{
					// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
					dca.SetEntry<TValue>(operationId, key, entry, options, token);
				}
			}

			// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
			if (entry is not null)
			{
				_mca.SetEntry<TValue>(operationId, key, entry.AsMemoryEntry(options), options);
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
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, factory, true, failSafeDefaultValue, options, token);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.LogError("FUSION (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());

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
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSet<T> {Options}", operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, (_, _) => defaultValue, false, default, options, token);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.LogError("FUSION (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());

		return entry.GetValue<TValue>();
	}

	/// <inheritdoc/>
	public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling TryGet<T> {Options}", operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, false, default, options, token);

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
	public TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrDefault<T> {Options}", operationId, key, options.ToLogString());

		var entry = GetOrSetEntryInternal<TValue>(operationId, key, null, false, default, options, token);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", operationId, key);
			return defaultValue;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): return {Entry}", operationId, key, entry.ToLogString());

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
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling Set<T> {Options}", operationId, key, options.ToLogString());

		// TODO: MAYBE FIND A WAY TO PASS LASTMODIFIED/ETAG HERE
		var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false, null, null);

		_mca.SetEntry<TValue>(operationId, key, entry, options);

		var dca = GetCurrentDistributedAccessor(options);

		if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
		{
			dca.SetEntry<TValue>(operationId, key, entry, options, token);
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
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling Remove<T> {Options}", operationId, key, options.ToLogString());

		_mca.RemoveEntry(operationId, key, options);

		var dca = GetCurrentDistributedAccessor(options);

		if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
		{
			dca.RemoveEntry(operationId, key, options, token);
		}

		// EVENT
		_events.OnRemove(operationId, key);

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
			PublishInternal(operationId, BackplaneMessage.CreateForEntryRemove(key), options, token);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool PublishInternal(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (_bpa is null)
			return false;

		return _bpa.Publish(operationId, message, options, false, token);
	}

	/// <inheritdoc/>
	internal bool Publish(BackplaneMessage message, FusionCacheEntryOptions? options, CancellationToken token)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		return PublishInternal(GenerateOperationId(), message, options, token);
	}
}
