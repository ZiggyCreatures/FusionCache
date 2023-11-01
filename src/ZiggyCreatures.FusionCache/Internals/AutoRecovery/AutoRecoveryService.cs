﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.AutoRecovery;

internal sealed class AutoRecoveryService
	: IDisposable
{
	private readonly FusionCache _cache;
	private readonly FusionCacheOptions _options;
	private readonly ILogger<FusionCache>? _logger;

	private readonly ConcurrentDictionary<string, AutoRecoveryItem> _queue = new ConcurrentDictionary<string, AutoRecoveryItem>();
	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
	private readonly int _maxItems;
	private readonly int _maxRetryCount;
	private readonly TimeSpan _delay;
	private static readonly TimeSpan _minDelay = TimeSpan.FromMilliseconds(10);
	private CancellationTokenSource? _cts;
	private long _barrierTicks = 0;

	public AutoRecoveryService(FusionCache cache, FusionCacheOptions options, ILogger<FusionCache>? logger)
	{
		_cache = cache;
		_options = options;
		_logger = logger;

		_delay = _options.AutoRecoveryDelay;
		// NOTE: THIS IS PRAGMATIC, SO TO AVOID CHECKING AN int? EVERY TIME, AND int.MaxValue IS HIGH ENOUGH THAT IT WON'T MATTER
		_maxItems = _options.AutoRecoveryMaxItems ?? int.MaxValue;
		_maxRetryCount = _options.AutoRecoveryMaxRetryCount ?? int.MaxValue;

		// AUTO-RECOVERY
		if (_options.EnableAutoRecovery)
		{
			if (_delay <= TimeSpan.Zero)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery is enabled but cannot be started because the AutoRecoveryDelay has been set to zero", _cache.CacheName, _cache.InstanceId, FusionCacheInternalUtils.MaybeGenerateOperationId(_logger));
			}
			else
			{
				_cts = new CancellationTokenSource();
				_ = BackgroundJobAsync();
			}
		}
	}

	internal bool TryAddItem(string? operationId, string? cacheKey, FusionCacheAction action, long timestamp, FusionCacheEntryOptions options)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (_cache.RequiresDistributedOperations(options) == false)
			return false;

		if (cacheKey is null)
			return false;

		if (action == FusionCacheAction.Unknown)
			return false;

		options = options.Duplicate();

		// DISTRIBUTED CACHE
		if (options.SkipDistributedCache == false)
		{
			options.AllowBackgroundDistributedCacheOperations = false;
			options.DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan;
			options.DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan;
			options.ReThrowDistributedCacheExceptions = true;
			options.ReThrowSerializationExceptions = true;
			options.SkipDistributedCacheReadWhenStale = false;
		}

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
		{
			options.AllowBackgroundBackplaneOperations = false;
			options.ReThrowBackplaneExceptions = true;
		}

		var duration = (options.SkipDistributedCache || _cache.HasDistributedCache == false) ? options.Duration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration);
		var expirationTicks = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(duration, options, false).Ticks;

		if (_queue.Count >= _maxItems && _queue.ContainsKey(cacheKey) == false)
		{
			// IF:
			// - A LIMIT HAS BEEN SET
			// - THE LIMIT HAS BEEN REACHED OR SURPASSED
			// - THE ITEM TO BE ADDED IS NOT ALREADY THERE (OTHERWISE IT WILL BE AN OVERWRITE AND SIZE WILL NOT GROW)
			// THEN:
			// - FIND THE ITEM THAT WILL EXPIRE SOONER AND REMOVE IT
			// - OR, IF NEW ITEM WILL EXPIRE SOONER, DO NOT ADD IT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the auto-recovery queue has reached the max size of {MaxSize}", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, _maxItems);

			try
			{
				var earliestToExpire = _queue.Values.ToArray().Where(x => x.ExpirationTicks is not null).OrderBy(x => x.ExpirationTicks).FirstOrDefault();
				if (earliestToExpire is not null)
				{
					if (earliestToExpire.ExpirationTicks < expirationTicks)
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an item with cache key {CacheKeyToRemove} has been removed from the auto-recovery queue to make space for the new one", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, earliestToExpire.CacheKey);

						// REMOVE THE QUEUED ITEM
						TryRemoveItem(operationId, earliestToExpire);
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the item has not been added to the auto-recovery queue because it would have expired earlier than the earliest item already present in the queue (with cache key {CacheKeyEarliest})", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, earliestToExpire.CacheKey);

						// IGNORE THE NEW ITEM
						return false;
					}
				}
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while deciding which item in the auto-recovery queue to remove to make space for a new one", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);
			}
		}

		_queue[cacheKey] = new AutoRecoveryItem(cacheKey, action, timestamp, options, expirationTicks, _maxRetryCount);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): added (or overwrote) an item to the auto-recovery queue", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		return true;
	}

	internal bool TryRemoveItemByCacheKey(string? operationId, string cacheKey)
	{
		if (cacheKey is null)
			return false;

		if (_queue.TryRemove(cacheKey, out _) == false)
			return false;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the auto-recovery queue", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

		return true;
	}

	internal bool TryRemoveItem(string? operationId, AutoRecoveryItem item)
	{
		if (item is null)
			return false;

		if (item.CacheKey is null)
			return false;

		if (_queue.TryGetValue(item.CacheKey, out var pendingLocal) == false)
			return false;

		// NOTE: HERE WE SHOULD USE THE NEW OVERLOAD TryRemove(KeyValuePair<TKey,TValue>) BUT THAT IS NOT AVAILABLE UNTIL .NET 5
		// SO WE DO THE NEXT BEST THING WE CAN: TRY TO GET THE VALUE AND, IF IT IS THE SAME AS THE ONE WE HAVE, THEN REMOVE IT
		// OTHERWISE SKIP THE REMOVAL
		//
		// SEE: https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.tryremove?view=net-7.0#system-collections-concurrent-concurrentdictionary-2-tryremove(system-collections-generic-keyvaluepair((-0-1)))

		if (ReferenceEquals(item, pendingLocal) == false)
			return false;

		if (_queue.TryRemove(item.CacheKey, out _) == false)
			return false;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the auto-recovery queue", _cache.CacheName, _cache.InstanceId, operationId, item.CacheKey);

		return true;
	}

	internal bool TryCleanUpQueue(string operationId, IList<AutoRecoveryItem> items)
	{
		if (items.Count == 0)
			return false;

		var atLeastOneRemoved = false;

		// NOTE: WE USE THE REVERSE ITERATION TRICK TO AVOID PROBLEMS WITH REMOVING ITEMS WHILE ITERATING
		for (int i = items.Count - 1; i >= 0; i--)
		{
			var item = items[i];
			// IF THE ITEM IS SINCE EXPIRED -> REMOVE IT FROM THE QUEUE *AND* FROM THE LIST
			if (item.IsExpired())
			{
				TryRemoveItem(operationId, item);
				items.RemoveAt(i);
				atLeastOneRemoved = true;

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): auto-cleanup of auto-recovery item", _cache.CacheName, _cache.InstanceId, operationId, item.CacheKey);
			}
		}

		return atLeastOneRemoved;
	}

	internal bool CheckIncomingMessageForConflicts(string operationId, BackplaneMessage message)
	{
		if (message.CacheKey is null)
		{
			return true;
		}

		if (_queue.TryGetValue(message.CacheKey, out var pendingLocal) == false)
		{
			// NO PENDING LOCAL MESSAGE WITH THE SAME KEY
			return true;
		}

		if (pendingLocal.Timestamp <= message.Timestamp)
		{
			// PENDING LOCAL MESSAGE IS -OLDER- THAN THE INCOMING ONE -> REMOVE THE LOCAL ONE
			TryRemoveItem(operationId, pendingLocal);
			return true;
		}

		// PENDING LOCAL MESSAGE IS -NEWER- THAN THE INCOMING ONE -> DO NOT PROCESS THE INCOMING ONE
		return false;
	}

	internal bool TryUpdateBarrier(string operationId)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (_queue.Count == 0)
			return false;

		var newBarrier = DateTimeOffset.UtcNow.Ticks + _delay.Ticks;
		var oldBarrier = Interlocked.Exchange(ref _barrierTicks, newBarrier);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery barrier set from {OldAutoRecoveryBarrier} to {NewAutoRecoveryBarrier}", _cache.CacheName, _cache.InstanceId, operationId, oldBarrier, newBarrier);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting at least {AutoRecoveryDelay} to start auto-recovery to let the other nodes reconnect, to better handle backpressure", _cache.CacheName, _cache.InstanceId, operationId, _delay);

		return true;
	}

	internal async ValueTask<bool> TryProcessQueueAsync(string operationId, CancellationToken token)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (_queue.Count == 0)
			return false;

		// ACQUIRE THE LOCK
		if (_lock.Wait(0) == false)
		{
			// IF THE LOCK HAS NOT BEEN ACQUIRED IMMEDIATELY -> PROCESSING IS ALREADY ONGOING, SO WE JUST RETURN
			return false;
		}

		// SNAPSHOT THE ITEMS TO PROCESS
		var itemsToProcess = _queue.Values.ToList();

		// INITIAL CLEANUP
		_ = TryCleanUpQueue(operationId, itemsToProcess);

		// IF NO REMAINING ITEMS -> JUST RELEASE THE LOCK AND RETURN
		if (itemsToProcess.Count == 0)
		{
			_lock.Release();
			return false;
		}

		var processedCount = 0;
		var hasStopped = false;
		AutoRecoveryItem? lastProcessedItem = null;

		try
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): starting auto-recovery of {Count} pending items", _cache.CacheName, _cache.InstanceId, operationId, itemsToProcess.Count);

			foreach (var item in itemsToProcess)
			{
				processedCount++;

				token.ThrowIfCancellationRequested();

				lastProcessedItem = item;

				var success = false;

				switch (item.Action)
				{
					case FusionCacheAction.EntrySet:
						success = await TryProcessItemSetAsync(operationId, item, token).ConfigureAwait(false);
						break;
					case FusionCacheAction.EntryRemove:
						success = await TryProcessItemRemoveAsync(operationId, item, token).ConfigureAwait(false);
						break;
					case FusionCacheAction.EntryExpire:
						success = await TryProcessItemExpireAsync(operationId, item, token).ConfigureAwait(false);
						break;
					default:
						success = true;
						break;
				}

				if (success)
				{
					TryRemoveItem(operationId, item);
				}
				else
				{
					hasStopped = true;
					return false;
				}
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): completed auto-recovery of {Count} items", _cache.CacheName, _cache.InstanceId, operationId, processedCount);
		}
		catch (OperationCanceledException)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery canceled after having processed {Count} items", _cache.CacheName, _cache.InstanceId, operationId, processedCount);
		}
		catch (Exception exc)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred during a auto-recovery of an item ({RetryCount} retries left)", _cache.CacheName, _cache.InstanceId, operationId, lastProcessedItem?.CacheKey, lastProcessedItem?.RetryCount);
		}
		finally
		{
			if (hasStopped)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): stopped auto-recovery because of an error after {Count} processed items", _cache.CacheName, _cache.InstanceId, operationId, lastProcessedItem?.CacheKey, processedCount);

				if (lastProcessedItem is not null)
				{
					// UPDATE RETRY COUNT
					lastProcessedItem.RecordRetry();

					if (lastProcessedItem.CanRetry() == false)
					{
						TryRemoveItem(operationId, lastProcessedItem);

						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a auto-recovery item retried too many times, so it has been removed from the queue", _cache.CacheName, _cache.InstanceId, operationId, lastProcessedItem?.CacheKey);
					}
				}
			}

			// RELEASE THE LOCK
			_lock.Release();
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessItemSetAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = _cache.GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			if (dca.IsCurrentlyUsable(operationId, item.CacheKey) == false)
			{
				return false;
			}

			// TRY TO GET THE MEMORY CACHE
			var mca = _cache.GetCurrentMemoryAccessor(item.Options);

			if (mca is not null)
			{
				// TRY TO GET THE MEMORY ENTRY
				var memoryEntry = mca.GetEntryOrNull(operationId, item.CacheKey);

				if (memoryEntry is not null)
				{
					try
					{
						(var error, var isSame, var hasUpdated) = await _cache.TryUpdateMemoryEntryFromDistributedEntryUntypedAsync(operationId, item.CacheKey, memoryEntry).ConfigureAwait(false);

						if (error)
						{
							// STOP PROCESSING THE QUEUE
							return false;
						}

						if (hasUpdated)
						{
							// IF THE MEMORY ENTRY HAS BEEN UPDATED FROM THE DISTRIBUTED ENTRY, IT MEANS THAT THE DISTRIBUTED ENTRY
							// IS NEWER THAN THE MEMORY ENTRY, BECAUSE IT HAS BEEN UPDATED SINCE WE SET IT LOCALLY AND NOW IT'S
							// NEWER -> STOP HERE, ALL IS GOOD
							return true;
						}

						if (isSame == false)
						{
							// IF THE MEMORY ENTRY IS ALSO NOT THE SAME AS THE DISTRIBUTED ENTRY, IT MEANS THAT THE DISTRIBUTED ENTRY
							// IS EITHER OLDER OR IT'S NOT THERE AT ALL -> WE SET IT TO THE CURRENT ONE

							var dcaSuccess = await dca.SetEntryUntypedAsync(operationId, item.CacheKey, memoryEntry, item.Options, true, token).ConfigureAwait(false);
							if (dcaSuccess == false)
							{
								// STOP PROCESSING THE QUEUE
								return false;
							}
						}
					}
					catch
					{
						return false;
					}
				}
			}
		}

		// BACKPLANE
		var bpa = _cache.GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishSetAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessItemRemoveAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = _cache.GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			var dcaSuccess = false;
			try
			{
				if (dca.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					dcaSuccess = await dca.RemoveEntryAsync(operationId, item.CacheKey, item.Options, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				dcaSuccess = false;
			}

			if (dcaSuccess == false)
			{
				return false;
			}
		}

		// BACKPLANE
		var bpa = _cache.GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishRemoveAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessItemExpireAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = _cache.GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			var dcaSuccess = false;
			try
			{
				if (dca.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					dcaSuccess = await dca.RemoveEntryAsync(operationId, item.CacheKey, item.Options, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				dcaSuccess = false;
			}

			if (dcaSuccess == false)
			{
				return false;
			}
		}

		// BACKPLANE
		var bpa = _cache.GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishExpireAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async Task BackgroundJobAsync()
	{
		if (_cts is null)
			return;

		try
		{
			var ct = _cts.Token;
			while (!ct.IsCancellationRequested)
			{
				var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
				var delay = _delay;
				var nowTicks = DateTimeOffset.UtcNow.Ticks;
				var barrierTicks = Interlocked.Read(ref _barrierTicks);
				if (nowTicks < barrierTicks)
				{
					// SET THE NEW DELAY TO REACH THE BARRIER (+ A MICROSCOPIC EXTRA)
					var oldDelay = delay;
					var newDelayTicks = barrierTicks - nowTicks + 1_000;
					delay = TimeSpan.FromTicks(newDelayTicks);

					// CHECK IF THE NEW DELAY IS BELOW A SAFETY LIMIT
					if (delay < _minDelay)
					{
						delay = _minDelay;
						newDelayTicks = delay.Ticks;
					}

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): instead of the standard auto-recovery delay of {AutoRecoveryNormalDelay} the new delay is {AutoRecoveryNewDelay} ({AutoRecoveryNewDelayMs} ms, {AutoRecoveryNewDelayTicks} ticks)", _cache.CacheName, _cache.InstanceId, operationId, oldDelay, delay, delay.TotalMilliseconds, newDelayTicks);
				}

				if (_queue.Count > 0)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting {AutoRecoveryCurrentDelay} before the next try of auto-recovery", _cache.CacheName, _cache.InstanceId, operationId, delay);
				}

				await Task.Delay(delay, ct).ConfigureAwait(false);

				// AFTER THE DELAY, READ THE BARRIER AGAIN, IN CASE IT HAS BEEN MODIFIED WHILE WAITING
				barrierTicks = Interlocked.Read(ref _barrierTicks);

				// CHECK AGAIN THE BARRIER (MAY HAVE BEEN UPDATED WHILE WAITING): IF UPDATED -> SKIP TO THE NEXT LOOP CYCLE
				if (DateTimeOffset.UtcNow.Ticks < barrierTicks)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a barrier has been set after having awaited to start processing the auto-recovery queue: skipping to the next loop cycle", _cache.CacheName, _cache.InstanceId, operationId);

					continue;
				}

				ct.ThrowIfCancellationRequested();

				if (_queue.Count > 0)
				{
					_ = await TryProcessQueueAsync(operationId, ct).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// EMPTY
		}
	}

	// IDISPOSABLE
	private bool disposedValue;
	private void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				_queue.Clear();
				_cts?.Cancel();
				_cts = null;
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}