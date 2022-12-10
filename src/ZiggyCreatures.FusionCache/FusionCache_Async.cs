﻿using System;
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
	private async ValueTask<IFusionCacheEntry?> GetOrSetEntryInternalAsync<TValue>(string operationId, string key, Func<FusionCacheFactoryExecutionContext, CancellationToken, Task<TValue?>>? factory, MaybeValue<TValue?> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
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
			_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

			return memoryEntry;
		}

		var dca = GetCurrentDistributedAccessor(options);

		// SHORT-CIRCUIT: NO FACTORY AND NO USABLE DISTRIBUTED CACHE
		if (factory is null && (dca?.IsCurrentlyUsable(operationId, key) ?? false) == false)
		{
			//if (failSafeDefaultValue.HasValue)
			//{
			//	// CREATE A NEW ENTRY
			//	memoryEntry = FusionCacheMemoryEntry.CreateFromOptions(failSafeDefaultValue, options, false);

			//	if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			//		_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): using the default value", operationId, key);

			//	// SAVING THE DATA IN THE MEMORY CACHE
			//	_mca.SetEntry<TValue>(operationId, key, memoryEntry, options);

			//	// EVENT
			//	_events.OnSet(operationId, key);

			//	// BACKPLANE
			//	if (options.EnableBackplaneNotifications)
			//		await PublishInternalAsync(operationId, BackplaneMessage.CreateForEntrySet(key), options, token).ConfigureAwait(false);

			//	return memoryEntry;
			//}

			if (options.IsFailSafeEnabled && memoryEntry is not null)
			{
				// CREATE A NEW (THROTTLED) ENTRY
				memoryEntry = FusionCacheMemoryEntry.CreateFromOptions(memoryEntry.Value, options, true);

				// SAVING THE DATA IN THE MEMORY CACHE (EVEN IF IT IS FROM FAIL-SAFE)
				_mca.SetEntry<TValue>(operationId, key, memoryEntry, options);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

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
		var lockObj = await _reactor.AcquireLockAsync(key, operationId, lto, _logger, token).ConfigureAwait(false);

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

		IFusionCacheEntry? entry;
		bool isStale;
		bool hasNewValue = false;

		try
		{
			// TRY AGAIN WITH MEMORY CACHE (AFTER THE LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry<TValue>(operationId, key);
			if (memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): using memory entry", operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || (memoryEntry?.Metadata?.IsFromFailSafe ?? false));

				return memoryEntry;
			}

			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			FusionCacheDistributedEntry<TValue>? distributedEntry = null;
			bool distributedEntryIsValid = false;

			if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
			{
				(distributedEntry, distributedEntryIsValid) = await dca.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, token).ConfigureAwait(false);
			}

			if (distributedEntryIsValid)
			{
				isStale = false;
				//entry = FusionCacheMemoryEntry.CreateFromOptions(distributedEntry!.Value, options, distributedEntry?.Metadata?.IsFromFailSafe ?? false);
				entry = FusionCacheMemoryEntry.CreateFromOtherEntry<TValue>(distributedEntry!, options);
			}
			else
			{
				TValue? value;
				bool failSafeActivated = false;

				if (factory is null)
				{
					// NO FACTORY

					var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, out failSafeActivated);
					if (fallbackEntry is not null)
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

					Task<TValue?>? factoryTask = null;

					var timeout = options.GetAppropriateFactoryTimeout(memoryEntry is not null || distributedEntry is not null);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", operationId, key, timeout.ToLogString_Timeout());

					var ctx = new FusionCacheFactoryExecutionContext(options);

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
							options = maybeNewOptions;
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception exc)
					{
						ProcessFactoryError(operationId, key, exc);

						MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, ctx, factoryTask, options, dca, token);

						var fallbackEntry = MaybeGetFallbackEntry(operationId, key, distributedEntry, memoryEntry, options, out failSafeActivated);
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

				entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, failSafeActivated);
				isStale = failSafeActivated;

				if ((dca?.IsCurrentlyUsable(operationId, key) ?? false) && failSafeActivated == false)
				{
					// SAVE IN THE DISTRIBUTED CACHE (BUT ONLY IF NO FAIL-SAFE HAS BEEN EXECUTED)
					await dca.SetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);
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
			if (options.EnableBackplaneNotifications)
				await PublishInternalAsync(operationId, BackplaneMessage.CreateForEntrySet(key), options, token).ConfigureAwait(false);
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
	public async ValueTask<TValue?> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext, CancellationToken, Task<TValue?>> factory, MaybeValue<TValue?> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		if (factory is null)
			throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", operationId, key, options.ToLogString());

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, factory, failSafeDefaultValue, options, token).ConfigureAwait(false);

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
	public async ValueTask<TValue?> GetOrSetAsync<TValue>(string key, TValue? defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", operationId, key, options.ToLogString());

		// TODO: MAYBE WE SHOULD AVOID ALLOCATING A LAMBDA HERE, BY CHANGING THE INTERNAL LOGIC OF THE GetOrSetEntryInternalAsync METHOD
		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, (_, _) => Task.FromResult(defaultValue), default, options, token).ConfigureAwait(false);

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
	public async ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling TryGetAsync<T> {Options}", operationId, key, options.ToLogString());

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, default, options, token).ConfigureAwait(false);

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
	public async ValueTask<TValue?> GetOrDefaultAsync<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling GetOrDefaultAsync<T> {Options}", operationId, key, options.ToLogString());

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, null, default, options, token).ConfigureAwait(false);

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
	public async ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling SetAsync<T> {Options}", operationId, key, options.ToLogString());

		var entry = FusionCacheMemoryEntry.CreateFromOptions(value, options, false);

		_mca.SetEntry<TValue>(operationId, key, entry, options);

		var dca = GetCurrentDistributedAccessor(options);

		if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
		{
			await dca.SetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);
		}

		// EVENT
		_events.OnSet(operationId, key);

		// BACKPLANE
		if (options.EnableBackplaneNotifications)
			await PublishInternalAsync(operationId, BackplaneMessage.CreateForEntrySet(key), options, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		token.ThrowIfCancellationRequested();

		if (options is null)
			options = _options.DefaultEntryOptions;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling RemoveAsync<T> {Options}", operationId, key, options.ToLogString());

		_mca.RemoveEntry(operationId, key, options);

		var dca = GetCurrentDistributedAccessor(options);

		if (dca?.IsCurrentlyUsable(operationId, key) ?? false)
		{
			await dca.RemoveEntryAsync(operationId, key, options, token).ConfigureAwait(false);
		}

		// EVENT
		_events.OnRemove(operationId, key);

		// BACKPLANE
		if (options.EnableBackplaneNotifications)
			await PublishInternalAsync(operationId, BackplaneMessage.CreateForEntryRemove(key), options, token).ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async ValueTask<bool> PublishInternalAsync(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (_bpa is null)
			return false;

		return await _bpa.PublishAsync(operationId, message, options, false, token);
	}

	/// <inheritdoc/>
	internal ValueTask<bool> PublishAsync(BackplaneMessage message, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		if (options is null)
			options = _options.DefaultEntryOptions;

		return PublishInternalAsync(GenerateOperationId(), message, options, token);
	}
}
