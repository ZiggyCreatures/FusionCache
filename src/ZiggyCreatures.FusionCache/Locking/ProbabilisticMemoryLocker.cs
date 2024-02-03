﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking;

/// <summary>
/// An implementation of <see cref="IFusionCacheMemoryLocker"/> based on a probabilistic approach.
/// <br></br>
/// ⚠️ WARNING: this type of locker may lead to deadlocks, so be careful.
/// </summary>
internal sealed class ProbabilisticMemoryLocker
	: IFusionCacheMemoryLocker
{
	private readonly int _poolSize;
	private SemaphoreSlim[] _pool;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProbabilisticMemoryLocker"/> class.
	/// </summary>
	/// <param name="poolSize">The size of the pool used internally.</param>
	public ProbabilisticMemoryLocker(int poolSize = 8_440)
	{
		_poolSize = poolSize;
		_pool = new SemaphoreSlim[_poolSize];
		for (var i = 0; i < _pool.Length; i++)
		{
			_pool[i] = new SemaphoreSlim(1, 1);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetLockIndex(string key)
	{
		return unchecked((uint)key.GetHashCode()) % (uint)_poolSize;
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var idx = GetLockIndex(key);
		var semaphore = _pool[idx];

		//if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to fast-acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		//var acquired = semaphore.Wait(0);
		//if (acquired)
		//{
		//	_lockPoolKeys[idx] = key;
		//	if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//		logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK fast-acquired", cacheName, cacheInstanceId, operationId, key);

		//	return semaphore;
		//}

		//if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK already taken", cacheName, cacheInstanceId, operationId, key);

		//var key2 = _lockPoolKeys[idx];
		//if (key2 != key)
		//{
		//	if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//		logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", cacheName, cacheInstanceId, operationId, key);

		//	Interlocked.Increment(ref _lockPoolCollisions);
		//}

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		//_lockPoolKeys[idx] = key;

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, cacheInstanceId, operationId, key);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var idx = GetLockIndex(key);
		var semaphore = _pool[idx];

		//if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to fast-acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		//var acquired = semaphore.Wait(0);
		//if (acquired)
		//{
		//	_lockPoolKeys[idx] = key;
		//	if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//		logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK fast-acquired", cacheName, cacheInstanceId, operationId, key);

		//	return semaphore;
		//}

		//if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK already taken", cacheName, cacheInstanceId, operationId, key);

		//var key2 = _lockPoolKeys[idx];
		//if (key2 != key)
		//{
		//	if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//		logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", cacheName, cacheInstanceId, operationId, key);

		//	Interlocked.Increment(ref _lockPoolCollisions);
		//}

		//if (logger?.IsEnabled(LogLevel.Trace) ?? false)
		//	logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		var acquired = semaphore.Wait(timeout, token);

		//_lockPoolKeys[idx] = key;

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, cacheInstanceId, operationId, key);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string key, string operationId, object? lockObj, ILogger? logger)
	{
		if (lockObj is null)
			return;

		//var idx = GetLockIndex(key);
		//_lockPoolKeys[idx] = null;

		try
		{
			((SemaphoreSlim)lockObj).Release();
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the memory locker", cacheName, cacheInstanceId, operationId, key);
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
				if (_pool is not null)
				{
					foreach (var semaphore in _pool)
					{
						semaphore.Dispose();
					}
				}
			}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_pool = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
