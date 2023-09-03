using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal sealed partial class BackplaneAccessor
{
	private readonly FusionCache _cache;
	private readonly IFusionCacheBackplane _backplane;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheBackplaneEventsHub _events;
	private readonly SimpleCircuitBreaker _breaker;

	// AUTO-RECOVERY
	private readonly ConcurrentDictionary<string, BackplaneAutoRecoveryItem> _autoRecoveryQueue = new ConcurrentDictionary<string, BackplaneAutoRecoveryItem>();
	private readonly SemaphoreSlim _autoRecoveryProcessingLock = new SemaphoreSlim(1, 1);
	private readonly FusionCacheEntryOptions _autoRecoveryDistributedCacheEntryOptions;
	private readonly FusionCacheEntryOptions _autoRecoverySentinelEntryOptions;
	private readonly int _autoRecoveryMaxItems;
	private readonly int _autoRecoveryMaxRetryCount;
	private readonly TimeSpan _autoRecoveryDelay;
	private CancellationTokenSource? _autoRecoveryCts;
	private long _autoRecoveryBarrierTicks = 0;
	private readonly string _autoRecoverySentinelCacheKey = Guid.NewGuid().ToString("N");

	public BackplaneAccessor(FusionCache cache, IFusionCacheBackplane backplane, FusionCacheOptions options, ILogger? logger, FusionCacheBackplaneEventsHub events)
	{
		if (cache is null)
			throw new ArgumentNullException(nameof(cache));

		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		_cache = cache;
		_backplane = backplane;

		_options = options;
		_autoRecoveryDelay = _options.BackplaneAutoRecoveryDelay;
		// NOTE: THIS IS PRAGMATIC, SO TO AVOID CHECKING AN int? EVERY TIME, AND int.MaxValue IS HIGH ENOUGH THAT IT WON'T MATTER
		_autoRecoveryMaxItems = _options.BackplaneAutoRecoveryMaxItems ?? int.MaxValue;
		_autoRecoveryMaxRetryCount = _options.BackplaneAutoRecoveryMaxRetryCount ?? int.MaxValue;

		_logger = logger;
		_events = events;

		// CIRCUIT-BREAKER
		_breaker = new SimpleCircuitBreaker(options.BackplaneCircuitBreakerDuration);

		// AUTO-RECOVERY SPECIFIC ENTRY OPTIONS
		_autoRecoveryDistributedCacheEntryOptions = new FusionCacheEntryOptions
		{
			// MEMORY CACHE
			SkipMemoryCache = true,
			// DISTRIBUTED CACHE
			SkipDistributedCache = false,
			DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
			DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
			AllowBackgroundDistributedCacheOperations = false,
			ReThrowDistributedCacheExceptions = true,
			SkipDistributedCacheReadWhenStale = false,
			// BACKPLANE
			SkipBackplaneNotifications = true,
			AllowBackgroundBackplaneOperations = false,
			ReThrowBackplaneExceptions = true
		};

		_autoRecoverySentinelEntryOptions = new FusionCacheEntryOptions
		{
			// MEMORY CACHE
			SkipMemoryCache = true,
			// DISTRIBUTED CACHE
			SkipDistributedCache = false,
			DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
			DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
			AllowBackgroundDistributedCacheOperations = false,
			ReThrowDistributedCacheExceptions = true,
			SkipDistributedCacheReadWhenStale = false,
			// BACKPLANE
			SkipBackplaneNotifications = false,
			AllowBackgroundBackplaneOperations = false,
			ReThrowBackplaneExceptions = true
		};
	}

	private void UpdateLastError(string key, string operationId)
	{
		// NO DISTRIBUTEC CACHE
		if (_backplane is null)
			return;

		var res = _breaker.TryOpen(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): backplane temporarily de-activated for {BreakDuration}", _cache.CacheName, _cache.InstanceId, operationId, key, _breaker.BreakDuration);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, false);
		}
	}

	public bool IsCurrentlyUsable(string? operationId, string? key)
	{
		var res = _breaker.IsClosed(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): backplane activated again", _cache.CacheName, _cache.InstanceId, operationId, key);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, true);
		}

		return res;
	}

	private void ProcessError(string operationId, string key, Exception exc, string actionDescription)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.BackplaneSyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.BackplaneSyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);

			return;
		}

		UpdateLastError(key, operationId);

		if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
			_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, _cache.CacheName, _cache.InstanceId, operationId, key);
	}

	public bool TryAddAutoRecoveryItem(string? operationId, BackplaneMessage message, FusionCacheEntryOptions options, bool preSyncDistributedCache)
	{
		if (message.CacheKey is null)
			return false;

		if (_options.EnableBackplaneAutoRecovery == false)
			return false;

		options = options.Duplicate();
		// DISTRIBUTED CACHE
		if (options.SkipDistributedCache == false)
		{
			options.AllowBackgroundDistributedCacheOperations = false;
			options.DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan;
			options.DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan;
			options.ReThrowDistributedCacheExceptions = true;
			options.SkipDistributedCacheReadWhenStale = false;
		}
		// BACKPLANE
		options.SkipBackplaneNotifications = true;
		options.AllowBackgroundBackplaneOperations = false;
		options.ReThrowBackplaneExceptions = true;

		var duration = (options.SkipDistributedCache || _cache.HasDistributedCache == false) ? options.Duration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration);
		var expirationTicks = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(duration, options, false).Ticks;

		if (_autoRecoveryQueue.Count >= _autoRecoveryMaxItems && _autoRecoveryQueue.ContainsKey(message.CacheKey) == false)
		{
			// IF:
			// - A LIMIT HAS BEEN SET
			// - THE LIMIT HAS BEEN REACHED OR SURPASSED
			// - THE ITEM TO BE ADDED IS NOT ALREADY THERE (OTHERWISE IT WILL BE AN OVERWRITE AND SIZE WILL NOT GROW)
			// THEN:
			// - FIND THE ITEM THAT WILL EXPIRE SOONER AND REMOVE IT
			// - OR, IF NEW ITEM WILL EXPIRE SOONER, DO NOT ADD IT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the auto-recovery queue has reached the max size of {MaxSize}", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey, _autoRecoveryMaxItems);

			try
			{
				var earliestToExpire = _autoRecoveryQueue.Values.OrderBy(x => x.ExpirationTicks).FirstOrDefault();
				if (earliestToExpire.Message is not null)
				{
					if (earliestToExpire.ExpirationTicks < expirationTicks)
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an item with cache key {CacheKeyToRemove} has been removed from the backplane auto-recovery queue to make space for the new one", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey, earliestToExpire.Message.CacheKey);

						// REMOVE THE QUEUED ITEM
						_autoRecoveryQueue.TryRemove(earliestToExpire.Message.CacheKey!, out _);
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the item has not been added to the backplane auto-recovery queue because it would have expired earlier than the earliest item already present in the queue (with cache key {CacheKeyEarliest})", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey, earliestToExpire.Message.CacheKey);

						// IGNORE THE NEW ITEM
						return false;
					}
				}
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while deciding which item in the backplane auto-recovery queue to remove to make space for a new one", _cache.CacheName, _cache.InstanceId, operationId, message?.CacheKey);
			}
		}

		if (message is null)
			return false;

		_autoRecoveryQueue[message.CacheKey] = new BackplaneAutoRecoveryItem(message, options, expirationTicks, preSyncDistributedCache, _autoRecoveryMaxRetryCount);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): added (or overwrote) an item to the backplane auto-recovery queue", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

		return true;
	}

	private bool TryRemoveAutoRecoveryItemByCacheKey(string? operationId, string? cacheKey)
	{
		if (cacheKey is null)
			return false;

		if (_autoRecoveryQueue.ContainsKey(cacheKey) == false)
			return false;

		if (_autoRecoveryQueue.TryRemove(cacheKey, out _))
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the backplane auto-recovery queue because a new one is about to be sent", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

			return true;
		}

		return false;
	}

	private bool CheckIncomingMessageForAutoRecoveryConflicts(BackplaneMessage message)
	{
		if (message.CacheKey is null)
		{
			return true;
		}

		if (_autoRecoveryQueue.TryGetValue(message.CacheKey, out var pendingLocal) == false)
		{
			// NO PENDING LOCAL MESSAGE WITH THE SAME KEY
			return true;
		}

		if (pendingLocal.Message.InstantTicks <= message.InstantTicks)
		{
			// PENDING LOCAL MESSAGE IS -OLDER- THAN THE INCOMING ONE -> REMOVE THE LOCAL ONE
			_autoRecoveryQueue.TryRemove(message.CacheKey, out _);
			return true;
		}

		// PENDING LOCAL MESSAGE IS -NEWER- THAN THE INCOMING ONE -> DO NOT PROCESS THE INCOMING ONE
		return false;
	}

	private bool TryCleanUpAutoRecoveryQueue(string operationId, IList<BackplaneAutoRecoveryItem> items)
	{
		if (items.Count == 0)
			return false;

		var atLeastOneRemoved = false;
		for (int i = items.Count - 1; i >= 0; i--)
		{
			var item = items[i];
			// IF THE ITEM IS SINCE EXPIRED -> REMOVE IT FROM THE QUEUE *AND* FROM THE LIST
			if (item.IsExpired())
			{
				_autoRecoveryQueue.TryRemove(item.Message.CacheKey!, out _);
				items.RemoveAt(i);
				atLeastOneRemoved = true;

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): auto cleanup of backplane auto-recovery item", _cache.CacheName, _cache.InstanceId, operationId, item.Message.CacheKey);
			}
		}

		return atLeastOneRemoved;
	}

	private async ValueTask<bool> TryProcessAutoRecoveryQueueAsync(string operationId, CancellationToken token = default)
	{
		if (_options.EnableBackplaneAutoRecovery == false)
			return false;

		if (IsCurrentlyUsable(null, null) == false)
			return false;

		if (_autoRecoveryQueue.Count == 0)
			return false;

		// ACQUIRE THE LOCK
		if (_autoRecoveryProcessingLock.Wait(0) == false)
		{
			// IF THE LOCK HAS NOT BEEN ACQUIRED IMMEDIATELY PROCESSING IS ALREADY ONGOING, SO WE JUST RETURN
			return false;
		}

		// SNAPSHOT THE ITEMS TO PROCESS
		var itemsToProcess = _autoRecoveryQueue.Values.ToList();

		// INITIAL CLEANUP
		TryCleanUpAutoRecoveryQueue(operationId, itemsToProcess);

		// IF NO REMAINING ITEMS -> JUST RELEASE THE LOCK AND RETURN
		if (itemsToProcess.Count == 0)
		{
			_autoRecoveryProcessingLock.Release();
			return false;
		}

		var processedCount = 0;
		var hasStopped = false;
		string? cacheKey = null;

		try
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): starting backplane auto-recovery of {Count} pending items", _cache.CacheName, _cache.InstanceId, operationId, itemsToProcess.Count);

			// PUBLISH (SENTINEL)
			await PublishAsync(operationId, BackplaneMessage.CreateForEntrySentinel(_autoRecoverySentinelCacheKey), _autoRecoverySentinelEntryOptions, true, true, token).ConfigureAwait(false);

			foreach (var item in itemsToProcess)
			{
				processedCount++;
				cacheKey = item.Message.CacheKey!;

				token.ThrowIfCancellationRequested();

				try
				{
					// IF:
					// - THERE IS A DISTRIBUTED CACHE
					// - AND THE ITEM REQUIRES A PRE-SYNC WITH THE DISTRIBUTED CACHE
					// - AND SkipDistributedCache IS NOT ENABLED
					// THEN:
					// - REMOVE THE ENTRY (BUT ONLY FROM THE DISTRIBUTED CACHE)
					if (_cache.HasDistributedCache && item.PreSyncDistributedCache && item.Options.SkipDistributedCache == false)
					{
						var dca = _cache.GetCurrentDistributedAccessor(_autoRecoveryDistributedCacheEntryOptions);
						if (dca.CanBeUsed(operationId, cacheKey) == false)
						{
							if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
								_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): during backplane auto-recovery of an item, the distributed cache was necessary (because of the EnableDistributedExpireOnBackplaneAutoRecovery option) but was not available", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

							// STOP PROCESSING THE QUEUE
							hasStopped = true;
							return false;
						}

						var hasRemoved = await dca!.RemoveEntryAsync(operationId, cacheKey, _autoRecoveryDistributedCacheEntryOptions, token).ConfigureAwait(false);
						if (hasRemoved == false)
						{
							// STOP PROCESSING THE QUEUE
							hasStopped = true;
							return false;
						}
					}

					// PUBLISH
					await PublishAsync(operationId, item.Message, item.Options, item.PreSyncDistributedCache == false, true, token).ConfigureAwait(false);

					// IF ALL WENT WELL -> REMOVE ITEM FROM THE QUEUE
					_autoRecoveryQueue.TryRemove(cacheKey, out _);
				}
				catch (OperationCanceledException)
				{
					// EMPTY
					return false;
				}
				catch (Exception exc)
				{
					// UPDATE RETRY COUNT
					item.RecordRetry();

					if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
						_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred during backplane auto-recovery of an item ({RetryCount} retries left)", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, item.RetryCount);

					if (item.CanRetry() == false)
					{
						_autoRecoveryQueue.TryRemove(cacheKey, out var removedMessage);

						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane auto-recovery item retried too many times, so it has been removed from the queue", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
					}

					// STOP PROCESSING THE QUEUE
					hasStopped = true;
					return false;
				}
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): completed backplane auto-recovery of {Count} items", _cache.CacheName, _cache.InstanceId, operationId, processedCount);
		}
		catch (OperationCanceledException)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane auto-recovery canceled after having processed {Count} items", _cache.CacheName, _cache.InstanceId, operationId, processedCount);
		}
		catch (Exception exc)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred during backplane auto-recovery", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
		}
		finally
		{
			if (hasStopped)
			{
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): stopped backplane auto-recovery because of an error after {Count} processed items", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, processedCount);
			}

			// RELEASE THE LOCK
			_autoRecoveryProcessingLock.Release();
		}


		return true;
	}

	private async Task BackgroundAutoRecoveryAsync()
	{
		if (_autoRecoveryCts is null)
			return;

		try
		{
			var ct = _autoRecoveryCts.Token;
			while (!ct.IsCancellationRequested)
			{
				var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
				var delay = _autoRecoveryDelay;
				var barrierTicks = Interlocked.Read(ref _autoRecoveryBarrierTicks);
				if (DateTimeOffset.UtcNow.Ticks < barrierTicks)
				{
					// IF THERE'S AN ACTIVE BARRIER AND THE DELTA IS LOWER THAN THE NORMAL DELAY -> UPDATE THE DELAY
					var tmp = TimeSpan.FromTicks(barrierTicks - DateTimeOffset.UtcNow.Ticks + 1_000);
					if (tmp < delay)
					{
						delay = tmp;
					}
				}

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): awaiting {AutoRecoveryCurrentDelay} before proceeding with backplane auto-recovery", _cache.CacheName, _cache.InstanceId, operationId, delay);

				await Task.Delay(delay, ct);

				// AFTER THE DELAY, UPDATE THE BARRIER
				barrierTicks = Interlocked.Read(ref _autoRecoveryBarrierTicks);

				// CHECK AGAIN THE BARRIER (MAY HAVE BEEN UPDATED WHILE WAITING): IF UPDATED -> SKIP TO THE NEXT LOOP CYCLE
				if (DateTimeOffset.UtcNow.Ticks < barrierTicks)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): after having awaited to start processing the auto-recovery queue a barrier has been set, so we skip to the next loop cycle", _cache.CacheName, _cache.InstanceId, operationId);

					continue;
				}

				ct.ThrowIfCancellationRequested();

				if (_autoRecoveryQueue.Count > 0)
				{
					_ = await TryProcessAutoRecoveryQueueAsync(operationId, ct);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// EMPTY
		}
	}

	public void Subscribe()
	{
		try
		{
			_backplane.Subscribe(
				new BackplaneSubscriptionOptions(
					_options.GetBackplaneChannelName(),
					HandleConnect,
					HandleIncomingMessage
				)
			);

			// AUTO-RECOVERY
			if (_options.EnableBackplaneAutoRecovery)
			{
				if (_autoRecoveryDelay <= TimeSpan.Zero)
				{
					if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
						_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): backplane auto-recovery is enabled but cannot be started because the BackplaneAutoRecoveryDelay has been set to zero", _cache.CacheName, _cache.InstanceId, FusionCacheInternalUtils.MaybeGenerateOperationId(_logger), "", _backplane.GetType().FullName);
				}
				else
				{
					_autoRecoveryCts = new CancellationTokenSource();
					_ = BackgroundAutoRecoveryAsync();
				}
			}
		}
		catch (Exception exc)
		{
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

			ProcessError(operationId, "", exc, $"subscribing to a backplane of type {_backplane.GetType().FullName}");

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while subscribing to a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	public void Unsubscribe()
	{
		_autoRecoveryQueue.Clear();

		try
		{
			_autoRecoveryCts?.Cancel();
			_autoRecoveryCts = null;
		}
		catch (Exception)
		{
			throw;
		}

		try
		{
			_backplane.Unsubscribe();
		}
		catch (Exception exc)
		{
			var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

			ProcessError(operationId, "", exc, $"unsubscribing from a backplane of type {_backplane.GetType().FullName}");

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while unsubscribing from a backplane of type {BackplaneType}", _cache.CacheName, _cache.InstanceId, operationId, "", _backplane.GetType().FullName);
		}
	}

	private void HandleConnect(BackplaneConnectionInfo info)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane " + (info.IsReconnection ? "re-connected" : "connected"), _cache.CacheName, _cache.InstanceId, operationId);

		if (info.IsReconnection && _options.EnableBackplaneAutoRecovery)
		{
			if (_autoRecoveryQueue.Count == 0)
				return;

			_ = Interlocked.Exchange(ref _autoRecoveryBarrierTicks, DateTimeOffset.UtcNow.Ticks + _options.BackplaneAutoRecoveryDelay.Ticks);

			if (_logger?.IsEnabled(LogLevel.Information) ?? false)
				_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting at least {AutoRecoveryDelay} to start backplane auto-recovery to let the other nodes reconnect, to better handle backpressure", _cache.CacheName, _cache.InstanceId, operationId, _options.BackplaneAutoRecoveryDelay);
		}
	}

	private void HandleIncomingMessage(BackplaneMessage message)
	{
		var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);

		// CHECK CIRCUIT BREAKER
		_breaker.Close(out var hasChanged);
		if (hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): backplane activated again", _cache.CacheName, _cache.InstanceId, operationId);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, null, true);
		}

		// IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return;
		}

		// IGNORE MESSAGES FROM THIS SOURCE
		if (message.SourceId == _cache.InstanceId)
		{
			return;
		}

		// IGNORE INVALID MESSAGES
		if (message.IsValid() == false)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an invalid backplane notification has been received from remote cache {RemoteCacheInstanceId} (A={Action}, T={InstanceTicks})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action, message.InstantTicks);

			return;
		}

		// AUTO-RECOVERY
		if (_options.EnableBackplaneAutoRecovery)
		{
			if (CheckIncomingMessageForAutoRecoveryConflicts(message) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId}, but has been discarded since there is a pending one in the auto-recovery queue which is more recent", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				return;
			}
		}

		// PROCESS MESSAGE
		switch (message.Action)
		{
			case BackplaneMessageAction.EntrySet:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SET)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, true);
				break;
			case BackplaneMessageAction.EntryRemove:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (REMOVE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, false);
				break;
			case BackplaneMessageAction.EntryExpire:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (EXPIRE)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				_cache.ExpireMemoryEntryInternal(operationId, message.CacheKey!, true);
				break;
			case BackplaneMessageAction.EntrySentinel:
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a backplane notification has been received from remote cache {RemoteCacheInstanceId} (SENTINEL)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId);

				// DO NOTHING
				break;
			default:
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an backplane notification has been received from remote cache {RemoteCacheInstanceId} for an unknown action {Action}", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.SourceId, message.Action);
				break;
		}

		// EVENT
		_events.OnMessageReceived(operationId, message);
	}
}
