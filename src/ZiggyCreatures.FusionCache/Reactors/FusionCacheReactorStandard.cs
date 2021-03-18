using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Reactors
{

	public class FusionCacheReactorStandard
		: IFusionCacheReactor
	{

		private MemoryCache _lockCache;

		private int _lockPoolSize;
		private object[] _lockPool;
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

		private SemaphoreSlim GetSemaphore(string key, string operationId, ILogger? logger)
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
							logger.LogWarning(exc, "FUSION (K={CacheKey}): an error occurred while trying to dispose a SemaphoreSlim in the reactor", key);
					}
				});

				return (SemaphoreSlim)_semaphore;
			}
		}

		// ACQUIRE LOCK ASYNC
		public async Task<object?> AcquireLockAsync(string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();

			var semaphore = GetSemaphore(key, operationId, logger);

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): waiting to acquire the LOCK", key, operationId);

			var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK acquired", key, operationId);

			return acquired ? semaphore : null;
		}

		// ACQUIRE LOCK
		public object? AcquireLock(string key, string operationId, TimeSpan timeout, ILogger? logger)
		{
			var semaphore = GetSemaphore(key, operationId, logger);

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): waiting to acquire the LOCK", key, operationId);

			var acquired = semaphore.Wait(timeout);

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK acquired", key, operationId);

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
					logger.LogWarning(exc, "FUSION (K={CacheKey} OP={CacheOperationId}): an error occurred while trying to release a SemaphoreSlim in the reactor", key, operationId);
			}
		}

		// IDISPOSABLE
		private bool disposedValue;
		protected virtual void Dispose(bool disposing)
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

}