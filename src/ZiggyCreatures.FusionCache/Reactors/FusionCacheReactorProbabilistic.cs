using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors;

internal sealed class FusionCacheReactorProbabilistic
	: IFusionCacheReactor
{
	private readonly int _lockPoolSize;
	private SemaphoreSlim[] _lockPool;
	private readonly string?[] _lockPoolKeys;
	private int _lockPoolCollisions;

	public FusionCacheReactorProbabilistic(int reactorSize = 8_440)
	{
		_lockPoolSize = reactorSize;

		_lockPoolKeys = new string[_lockPoolSize];
		_lockPool = new SemaphoreSlim[_lockPoolSize];
		for (int i = 0; i < _lockPool.Length; i++)
		{
			_lockPool[i] = new SemaphoreSlim(1, 1);
		}
	}

	public int Collisions
	{
		get { return _lockPoolCollisions; }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetLockIndex(string key)
	{
		return unchecked((uint)key.GetHashCode()) % (uint)_lockPoolSize;
	}

	// ACQUIRE LOCK ASYNC
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var idx = GetLockIndex(key);
		var semaphore = _lockPool[idx];

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): trying to fast-acquire the LOCK", cacheName, operationId, key);

		var acquired = semaphore.Wait(0);
		if (acquired)
		{
			_lockPoolKeys[idx] = key;
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK fast-acquired", cacheName, operationId, key);

			return semaphore;
		}

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK already taken", cacheName, operationId, key);

		var key2 = _lockPoolKeys[idx];
		if (key2 != key)
		{
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", cacheName, operationId, key);

			Interlocked.Increment(ref _lockPoolCollisions);
		}

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, operationId, key);

		acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		_lockPoolKeys[idx] = key;

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, operationId, key);

		return acquired ? semaphore : null;
	}

	// ACQUIRE LOCK
	public object? AcquireLock(string cacheName, string key, string operationId, TimeSpan timeout, ILogger? logger)
	{
		var idx = GetLockIndex(key);
		var semaphore = _lockPool[idx];

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): trying to fast-acquire the LOCK", cacheName, operationId, key);

		var acquired = semaphore.Wait(0);
		if (acquired)
		{
			_lockPoolKeys[idx] = key;
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK fast-acquired", cacheName, operationId, key);

			return semaphore;
		}

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK already taken", cacheName, operationId, key);

		var key2 = _lockPoolKeys[idx];
		if (key2 != key)
		{
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", cacheName, operationId, key);

			Interlocked.Increment(ref _lockPoolCollisions);
		}

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, operationId, key);

		acquired = semaphore.Wait(timeout);

		_lockPoolKeys[idx] = key;

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, operationId, key);

		return acquired ? semaphore : null;
	}

	// RELEASE LOCK ASYNC
	public void ReleaseLock(string cacheName, string key, string operationId, object? lockObj, ILogger? logger)
	{
		if (lockObj is null)
			return;

		var idx = GetLockIndex(key);
		_lockPoolKeys[idx] = null;

		try
		{
			((SemaphoreSlim)lockObj).Release();
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the reactor", cacheName, operationId, key);
		}
	}

	// IDISPOSABLE
	private bool disposedValue;
	/*protected virtual*/
	private void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (_lockPool is not null)
				{
					foreach (var semaphore in _lockPool)
					{
						semaphore.Dispose();
					}
				}
			}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_lockPool = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		System.GC.SuppressFinalize(this);
	}
}
