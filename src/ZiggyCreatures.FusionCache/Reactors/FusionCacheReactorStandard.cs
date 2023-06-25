using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors;

internal sealed class FusionCacheReactorStandard
	: IFusionCacheReactor
{
	private MemoryCache _lockCache;

	private readonly int _lockPoolSize;
	private readonly object[] _lockPool;
	private TimeSpan _slidingExpiration = TimeSpan.FromMinutes(5);

	public FusionCacheReactorStandard(int reactorSize = 8_440)
	{
		_lockCache = new MemoryCache(new MemoryCacheOptions());

		// LOCKING
		_lockPoolSize = reactorSize;
		_lockPool = new object[_lockPoolSize];
		for (int i = 0; i < _lockPool.Length; i++)
		{
			_lockPool[i] = new object();
		}
	}

	public int Collisions
	{
		get { return 0; }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetLockIndex(string key)
	{
		return unchecked((uint)key.GetHashCode()) % (uint)_lockPoolSize;
	}

	private SemaphoreSlim GetSemaphore(string cacheName, string key, string operationId, ILogger? logger)
	{
		object _semaphore;

		if (_lockCache.TryGetValue(key, out _semaphore))
			return (SemaphoreSlim)_semaphore;

		lock (_lockPool[GetLockIndex(key)])
		{
			if (_lockCache.TryGetValue(key, out _semaphore))
				return (SemaphoreSlim)_semaphore;

			_semaphore = new SemaphoreSlim(1, 1);

			using ICacheEntry entry = _lockCache.CreateEntry(key);
			entry.Value = _semaphore;
			entry.SlidingExpiration = _slidingExpiration;
			entry.RegisterPostEvictionCallback((key, value, reason, state) =>
			{
				try
				{
					((SemaphoreSlim)value).Dispose();
				}
				catch (Exception exc)
				{
					if (logger?.IsEnabled(LogLevel.Warning) ?? false)
						logger.Log(LogLevel.Warning, exc, "FUSION [{CacheName}] (K={CacheKey}): an error occurred while trying to dispose a SemaphoreSlim in the reactor", cacheName, key);
				}
			});

			return (SemaphoreSlim)_semaphore;
		}
	}

	// ACQUIRE LOCK ASYNC
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var semaphore = GetSemaphore(cacheName, key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, operationId, key);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		if (acquired)
		{
			// LOCK ACQUIRED
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK timeout", cacheName, operationId, key);
		}

		return acquired ? semaphore : null;
	}

	// ACQUIRE LOCK
	public object? AcquireLock(string cacheName, string key, string operationId, TimeSpan timeout, ILogger? logger)
	{
		var semaphore = GetSemaphore(cacheName, key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, operationId, key);

		var acquired = semaphore.Wait(timeout);

		if (acquired)
		{
			// LOCK ACQUIRED
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK timeout", cacheName, operationId, key);
		}

		return acquired ? semaphore : null;
	}

	// RELEASE LOCK ASYNC
	public void ReleaseLock(string cacheName, string key, string operationId, object? lockObj, ILogger? logger)
	{
		if (lockObj is null)
			return;

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
				// TODO: MAYBE FIND A WAY TO CLEAR ALL THE ENTRIES IN THE CACHE (INCLUDING THE ONES WITH A NeverRemove PRIORITY) AND DISPOSE ALL RELATED SEMAPHORES
				_lockCache.Compact(1.0);
				_lockCache.Dispose();
			}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_lockCache = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
