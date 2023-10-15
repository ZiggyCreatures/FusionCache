using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors;

internal sealed class FusionCacheReactorUnbounded
	: IFusionCacheReactor
{
	private Dictionary<string, SemaphoreSlim> _lockCache;

	public FusionCacheReactorUnbounded(int reactorSize = 100)
	{
		_lockCache = new Dictionary<string, SemaphoreSlim>(reactorSize);
	}

	public int Collisions
	{
		get { return 0; }
	}

	private SemaphoreSlim GetSemaphore(string key, string operationId, ILogger? logger)
	{
		SemaphoreSlim _semaphore;

		if (_lockCache.TryGetValue(key, out _semaphore))
			return _semaphore;

		lock (_lockCache)
		{
			if (_lockCache.TryGetValue(key, out _semaphore))
				return _semaphore;

			_semaphore = new SemaphoreSlim(1, 1);

			_lockCache[key] = _semaphore;

			return _semaphore;
		}
	}

	// ACQUIRE LOCK ASYNC
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var semaphore = GetSemaphore(key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, cacheInstanceId, operationId, key);

		return acquired ? semaphore : null;
	}

	// ACQUIRE LOCK
	public object? AcquireLock(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger)
	{
		var semaphore = GetSemaphore(key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", cacheName, cacheInstanceId, operationId, key);

		var acquired = semaphore.Wait(timeout);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", cacheName, cacheInstanceId, operationId, key);

		return acquired ? semaphore : null;
	}

	// RELEASE LOCK ASYNC
	public void ReleaseLock(string cacheName, string cacheInstanceId, string key, string operationId, object? lockObj, ILogger? logger)
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
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the reactor", cacheName, cacheInstanceId, operationId, key);
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
				foreach (var semaphore in _lockCache.Values)
				{
					try
					{
						semaphore.Dispose();
					}
					catch
					{
						// EMPTY
					}
				}
				_lockCache.Clear();
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
