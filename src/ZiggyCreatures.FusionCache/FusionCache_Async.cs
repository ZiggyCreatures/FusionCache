using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion;

public partial class FusionCache
{
	// GET OR SET

	private void ExecuteEagerRefreshWithAsyncFactory<TValue>(string operationId, string key, string[]? tags, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory, FusionCacheEntryOptions options, IFusionCacheMemoryEntry memoryEntry, object memoryLockObj)
	{
		// EVENT
		_events.OnEagerRefresh(operationId, key);

		_ = Task.Run(async () =>
		{
			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			try
			{
				var dca = DistributedCache;
				if (dca.ShouldRead(options) && dca.CanBeUsed(operationId, key))
				{
					FusionCacheDistributedEntry<TValue>? distributedEntry;
					bool distributedEntryIsValid;

					(distributedEntry, distributedEntryIsValid) = await dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, Timeout.InfiniteTimeSpan, default).ConfigureAwait(false);
					if (distributedEntryIsValid)
					{
						if ((distributedEntry?.Timestamp ?? 0) > (memoryEntry?.Timestamp ?? 0))
						{
							try
							{
								// THE DISTRIBUTED ENTRY IS MORE RECENT THAN THE MEMORY ENTRY -> USE IT
								if (_mca.ShouldWrite(options))
								{
									if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
										_logger.LogTrace("FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): distributed entry found ({DistributedTimestamp}) is more recent than the current memory entry ({MemoryTimestamp}): using it", CacheName, InstanceId, operationId, key, distributedEntry?.Timestamp, memoryEntry?.Timestamp);

									_mca.SetEntry<TValue>(operationId, key, FusionCacheMemoryEntry<TValue>.CreateFromOtherEntry(distributedEntry!, options), options);
								}
							}
							finally
							{
								// MEMORY LOCK
								if (memoryLockObj is not null)
									ReleaseMemoryLock(operationId, key, memoryLockObj);
							}

							return;
						}
						else
						{
							if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
								_logger.LogTrace("FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): distributed entry found ({DistributedTimestamp}) is less recent than the current memory entry ({MemoryTimestamp}): ignoring it", CacheName, InstanceId, operationId, key, distributedEntry?.Timestamp, memoryEntry?.Timestamp);
						}
					}
				}
			}
			catch
			{
				// EMPTY
			}

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): eagerly refreshing", CacheName, InstanceId, operationId, key);

			// ACTIVITY
			var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.ExecuteFactory, CacheName, InstanceId, key, operationId);
			activity?.SetTag(Tags.Names.FactoryEagerRefresh, true);

			var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(options, null, memoryEntry, tags);

			var factoryTask = factory(ctx, default);

			CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, memoryLockObj, activity);
		});
	}

	private async ValueTask<IFusionCacheMemoryEntry?> GetOrSetEntryInternalAsync<TValue>(string operationId, string key, string[]? tags, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory, bool isRealFactory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options, Activity? activity, CancellationToken token)
	{
		options ??= _options.DefaultEntryOptions;

		IFusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;
		object? memoryLockObj = null;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		if (_mca.ShouldRead(options))
		{
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);
		}

		// TAGGING
		if (memoryEntryIsValid)
		{
			(memoryEntry, memoryEntryIsValid) = await CheckEntrySecondaryExpirationAsync(operationId, key, memoryEntry, false, token).ConfigureAwait(false);
		}

		if (memoryEntryIsValid)
		{
			// VALID CACHE ENTRY

			// CHECK FOR EAGER REFRESH
			if (isRealFactory && (memoryEntry!.Metadata?.ShouldEagerlyRefresh() ?? false))
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): should eagerly refresh", CacheName, InstanceId, operationId, key);

				// TRY TO GET THE MEMORY LOCK WITHOUT WAITING, SO THAT ONLY THE FIRST ONE WILL ACTUALLY REFRESH THE ENTRY
				memoryLockObj = await AcquireMemoryLockAsync(operationId, key, TimeSpan.Zero, token).ConfigureAwait(false);
				if (memoryLockObj is null)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): eager refresh already occurring", CacheName, InstanceId, operationId, key);
				}
				else
				{
					// EXECUTE EAGER REFRESH
					ExecuteEagerRefreshWithAsyncFactory<TValue>(operationId, key, tags, factory, options, memoryEntry, memoryLockObj);
				}
			}

			// RETURN THE ENTRY
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, InstanceId, operationId, key);

			// EVENT
			_events.OnHit(operationId, key, memoryEntryIsValid == false || memoryEntry!.IsStale(), activity);

			return memoryEntry;
		}

		IFusionCacheMemoryEntry? entry;
		bool isStale = false;
		var hasNewValue = false;

		try
		{
			// MEMORY LOCK
			memoryLockObj = await AcquireMemoryLockAsync(operationId, key, options.GetAppropriateMemoryLockTimeout(memoryEntry is not null), token).ConfigureAwait(false);

			if (memoryLockObj is null && options.IsFailSafeEnabled && memoryEntry is not null)
			{
				// IF THE MEMORY LOCK HAS NOT BEEN ACQUIRED

				// + THERE IS A FALLBACK ENTRY
				// + FAIL-SAFE IS ENABLED
				// --> USE IT (WITHOUT SAVING IT, SINCE THE ALREADY RUNNING FACTORY WILL DO IT ANYWAY)

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || memoryEntry.IsStale(), activity);

				return memoryEntry;
			}

			// TRY AGAIN WITH MEMORY CACHE (AFTER THE MEMORY LOCK HAS BEEN ACQUIRED, MAYBE SOMETHING CHANGED)
			if (_mca.ShouldRead(options))
			{
				(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);
			}

			// TAGGING
			if (memoryEntryIsValid)
			{
				(memoryEntry, memoryEntryIsValid) = await CheckEntrySecondaryExpirationAsync(operationId, key, memoryEntry, false, token).ConfigureAwait(false);
			}

			if (memoryEntryIsValid)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, memoryEntryIsValid == false || memoryEntry!.IsStale(), activity);

				return memoryEntry;
			}

			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			FusionCacheDistributedEntry<TValue>? distributedEntry = null;
			bool distributedEntryIsValid = false;

			var dca = DistributedCache;
			if (dca.ShouldRead(options) && dca.CanBeUsed(operationId, key))
			{
				if ((memoryEntry is not null && dca.ShouldReadWhenStale(options) == false) == false)
				{
					token.ThrowIfCancellationRequested();

					(distributedEntry, distributedEntryIsValid) = await _dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, null, token).ConfigureAwait(false);
				}
			}

			// TAGGING (DISTRIBUTED)
			if (distributedEntryIsValid)
			{
				(distributedEntry, distributedEntryIsValid) = await CheckEntrySecondaryExpirationAsync(operationId, key, distributedEntry, false, token).ConfigureAwait(false);
			}

			if (distributedEntryIsValid)
			{
				isStale = false;
				entry = FusionCacheMemoryEntry<TValue>.CreateFromOtherEntry(distributedEntry!, options);
			}
			else
			{
				// FACTORY
				if (isRealFactory == false)
				{
					var value = await factory(null!, token).ConfigureAwait(false);
					hasNewValue = true;

					entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, tags, options, isStale, null, null, null);
				}
				else
				{
					Task<TValue>? factoryTask = null;

					var timeout = options.GetAppropriateFactoryTimeout(memoryEntry is not null || distributedEntry is not null);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", CacheName, InstanceId, operationId, key, timeout.ToLogString_Timeout());

					var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(options, distributedEntry, memoryEntry, tags);

					// ACTIVITY
					var activityForFactory = Activities.Source.StartActivityWithCommonTags(Activities.Names.ExecuteFactory, CacheName, InstanceId, key, operationId);

					try
					{
						token.ThrowIfCancellationRequested();

						TValue? value;
						if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
						{
							value = await factory(ctx, CancellationToken.None).ConfigureAwait(false);
						}
						else
						{
							value = await RunUtils.RunAsyncFuncWithTimeoutAsync(ct => factory(ctx, ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token).ConfigureAwait(false);
						}

						if (ctx.HasFailed)
						{
							// FAIL

							UpdateAdaptiveOptions(ctx, ref options);

							var errorMessage = ctx.ErrorMessage!;

							ProcessFactoryError(operationId, key, errorMessage);

							// ACTIVITY
							activityForFactory?.SetStatus(ActivityStatusCode.Error, errorMessage);
							activityForFactory?.Dispose();

							entry = TryActivateFailSafe<TValue>(operationId, key, distributedEntry, memoryEntry, failSafeDefaultValue, options);

							if (entry is null)
							{
								throw new FusionCacheFactoryException(errorMessage);
							}

							isStale = true;
						}
						else
						{
							// SUCCESS

							activityForFactory?.Dispose();

							hasNewValue = true;

							UpdateAdaptiveOptions(ctx, ref options);

							entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, ctx.Tags, options, isStale, ctx.LastModified, ctx.ETag, null);

							// EVENTS
							_events.OnFactorySuccess(operationId, key);
						}
					}
					catch (OperationCanceledException exc)
					{
						// ACTIVITY
						activityForFactory?.SetStatus(ActivityStatusCode.Error, exc.Message);
						activityForFactory?.Dispose();

						throw;
					}
					catch (Exception exc)
					{
						UpdateAdaptiveOptions(ctx, ref options);

						ProcessFactoryError(operationId, key, exc);

						MaybeBackgroundCompleteTimedOutFactory<TValue>(operationId, key, ctx, factoryTask, options, activityForFactory);

						entry = TryActivateFailSafe<TValue>(operationId, key, distributedEntry, memoryEntry, failSafeDefaultValue, options);

						if (entry is null)
						{
							throw;
						}

						isStale = true;
					}
				}
			}

			// SAVING THE DATA IN THE MEMORY CACHE
			if (entry is not null)
			{
				if (_mca.ShouldWrite(options))
				{
					_mca.SetEntry<TValue>(operationId, key, entry, options, ReferenceEquals(memoryEntry, entry));
				}
			}
		}
		finally
		{
			// MEMORY LOCK
			if (memoryLockObj is not null)
				ReleaseMemoryLock(operationId, key, memoryLockObj);
		}

		if (hasNewValue)
		{
			// DISTRIBUTED
			if (entry is not null && isStale == false)
			{
				if (RequiresDistributedOperations(options))
				{
					await DistributedSetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);
				}
			}

			// EVENT
			_events.OnMiss(operationId, key, activity);
			_events.OnSet(operationId, key);
		}
		else if (entry is not null)
		{
			// EVENT
			_events.OnHit(operationId, key, isStale || entry.IsStale(), activity);
		}
		else
		{
			// EVENT
			_events.OnMiss(operationId, key, activity);
		}

		return entry;
	}

	/// <inheritdoc/>
	public async ValueTask<TValue> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		Metrics.CounterGetOrSet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		if (factory is null)
			throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrSet, CacheName, InstanceId, key, operationId);

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, tagsArray, factory, true, failSafeDefaultValue, options, activity, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, InstanceId, operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

		return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
	}

	/// <inheritdoc/>
	public async ValueTask<TValue> GetOrSetAsync<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		Metrics.CounterGetOrSet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling GetOrSetAsync<T> {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrSet, CacheName, InstanceId, key, operationId);

		var entry = await GetOrSetEntryInternalAsync<TValue>(operationId, key, tagsArray, (_, _) => Task.FromResult(defaultValue), false, default, options, activity, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, InstanceId, operationId, key);
			throw new InvalidOperationException("The resulting FusionCache entry is null");
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

		return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
	}

	// TRY GET

	private async ValueTask<IFusionCacheMemoryEntry?> TryGetEntryInternalAsync<TValue>(string operationId, string key, FusionCacheEntryOptions? options, Activity? activity, CancellationToken token)
	{
		options ??= _options.DefaultEntryOptions;

		token.ThrowIfCancellationRequested();

		IFusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;

		if (_mca.ShouldRead(options))
		{
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);
		}

		// TAGGING
		if (memoryEntryIsValid)
		{
			(memoryEntry, memoryEntryIsValid) = await CheckEntrySecondaryExpirationAsync(operationId, key, memoryEntry, false, token).ConfigureAwait(false);
			if (memoryEntry is null)
			{
				// EVENT
				_events.OnMiss(operationId, key, activity);

				return null;
			}
		}

		if (memoryEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, InstanceId, operationId, key);

			// EVENT
			_events.OnHit(operationId, key, memoryEntry!.IsStale(), activity);

			return memoryEntry;
		}

		var dca = DistributedCache;

		// EARLY RETURN: NO USABLE DISTRIBUTED CACHE
		if ((memoryEntry is not null && dca.ShouldReadWhenStale(options) == false) || dca.CanBeUsed(operationId, key) == false)
		{
			if (options.IsFailSafeEnabled && memoryEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, true, activity);

				return memoryEntry;
			}

			// EVENT
			_events.OnMiss(operationId, key, activity);

			return null;
		}

		// TRY WITH DISTRIBUTED CACHE
		FusionCacheDistributedEntry<TValue>? distributedEntry;
		bool distributedEntryIsValid;

		(distributedEntry, distributedEntryIsValid) = await dca!.TryGetEntryAsync<TValue>(operationId, key, options, memoryEntry is not null, null, token).ConfigureAwait(false);

		// TAGGING
		if (distributedEntryIsValid)
		{
			(distributedEntry, distributedEntryIsValid) = await CheckEntrySecondaryExpirationAsync(operationId, key, distributedEntry, false, token).ConfigureAwait(false);
			if (distributedEntry is null)
			{
				// EVENT
				_events.OnMiss(operationId, key, activity);

				return null;
			}
		}

		if (distributedEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using distributed entry", CacheName, InstanceId, operationId, key);

			memoryEntry = distributedEntry!.AsMemoryEntry<TValue>(options);

			// SAVING THE DATA IN THE MEMORY CACHE
			if (_mca.ShouldWrite(options))
			{
				_mca.SetEntry<TValue>(operationId, key, memoryEntry, options);
			}

			// EVENT
			_events.OnHit(operationId, key, distributedEntry!.IsStale(), activity);

			return memoryEntry;
		}

		if (options.IsFailSafeEnabled)
		{
			// FAIL-SAFE IS ENABLED -> CAN USE STALE ENTRY

			// IF DISTRIBUTED ENTRY IS THERE -> USE IT
			if (distributedEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using distributed entry (expired)", CacheName, InstanceId, operationId, key);

				memoryEntry = distributedEntry.AsMemoryEntry<TValue>(options);

				// SAVING THE DATA IN THE MEMORY CACHE
				if (_mca.ShouldWrite(options))
				{
					_mca.SetEntry<TValue>(operationId, key, memoryEntry, options);
				}

				// EVENT
				_events.OnHit(operationId, key, true, activity);

				return memoryEntry;
			}

			// IF MEMORY ENTRY IS THERE -> USE IT
			if (memoryEntry is not null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry (expired)", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.OnHit(operationId, key, true, activity);

				return memoryEntry;
			}
		}

		// EVENT
		_events.OnMiss(operationId, key, activity);

		return null;
	}

	/// <inheritdoc/>
	public async ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		Metrics.CounterTryGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling TryGetAsync<T> {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.TryGet, CacheName, InstanceId, key, operationId);

		var entry = await TryGetEntryInternalAsync<TValue>(operationId, key, options, activity, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return NO SUCCESS", CacheName, InstanceId, operationId, key);

			return default;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return SUCCESS", CacheName, InstanceId, operationId, key);

		return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
	}

	// GET OR DEFAULT

	/// <inheritdoc/>
	public async ValueTask<TValue?> GetOrDefaultAsync<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		Metrics.CounterGetOrDefault.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling GetOrDefaultAsync<T> {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrDefault, CacheName, InstanceId, key, operationId);

		var entry = await TryGetEntryInternalAsync<TValue>(operationId, key, options, activity, token).ConfigureAwait(false);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return DEFAULT VALUE", CacheName, InstanceId, operationId, key);

			return defaultValue;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

		return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
	}

	// SET

	/// <inheritdoc/>
	public async ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		options ??= _options.DefaultEntryOptions;

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling SetAsync<T> {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Set, CacheName, InstanceId, key, operationId);

		// TODO: MAYBE FIND A WAY TO PASS LASTMODIFIED/ETAG HERE
		var entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, tagsArray, options, false, null, null, null);

		if (_mca.ShouldWrite(options))
		{
			_mca.SetEntry<TValue>(operationId, key, entry, options);
		}

		if (RequiresDistributedOperations(options))
		{
			await DistributedSetEntryAsync<TValue>(operationId, key, entry, options, token).ConfigureAwait(false);
		}

		// EVENT
		_events.OnSet(operationId, key);
	}

	// REMOVE

	private async ValueTask RemoveInternalAsync(string key, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling RemoveAsync {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Remove, CacheName, InstanceId, key, operationId);

		if (_mca.ShouldWrite(options))
		{
			_mca.RemoveEntry(operationId, key);
		}

		if (RequiresDistributedOperations(options))
		{
			await DistributedRemoveEntryAsync(operationId, key, options, token).ConfigureAwait(false);
		}

		// EVENT
		_events.OnRemove(operationId, key);
	}

	/// <inheritdoc/>
	public async ValueTask RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		options ??= _options.DefaultEntryOptions;

		await RemoveInternalAsync(key, options, token).ConfigureAwait(false);
	}

	// EXPIRE

	private async ValueTask ExpireInternalAsync(string key, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling ExpireAsync {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Expire, CacheName, InstanceId, key, operationId);

		if (_mca.ShouldWrite(options))
		{
			_mca.ExpireEntry(operationId, key, null);
		}

		if (RequiresDistributedOperations(options))
		{
			await DistributedExpireEntryAsync(operationId, key, options, token).ConfigureAwait(false);
		}

		// EVENT
		_events.OnExpire(operationId, key);
	}

	/// <inheritdoc/>
	public async ValueTask ExpireAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		ValidateCacheKey(key);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		options ??= _options.DefaultEntryOptions;

		await ExpireInternalAsync(key, options, token).ConfigureAwait(false);
	}

	// TAGGING

	private async ValueTask<(TEntry? Entry, bool isValid)> CheckEntrySecondaryExpirationAsync<TEntry>(string operationId, string key, TEntry? entry, bool executeCascadeAction, CancellationToken token)
		where TEntry : class, IFusionCacheEntry
	{
		if (entry is null)
			return (null, false);

		if (_options.DisableTagging)
			return (entry, true);

		var entryTimestamp = entry.Timestamp;
		var tags = entry.Tags;

		// CHECK: CLEAR (REMOVE)
		if (ClearRemoveTagInternalCacheKey != key && ClearExpireTagInternalCacheKey != key && CanExecuteRawClear() == false)
		{
			if (ClearRemoveTimestamp < 0 || (HasDistributedCache && HasBackplane == false))
			{
				var _tmp = await GetOrSetAsync<long>(ClearRemoveTagCacheKey, FusionCacheInternalUtils.SharedTagExpirationDataFactoryAsync, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token).ConfigureAwait(false);

				var _tmp2 = Interlocked.Exchange(ref ClearRemoveTimestamp, _tmp);

				if (_tmp2 != _tmp)
				{
					// NEW CLEAR (REMOVE) TIMESTAMP
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): new Clear (Remove) timestamp {ClearRemoveTimestamp} (OLD: {OldClearRemoveTimestamp})", CacheName, InstanceId, operationId, key, _tmp, _tmp2);
				}
			}

			if (entryTimestamp <= ClearRemoveTimestamp)
			{
				// NOT VALID, VIA CLEAR (REMOVE)
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): entry no more valid via Clear (Remove) ({EntryTimestamp} <= {ClearRemoveTimestamp})", CacheName, InstanceId, operationId, key, entryTimestamp, ClearRemoveTimestamp);

				if (executeCascadeAction == false)
					return (entry, false);

				// REMOVE ENTRY
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cascade remove entry", CacheName, InstanceId, operationId, key);

				await RemoveInternalAsync(key, _cascadeRemoveByTagEntryOptions, token).ConfigureAwait(false);

				return (null, false);
			}
		}

		// CHECK: REMOVE BY TAG
		if (tags is not null && tags.Length > 0)
		{
			foreach (var tag in tags)
			{
				var tagExpiration = await GetOrSetAsync<long>(GetTagCacheKey(tag), FusionCacheInternalUtils.SharedTagExpirationDataFactoryAsync, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token).ConfigureAwait(false);
				if (entryTimestamp <= tagExpiration)
				{
					// NOT VALID, VIA REMOVE BY TAG
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): entry expired via tag {Tag}", CacheName, InstanceId, operationId, key, tag);

					if (executeCascadeAction == false)
						return (entry, false);

					// EXPIRE ENTRY
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cascade expire entry", CacheName, InstanceId, operationId, key);

					await ExpireInternalAsync(key, _cascadeRemoveByTagEntryOptions, token).ConfigureAwait(false);

					return (entry, false);
				}

				token.ThrowIfCancellationRequested();
			}
		}

		// CHECK: CLEAR (EXPIRE)
		if (ClearRemoveTagInternalCacheKey != key && ClearExpireTagInternalCacheKey != key)
		{
			if (ClearExpireTimestamp < 0 || (HasDistributedCache && HasBackplane == false))
			{
				var _tmp = await GetOrSetAsync<long>(ClearExpireTagCacheKey, FusionCacheInternalUtils.SharedTagExpirationDataFactoryAsync, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token).ConfigureAwait(false);

				var _tmp2 = Interlocked.Exchange(ref ClearExpireTimestamp, _tmp);

				if (_tmp2 != _tmp)
				{
					// NEW CLEAR (EXPIRE) TIMESTAMP
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): new Clear (Expire) timestamp {ClearExpireTimestamp} (OLD: {OldClearExpireTimestamp})", CacheName, InstanceId, operationId, key, _tmp, _tmp2);
				}
			}

			if (entryTimestamp <= ClearExpireTimestamp)
			{
				// NOT VALID, VIA CLEAR (EXPIRE)
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): entry no more valid via Clear (Expire) ({EntryTimestamp} <= {ClearExpireTimestamp})", CacheName, InstanceId, operationId, key, entryTimestamp, ClearExpireTimestamp);

				if (executeCascadeAction == false)
					return (entry, false);

				// EXPIRE ENTRY
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cascade expire entry", CacheName, InstanceId, operationId, key);

				await ExpireInternalAsync(key, _cascadeRemoveByTagEntryOptions, token).ConfigureAwait(false);

				return (entry, false);
			}
		}

		return (entry, true);
	}

	private async ValueTask SetTagDataInternalAsync(string tag, FusionCacheEntryOptions options, CancellationToken token)
	{
		await SetAsync(
			GetTagCacheKey(tag),
			FusionCacheInternalUtils.GetCurrentTimestamp(),
			options,
			FusionCacheInternalUtils.NoTags,
			token
		);
	}

	/// <inheritdoc/>
	public async ValueTask RemoveByTagAsync(string tag, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckTaggingEnabled();

		ValidateTag(tag);

		var operationId = MaybeGenerateOperationId();

		options ??= _tagsDefaultEntryOptions;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): calling RemoveByTagAsync {Options}", CacheName, InstanceId, operationId, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.RemoveByTag, CacheName, InstanceId, null, operationId);

		if (_options.IncludeTagsInTraces)
		{
			activity?.AddTag(Tags.Names.OperationTag, tag);
		}

		await SetTagDataInternalAsync(tag, options, token).ConfigureAwait(false);

		// EVENT
		_events.OnRemoveByTag(operationId, tag);
	}

	// CLEAR

	/// <inheritdoc/>
	public async ValueTask ClearAsync(bool allowFailSafe = true, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckTaggingEnabled();

		var operationId = MaybeGenerateOperationId();

		options ??= _tagsDefaultEntryOptions;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): calling ClearAsync {Options}", CacheName, InstanceId, operationId, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Clear, CacheName, InstanceId, null, operationId);

		if (allowFailSafe)
		{
			// CLEAR EXPIRE
			Interlocked.Exchange(ref ClearExpireTimestamp, FusionCacheInternalUtils.GetCurrentTimestamp());

			await SetTagDataInternalAsync(ClearExpireTag, options, token).ConfigureAwait(false);
		}
		else
		{
			// CLEAR REMOVE
			Interlocked.Exchange(ref ClearRemoveTimestamp, FusionCacheInternalUtils.GetCurrentTimestamp());

			if (TryExecuteRawClear(operationId) == false)
			{
				await SetTagDataInternalAsync(ClearRemoveTag, options, token).ConfigureAwait(false);
			}
		}

		// EVENT
		_events.OnClear(operationId);
	}

	// DISTRIBUTED ACTIONS

	private async ValueTask ExecuteDistributedActionAsync(string operationId, string key, FusionCacheAction action, long timestamp, Func<DistributedCacheAccessor, bool, CancellationToken, ValueTask<bool>> distributedCacheAction, Func<BackplaneAccessor, bool, CancellationToken, ValueTask<bool>> backplaneAction, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (RequiresDistributedOperations(options) == false)
		{
			return;
		}

		var mustAwaitCompletion = MustAwaitDistributedOperations(options);
		var isBackground = !mustAwaitCompletion;

		await RunUtils.RunAsyncActionAdvancedAsync(
			async ct1 =>
			{
				// DISTRIBUTED CACHE
				var dca = DistributedCache;
				if (dca.ShouldWrite(options))
				{
					var dcaSuccess = false;
					try
					{
						if (dca!.IsCurrentlyUsable(operationId, key))
						{
							dcaSuccess = await distributedCacheAction(dca, isBackground, ct1).ConfigureAwait(false);
						}
					}
					catch
					{
						//TryAddAutoRecoveryItem(operationId, key, action, timestamp, options, null);
						throw;
					}

					if (dcaSuccess == false)
					{
						AutoRecovery.TryAddItem(operationId, key, action, timestamp, options);
						return;
					}
				}

				var mustAwaitBackplaneCompletion = isBackground || MustAwaitBackplaneOperations(options);
				var isBackplaneBackground = isBackground || !mustAwaitBackplaneCompletion;

				await RunUtils.RunAsyncActionAdvancedAsync(
					async ct2 =>
					{
						// BACKPLANE
						var bpa = Backplane;
						if (bpa.ShouldWrite(options))
						{
							var bpaSuccess = false;
							try
							{
								if (bpa!.IsCurrentlyUsable(operationId, key))
								{
									bpaSuccess = await backplaneAction(bpa, isBackplaneBackground, ct2).ConfigureAwait(false);
								}
							}
							catch
							{
								throw;
							}

							if (bpaSuccess == false)
							{
								AutoRecovery.TryAddItem(operationId, key, action, timestamp, options);
							}
						}
					},
					Timeout.InfiniteTimeSpan,
					false,
					mustAwaitBackplaneCompletion,
					null,
					true,
					token
				).ConfigureAwait(false);
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
			entry.Timestamp,
			(dca, isBackground, ct) =>
			{
				return dca!.SetEntryAsync<TValue>(operationId, key, entry, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishSetAsync(operationId, key, entry.Timestamp, options, false, isBackground, ct);
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
			FusionCacheAction.EntryRemove,
			FusionCacheInternalUtils.GetCurrentTimestamp(),
			(dca, isBackground, ct) =>
			{
				return dca.RemoveEntryAsync(operationId, key, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishRemoveAsync(operationId, key, null, options, false, isBackground, ct);
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
			FusionCacheAction.EntryExpire,
			FusionCacheInternalUtils.GetCurrentTimestamp(),
			(dca, isBackground, ct) =>
			{
				return dca.RemoveEntryAsync(operationId, key, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishExpireAsync(operationId, key, null, options, false, isBackground, ct);
			},
			options,
			token
		);
	}
}
