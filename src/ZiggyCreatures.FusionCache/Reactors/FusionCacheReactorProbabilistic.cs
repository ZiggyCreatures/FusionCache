using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors
{

	internal class FusionCacheReactorProbabilistic
		: IFusionCacheReactor
	{

		private int _lockPoolSize;
		private SemaphoreSlim[] _lockPool;
		private string?[] _lockPoolKeys;
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
		public async Task<object?> AcquireLockAsync(string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();

			var idx = GetLockIndex(key);
			var semaphore = _lockPool[idx];

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to fast-acquire the LOCK", key, operationId);

			var acquired = semaphore.Wait(0);
			if (acquired)
			{
				_lockPoolKeys[idx] = key;
				if (logger?.IsEnabled(LogLevel.Trace) ?? false)
					logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK fast-acquired", key, operationId);

				return semaphore;
			}

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK already taken", key, operationId);

			var key2 = _lockPoolKeys[idx];
			if (key2 != key)
			{
				if (logger?.IsEnabled(LogLevel.Trace) ?? false)
					logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", key, operationId);

				Interlocked.Increment(ref _lockPoolCollisions);
			}

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): waiting to acquire the LOCK", key, operationId);

			acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

			_lockPoolKeys[idx] = key;

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK acquired", key, operationId);

			return acquired ? semaphore : null;
		}

		// ACQUIRE LOCK
		public object? AcquireLock(string key, string operationId, TimeSpan timeout, ILogger? logger)
		{
			var idx = GetLockIndex(key);
			var semaphore = _lockPool[idx];

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to fast-acquire the LOCK", key, operationId);

			var acquired = semaphore.Wait(0);
			if (acquired)
			{
				_lockPoolKeys[idx] = key;
				if (logger?.IsEnabled(LogLevel.Trace) ?? false)
					logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK fast-acquired", key, operationId);

				return semaphore;
			}

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK already taken", key, operationId);

			var key2 = _lockPoolKeys[idx];
			if (key2 != key)
			{
				if (logger?.IsEnabled(LogLevel.Trace) ?? false)
					logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK " + (key2 is null ? "maybe " : string.Empty) + "acquired for a different key (current key: " + key + ", other key: " + key2 + ")", key, operationId);

				Interlocked.Increment(ref _lockPoolCollisions);
			}

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): waiting to acquire the LOCK", key, operationId);

			acquired = semaphore.Wait(timeout);

			_lockPoolKeys[idx] = key;

			if (logger?.IsEnabled(LogLevel.Trace) ?? false)
				logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): LOCK acquired", key, operationId);

			return acquired ? semaphore : null;
		}

		// RELEASE LOCK ASYNC
		public void ReleaseLock(string key, string operationId, object? lockObj, ILogger? logger)
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
					if (_lockPool is object)
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

}