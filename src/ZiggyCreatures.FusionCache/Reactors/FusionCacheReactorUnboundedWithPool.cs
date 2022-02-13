using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors
{
	internal sealed class FusionCacheReactorUnboundedWithPool
		: IFusionCacheReactor
	{
		private Dictionary<string, SemaphoreSlim> _lockCache;

		private readonly int _lockPoolSize;
		private object[] _lockPool;

		public FusionCacheReactorUnboundedWithPool(int reactorSize = 100)
		{
			_lockCache = new Dictionary<string, SemaphoreSlim>(reactorSize);

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

		private SemaphoreSlim GetSemaphore(string key, string operationId, ILogger? logger)
		{
			SemaphoreSlim _semaphore;

			if (_lockCache.TryGetValue(key, out _semaphore))
				return _semaphore;

			lock (_lockPool[GetLockIndex(key)])
			{
				if (_lockCache.TryGetValue(key, out _semaphore))
					return _semaphore;

				_semaphore = new SemaphoreSlim(1, 1);

				_lockCache[key] = _semaphore;

				return _semaphore;
			}
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
					try
					{
						_lockCache.Clear();
					}
					catch
					{
						// EMPTY
					}
				}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
				_lockCache = null;
				_lockPool = null;
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
}
