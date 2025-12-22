using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking.Distributed.Memory;

/// <summary>
/// An in-memory implementation of <see cref="IFusionCacheDistributedLocker"/>, mainly used for local testing.
/// </summary>
public sealed class MemoryDistributedLocker
	: IFusionCacheDistributedLocker
{
	private MemoryCache _lockCache;

	private readonly int _lockPoolSize;
	private readonly object[] _lockPool;
	private TimeSpan _slidingExpiration = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryDistributedLocker"/> class.
	/// </summary>
	/// <param name="options">The options for the in-memory distributed locker.</param>
	public MemoryDistributedLocker(MemoryDistributedLockerOptions options)
	{
		if (options is null)
			throw new ArgumentNullException(nameof(options));

		_lockCache = new MemoryCache(new MemoryCacheOptions());

		// LOCKING
		_lockPoolSize = options.Size;
		_lockPool = new object[_lockPoolSize];
		for (var i = 0; i < _lockPool.Length; i++)
		{
			_lockPool[i] = new object();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetLockIndex(string lockName)
	{
		return unchecked((uint)lockName.GetHashCode()) % (uint)_lockPoolSize;
	}

	private SemaphoreSlim GetSemaphore(string cacheName, string cacheInstanceId, string key, string lockName, ILogger? logger)
	{
		object? _semaphore;

		if (_lockCache.TryGetValue(lockName, out _semaphore))
			return (SemaphoreSlim)_semaphore!;

		lock (_lockPool[GetLockIndex(lockName)])
		{
			if (_lockCache.TryGetValue(lockName, out _semaphore))
				return (SemaphoreSlim)_semaphore!;

			_semaphore = new SemaphoreSlim(1, 1);

			using var entry = _lockCache.CreateEntry(lockName);
			entry.Value = _semaphore;
			entry.SlidingExpiration = _slidingExpiration;
			entry.RegisterPostEvictionCallback(
				static (lockName, value, _, state) =>
				{
					if (state is null)
						return;

					var (cacheName, cacheInstanceId, key, logger) = ((string, string, string, ILogger))state;

					try
					{
						((SemaphoreSlim?)value)?.Dispose();
					}
					catch (Exception exc)
					{
						if (logger?.IsEnabled(LogLevel.Warning) ?? false)
							logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): an error occurred while trying to dispose a SemaphoreSlim in the distributed locker for lock {LockName}", cacheName, cacheInstanceId, key, lockName);
					}
				},
				(cacheName, cacheInstanceId, key, logger)
			);

			return (SemaphoreSlim)_semaphore;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var semaphore = GetSemaphore(cacheName, cacheInstanceId, key, lockName, logger);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var semaphore = GetSemaphore(cacheName, cacheInstanceId, key, lockName, logger);

		var acquired = semaphore.Wait(timeout, token);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
	{
		ReleaseLock(cacheName, cacheInstanceId, operationId, key, lockName, lockObj, logger, token);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
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
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the distributed locker for lock {LockName}", cacheName, cacheInstanceId, operationId, key, lockName);
		}
	}

	// IDISPOSABLE
	private bool _disposedValue;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposedValue)
		{
			return;
		}

		_lockCache.Compact(1.0);
		_lockCache.Dispose();

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
		_lockCache = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		_disposedValue = true;
	}
}
