using System.Diagnostics;
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

	private void ExecuteEagerRefreshWithSyncFactory<TValue>(string operationId, string key, string originalKey, string[]? tags, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory, FusionCacheEntryOptions options, IFusionCacheMemoryEntry memoryEntry, object memoryLockObj)
	{
		// EVENT
		_events.OnEagerRefresh(operationId, key);

		_ = Task.Run(() =>
		{
			// TRY WITH DISTRIBUTED CACHE (IF ANY)
			try
			{
				var dca = DistributedCacheAccessor;
				if (dca.ShouldRead(options) && dca.CanBeUsed(operationId, key))
				{
					FusionCacheDistributedEntry<TValue>? distributedEntry;
					bool distributedEntryIsValid;

					(distributedEntry, distributedEntryIsValid) = dca!.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, Timeout.InfiniteTimeSpan, default);
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

			var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(key, originalKey, options, null, memoryEntry, tags);

			var factoryTask = Task.Run(() => factory(ctx, default));

			CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, memoryLockObj, activity);
		});
	}

	private IFusionCacheMemoryEntry? GetOrSetEntryInternal<TValue>(string operationId, string key, string originalKey, string[]? tags, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory, bool isRealFactory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions options, Activity? activity, CancellationToken token)
	{
		IFusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;
		object? memoryLockObj = null;

		// DIRECTLY CHECK MEMORY CACHE (TO AVOID LOCKING)
		if (_mca.ShouldRead(options))
		{
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);
		}

		// TAGGING
		if (memoryEntry is not null)
		{
			(memoryEntry, memoryEntryIsValid) = CheckEntrySecondaryExpiration(operationId, key, memoryEntry, false, token);
		}

		if (memoryEntryIsValid)
		{
			// VALID CACHE ENTRY

			// CHECK FOR EAGER REFRESH
			if (isRealFactory && memoryEntry.ShouldEagerlyRefresh())
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): should eagerly refresh", CacheName, InstanceId, operationId, key);

				// TRY TO GET THE MEMORY LOCK WITHOUT WAITING, SO THAT ONLY THE FIRST ONE WILL ACTUALLY REFRESH THE ENTRY
				memoryLockObj = AcquireMemoryLock(operationId, key, TimeSpan.Zero, token);
				if (memoryLockObj is null)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): eager refresh already occurring", CacheName, InstanceId, operationId, key);
				}
				else
				{
					// RESET EAGER REFRESH
					if (memoryEntry!.Metadata is not null)
						memoryEntry.Metadata.EagerExpirationTimestamp = null;

					// EXECUTE EAGER REFRESH
					ExecuteEagerRefreshWithSyncFactory<TValue>(operationId, key, originalKey, tags, factory, options, memoryEntry, memoryLockObj);
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
			memoryLockObj = AcquireMemoryLock(operationId, key, options.GetAppropriateMemoryLockTimeout(_options, memoryEntry is not null), token);

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
			if (memoryEntry is not null)
			{
				(memoryEntry, memoryEntryIsValid) = CheckEntrySecondaryExpiration(operationId, key, memoryEntry, false, token);
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

			var dca = DistributedCacheAccessor;
			if (dca.ShouldRead(options) && dca.CanBeUsed(operationId, key))
			{
				if ((memoryEntry is not null && dca.ShouldReadWhenStale(options) == false) == false)
				{
					token.ThrowIfCancellationRequested();

					(distributedEntry, distributedEntryIsValid) = dca!.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, null, token);
				}
			}

			// TAGGING (DISTRIBUTED)
			if (distributedEntry is not null)
			{
				(distributedEntry, distributedEntryIsValid) = CheckEntrySecondaryExpiration(operationId, key, distributedEntry, false, token);
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
					var value = factory(null!, token);
					hasNewValue = true;

					entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, GetSerializedValueFromValue(operationId, key, value, options), null, tags, options, isStale, null, null);
				}
				else
				{
					Task<TValue>? factoryTask = null;

					var timeout = options.GetAppropriateFactoryTimeout(_options, memoryEntry is not null || distributedEntry is not null);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling the factory (timeout={Timeout})", CacheName, InstanceId, operationId, key, timeout.ToLogString_Timeout());

					var ctx = FusionCacheFactoryExecutionContext<TValue>.CreateFromEntries(key, originalKey, options, distributedEntry, memoryEntry, tags);

					// ACTIVITY
					var activityForFactory = Activities.Source.StartActivityWithCommonTags(Activities.Names.ExecuteFactory, CacheName, InstanceId, key, operationId);

					try
					{
						token.ThrowIfCancellationRequested();

						TValue? value;
						if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
						{
							value = factory(ctx, CancellationToken.None);
						}
						else
						{
							value = RunUtils.RunSyncFuncWithTimeout(ct => factory(ctx, ct), timeout, options.AllowTimedOutFactoryBackgroundCompletion == false, x => factoryTask = x, token);
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

							entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, GetSerializedValueFromValue(operationId, key, value, options), null, ctx.Tags, options, isStale, ctx.LastModified?.UtcTicks, ctx.ETag);

							// EVENTS
							_events.OnFactorySuccess(operationId, key);
						}
					}
					catch (OperationCanceledException exc)
					{
						// ACTIVITY
						activityForFactory?.SetStatus(ActivityStatusCode.Error, exc.Message);
						activityForFactory?.AddException(exc);
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
					DistributedSetEntry<TValue>(operationId, key, entry, options, token);
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
	public TValue GetOrSet<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		// METRIC
		Metrics.CounterGetOrSet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key, out var originalKey);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		if (factory is null)
			throw new ArgumentNullException(nameof(factory), "Factory cannot be null");

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrSet<T> call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrSet, CacheName, InstanceId, key, operationId);

		try
		{
			var entry = GetOrSetEntryInternal<TValue>(operationId, key, originalKey, tagsArray, factory, true, failSafeDefaultValue, options, activity, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, InstanceId, operationId, key);
				throw new InvalidOperationException("The resulting FusionCache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrSet<T> return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

			return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	/// <inheritdoc/>
	public TValue GetOrSet<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		// METRIC
		Metrics.CounterGetOrSet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key, out var originalKey);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrSet<T> call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrSet, CacheName, InstanceId, key, operationId);

		try
		{
			var entry = GetOrSetEntryInternal<TValue>(operationId, key, originalKey, tagsArray, (_, _) => defaultValue, false, default, options, activity, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): something went wrong, the resulting entry is null, and it should not be possible", CacheName, InstanceId, operationId, key);
				throw new InvalidOperationException("The resulting FusionCache entry is null");
			}

			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrSet<T> return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

			return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	// TRY GET

	private IFusionCacheMemoryEntry? TryGetEntryInternal<TValue>(string operationId, string key, FusionCacheEntryOptions options, Activity? activity, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		IFusionCacheMemoryEntry? memoryEntry = null;
		bool memoryEntryIsValid = false;

		if (_mca.ShouldRead(options))
		{
			(memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);
		}

		// TAGGING
		if (memoryEntry is not null)
		{
			(memoryEntry, memoryEntryIsValid) = CheckEntrySecondaryExpiration(operationId, key, memoryEntry, true, token);
		}

		if (memoryEntryIsValid)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using memory entry", CacheName, InstanceId, operationId, key);

			// EVENT
			_events.OnHit(operationId, key, memoryEntry!.IsStale(), activity);

			return memoryEntry;
		}

		var dca = DistributedCacheAccessor;

		// EARLY RETURN: NO USABLE DISTRIBUTED CACHE
		if ((memoryEntry is not null && dca.ShouldReadWhenStale(options) == false) || dca.CanBeUsed(operationId, key) == false)
		{
			if (options.AllowStaleOnReadOnly && memoryEntry is not null)
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
		FusionCacheDistributedEntry<TValue>? distributedEntry = null;
		bool distributedEntryIsValid = false;

		if (dca.ShouldRead(options) && dca.CanBeUsed(operationId, key))
		{
			(distributedEntry, distributedEntryIsValid) = dca!.TryGetEntry<TValue>(operationId, key, options, memoryEntry is not null, null, token);
		}

		// TAGGING
		if (distributedEntry is not null)
		{
			(distributedEntry, distributedEntryIsValid) = CheckEntrySecondaryExpiration(operationId, key, distributedEntry, false, token);
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

		if (options.AllowStaleOnReadOnly)
		{
			// CAN USE STALE ENTRY

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
	public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		// METRIC
		Metrics.CounterTryGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): TryGet<T> call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.TryGet, CacheName, InstanceId, key, operationId);

		try
		{
			var entry = TryGetEntryInternal<TValue>(operationId, key, options, activity, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Information) ?? false)
					_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): TryGet<T> return (no value)", CacheName, InstanceId, operationId, key);

				return default;
			}

			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): TryGet<T> return (has value)", CacheName, InstanceId, operationId, key);

			return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	// GET OR DEFAULT

	/// <inheritdoc/>
	public TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		// METRIC
		Metrics.CounterGetOrDefault.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrDefault<T> call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.GetOrDefault, CacheName, InstanceId, key, operationId);

		try
		{
			var entry = TryGetEntryInternal<TValue>(operationId, key, options, activity, token);

			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Information) ?? false)
					_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrDefault<T> return (default value)", CacheName, InstanceId, operationId, key);

				return defaultValue;
			}

			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): GetOrDefault<T> return {Entry}", CacheName, InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

			return GetValueFromMemoryEntry<TValue>(operationId, key, entry, options);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	// SET

	/// <inheritdoc/>
	public void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key);

		var tagsArray = tags.AsArray();

		ValidateTags(tagsArray);

		token.ThrowIfCancellationRequested();

		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): Set<T> call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Set, CacheName, InstanceId, key, operationId);

		try
		{
			// TODO: MAYBE FIND A WAY TO PASS LASTMODIFIED/ETAG HERE
			var entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(value, GetSerializedValueFromValue(operationId, key, value, options), null, tagsArray, options, false, null, null);

			if (_mca.ShouldWrite(options))
			{
				_mca.SetEntry<TValue>(operationId, key, entry, options);
			}

			if (RequiresDistributedOperations(options))
			{
				DistributedSetEntry<TValue>(operationId, key, entry, options, token);
			}

			// EVENT
			_events.OnSet(operationId, key);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	// REMOVE

	private void RemoveInternal(string key, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): Remove call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Remove, CacheName, InstanceId, key, operationId);

		try
		{
			if (_mca.ShouldWrite(options))
			{
				_mca.RemoveEntry(operationId, key);
			}

			if (RequiresDistributedOperations(options))
			{
				DistributedRemoveEntry(operationId, key, options, token);
			}

			// EVENT
			_events.OnRemove(operationId, key);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	/// <inheritdoc/>
	public void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		RemoveInternal(key, options, token);
	}

	// EXPIRE

	private void ExpireInternal(string key, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		var operationId = MaybeGenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): Expire call {Options}", CacheName, InstanceId, operationId, key, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Expire, CacheName, InstanceId, key, operationId);

		try
		{
			if (_mca.ShouldWrite(options))
			{
				_mca.ExpireEntry(operationId, key, null);
			}

			if (RequiresDistributedOperations(options))
			{
				DistributedExpireEntry(operationId, key, options, token);
			}

			// EVENT
			_events.OnExpire(operationId, key);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	/// <inheritdoc/>
	public void Expire(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		ValidateCacheKey(key);

		options ??= this.GetOrCreateDefaultEntryOptions(key, false);

		MaybePreProcessCacheKey(ref key);

		token.ThrowIfCancellationRequested();

		ExpireInternal(key, options, token);
	}

	// TAGGING

	private (TEntry? Entry, bool isValid) CheckEntrySecondaryExpiration<TEntry>(string operationId, string key, TEntry? entry, bool executeCascadeAction, CancellationToken token)
		where TEntry : class, IFusionCacheEntry
	{
		if (entry is null)
			return (null, false);

		if (_options.DisableTagging)
			return (entry, entry.IsLogicallyExpired() == false);

		if (key.StartsWith(TagInternalCacheKeyPrefix))
			return (entry, entry.IsLogicallyExpired() == false);

		var entryTimestamp = entry.Timestamp;
		var tags = entry.Tags;

		// CHECK: CLEAR (REMOVE)
		if (CanExecuteRawClear() == false)
		{
			if (ClearRemoveTimestamp < 0 || (HasDistributedCache && HasBackplane == false))
			{
				var _tmp = GetOrSet<long>(ClearRemoveTagCacheKey, FusionCacheInternalUtils.SharedTagExpirationDataFactory, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token);

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
					return (null, false);

				// REMOVE ENTRY
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cascade remove entry", CacheName, InstanceId, operationId, key);

				RemoveInternal(key, _cascadeRemoveByTagEntryOptions, token);

				return (null, false);
			}
		}

		// CHECK: REMOVE BY TAG
		if (tags is not null && tags.Length > 0)
		{
			foreach (var tag in tags)
			{
				var tagExpiration = GetOrSet<long>(GetTagCacheKey(tag), FusionCacheInternalUtils.SharedTagExpirationDataFactory, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token);
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

					ExpireInternal(key, _cascadeRemoveByTagEntryOptions, token);

					return (entry, false);
				}
			}
		}

		// CHECK: CLEAR (EXPIRE)
		if (ClearExpireTimestamp < 0 || (HasDistributedCache && HasBackplane == false))
		{
			if (CanExecuteRawClear())
			{
				// OPTIMIZATION: IF IT'S THE FIRST CHECK AND WE CAN EXECUTE RAW CLEAR
				// -> DIRECTLY SET IT WITHOUT CHECKING THE SPECIAL CACHE ENTRY, SINCE
				// NOBODY ELSE CAN HAVE CHANGED IT
				Interlocked.Exchange(ref ClearExpireTimestamp, 0L);
			}
			else
			{
				var _tmp = GetOrSet<long>(ClearExpireTagCacheKey, FusionCacheInternalUtils.SharedTagExpirationDataFactory, 0L, _tagsDefaultEntryOptions, FusionCacheInternalUtils.NoTags, token);

				var _tmp2 = Interlocked.Exchange(ref ClearExpireTimestamp, _tmp);

				if (_tmp2 != _tmp)
				{
					// NEW CLEAR (EXPIRE) TIMESTAMP
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): new Clear (Expire) timestamp {ClearExpireTimestamp} (OLD: {OldClearExpireTimestamp})", CacheName, InstanceId, operationId, key, _tmp, _tmp2);
				}
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

			ExpireInternal(key, _cascadeRemoveByTagEntryOptions, token);

			return (entry, false);
		}

		return (entry, entry.IsLogicallyExpired() == false);
	}

	internal void SetTagDataInternal(string tag, long timestamp, FusionCacheEntryOptions options, CancellationToken token)
	{
		Set(
			GetTagCacheKey(tag),
			timestamp,
			options,
			FusionCacheInternalUtils.NoTags,
			token
		);
	}

	/// <inheritdoc/>
	public void RemoveByTag(string tag, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		CheckTaggingEnabled();

		ValidateTag(tag);

		var operationId = MaybeGenerateOperationId();

		options ??= _tagsDefaultEntryOptions;

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): RemoveByTag call {Options}", CacheName, InstanceId, operationId, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.RemoveByTag, CacheName, InstanceId, null, operationId);

		try
		{
			if (_options.IncludeTagsInTraces)
			{
				activity?.AddTag(Tags.Names.OperationTag, tag);
			}

			SetTagDataInternal(tag, FusionCacheInternalUtils.GetCurrentTimestamp(), options, token);

			// EVENT
			_events.OnRemoveByTag(operationId, tag);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	/// <inheritdoc/>
	public void RemoveByTag(IEnumerable<string>? tags, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		CheckTaggingEnabled();

		if (tags is null)
			return;

		if (tags is ICollection<string> tags2)
		{
			if (tags2.Count == 0)
				return;

			if (tags2.Count == 1)
			{
				RemoveByTag(tags2.First(), options, token);
				return;
			}
		}

		foreach (var tag in tags)
		{
			RemoveByTag(tag, options, token);
		}
	}

	// CLEAR

	/// <inheritdoc/>
	public void Clear(bool allowFailSafe = true, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		CheckDisposed();

		CheckTaggingEnabled();

		var operationId = MaybeGenerateOperationId();

		options ??= _tagsDefaultEntryOptions;

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): Clear({AllowFailSafe}) call {Options}", CacheName, InstanceId, operationId, allowFailSafe, options.ToLogString());

		// ACTIVITY
		using var activity = Activities.Source.StartActivityWithCommonTags(Activities.Names.Clear, CacheName, InstanceId, null, operationId);

		var now = FusionCacheInternalUtils.GetCurrentTimestamp();

		try
		{
			if (allowFailSafe)
			{
				// CLEAR EXPIRE
				Interlocked.Exchange(ref ClearExpireTimestamp, now);

				SetTagDataInternal(_options.InternalStrings.ClearExpireTag, now, options, token);
			}
			else
			{
				// CLEAR REMOVE
				Interlocked.Exchange(ref ClearRemoveTimestamp, now);

				if (TryExecuteRawClear(operationId) == false)
				{
					SetTagDataInternal(_options.InternalStrings.ClearRemoveTag, now, options, token);
				}
			}

			// EVENT
			_events.OnClear(operationId);
		}
		catch (Exception exc)
		{
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);
			throw;
		}
	}

	// DISTRIBUTED ACTIONS

	private void ExecuteDistributedAction(string operationId, string key, FusionCacheAction action, long timestamp, Func<DistributedCacheAccessor, bool, CancellationToken, bool> distributedCacheAction, Func<BackplaneAccessor, bool, CancellationToken, bool> backplaneAction, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (RequiresDistributedOperations(options) == false)
		{
			return;
		}

		var mustAwaitCompletion = MustAwaitDistributedOperations(options);
		var isBackground = !mustAwaitCompletion;

		RunUtils.RunSyncActionAdvanced(
			ct1 =>
			{
				// DISTRIBUTED CACHE
				var dca = DistributedCacheAccessor;
				if (dca.ShouldWrite(options))
				{
					var dcaSuccess = false;
					try
					{
						if (dca!.IsCurrentlyUsable(operationId, key))
						{
							dcaSuccess = distributedCacheAction(dca, isBackground, ct1);
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

				RunUtils.RunSyncActionAdvanced(
					ct2 =>
					{
						// BACKPLANE
						var bpa = BackplaneAccessor;
						if (bpa.ShouldWrite(options))
						{
							var bpaSuccess = false;
							try
							{
								if (bpa!.IsCurrentlyUsable(operationId, key))
								{
									bpaSuccess = backplaneAction(bpa, isBackplaneBackground, ct2);
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
				);
			},
			Timeout.InfiniteTimeSpan,
			false,
			mustAwaitCompletion,
			null,
			true,
			token
		);
	}

	private void DistributedSetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, CancellationToken token)
	{
		ExecuteDistributedAction(
			operationId,
			key,
			FusionCacheAction.EntrySet,
			entry.Timestamp,
			(dca, isBackground, ct) =>
			{
				return dca!.SetEntry<TValue>(operationId, key, entry, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishSet(operationId, key, entry.Timestamp, options, false, isBackground, ct);
			},
			options,
			token
		);
	}

	private void DistributedRemoveEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
	{
		var now = FusionCacheInternalUtils.GetCurrentTimestamp();
		ExecuteDistributedAction(
			operationId,
			key,
			FusionCacheAction.EntryRemove,
			now,
			(dca, isBackground, ct) =>
			{
				return dca.RemoveEntry(operationId, key, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishRemove(operationId, key, now, options, false, isBackground, ct);
			},
			options,
			token
		);
	}

	private void DistributedExpireEntry(string operationId, string key, FusionCacheEntryOptions options, CancellationToken token)
	{
		var now = FusionCacheInternalUtils.GetCurrentTimestamp();
		ExecuteDistributedAction(
			operationId,
			key,
			FusionCacheAction.EntryExpire,
			now,
			(dca, isBackground, ct) =>
			{
				if (options.IsFailSafeEnabled)
				{
					return true;
				}
				
				return dca.RemoveEntry(operationId, key, options, isBackground, ct);
			},
			(bpa, isBackground, ct) =>
			{
				return bpa.PublishExpire(operationId, key, now, options, false, isBackground, ct);
			},
			options,
			token
		);
	}

	// INTERNAL STUFF

	internal (bool error, bool isSame, bool hasUpdated) TryUpdateMemoryEntryFromDistributedEntry<TValue>(string operationId, string key, FusionCacheMemoryEntry<TValue> memoryEntry)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to update memory entry from distributed entry", CacheName, InstanceId, operationId, key);

		try
		{
			if (HasDistributedCache == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because there's no distributed cache", CacheName, InstanceId, operationId, key);

				return (false, false, false);
			}

			var dca = DistributedCacheAccessor;

			if (dca is null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because distributed cache is not enabled for the current operation", CacheName, InstanceId, operationId, key);

				return (false, false, false);
			}

			if (dca.IsCurrentlyUsable(operationId, key) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because distributed cache is not currently usable", CacheName, InstanceId, operationId, key);

				return (true, false, false);
			}

			try
			{
				var (distributedEntry, isValid) = dca.TryGetEntry<TValue>(operationId, key, _tryUpdateEntryOptions, false, Timeout.InfiniteTimeSpan, default);

				if (distributedEntry is null || isValid == false)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): distributed entry not found or stale, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, false, false);
				}

				if (distributedEntry.Timestamp == memoryEntry.Timestamp)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry same as distributed entry, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, true, false);
				}

				if (distributedEntry.Timestamp < memoryEntry.Timestamp)
				{
					//return;
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry more fresh than distributed entry, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, false, false);
				}

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): updating memory entry from distributed entry", CacheName, InstanceId, operationId, key);


				_mca.UpdateEntryFromDistributedEntry(operationId, key, memoryEntry, distributedEntry);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry updated from distributed", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.Memory.OnSet(operationId, key);

				return (false, false, true);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to update a memory entry from a distributed entry", CacheName, InstanceId, operationId, key);

				return (true, false, false);
			}
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling TryUpdateMemoryEntryFromDistributedEntryUntypedAsync() to try to update a memory entry from a distributed entry without knowing the TValue type", CacheName, InstanceId, operationId, key);

			return (true, false, false);
		}
	}
}
