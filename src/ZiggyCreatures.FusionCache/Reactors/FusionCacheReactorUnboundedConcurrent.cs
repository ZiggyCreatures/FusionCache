using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors;

internal sealed class FusionCacheReactorUnboundedConcurrent
	: IFusionCacheReactor
{
	private ConcurrentDictionary<string, SemaphoreSlim> _lockCache;

	public FusionCacheReactorUnboundedConcurrent(int reactorSize = 100)
	{
		_lockCache = new ConcurrentDictionary<string, SemaphoreSlim>(Environment.ProcessorCount, reactorSize);
	}

	public int Collisions
	{
		get { return 0; }
	}

	private SemaphoreSlim GetSemaphore(string key, string operationId, ILogger? logger)
	{
		return _lockCache.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
	}

	// ACQUIRE LOCK ASYNC
	public async ValueTask<object?> AcquireLockAsync(string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var semaphore = GetSemaphore(key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", operationId, key);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): LOCK acquired", operationId, key);

		return acquired ? semaphore : null;
	}

	// ACQUIRE LOCK
	public object? AcquireLock(string key, string operationId, TimeSpan timeout, ILogger? logger)
	{
		var semaphore = GetSemaphore(key, operationId, logger);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", operationId, key);

		var acquired = semaphore.Wait(timeout);

		if (logger?.IsEnabled(LogLevel.Trace) ?? false)
			logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): LOCK acquired", operationId, key);

		return acquired ? semaphore : null;
	}

	// RELEASE LOCK ASYNC
	public void ReleaseLock(string key, string operationId, object? lockObj, ILogger? logger)
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
				logger.LogWarning(exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the reactor", operationId, key);
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
