using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking;

/// <summary>
/// A standard implementation of <see cref="IFusionCacheMemoryLocker"/>.
/// </summary>
internal sealed class StandardMemoryLocker
	: IFusionCacheMemoryLocker
{
	private MemoryCache _lockCache;

	private readonly int _lockPoolSize;
	private readonly object[] _lockPool;
	private TimeSpan _slidingExpiration = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Initializes a new instance of the <see cref="StandardMemoryLocker"/> class.
	/// </summary>
	/// <param name="size">The size of the pool used internally for the 1st level locking strategy.</param>
	public StandardMemoryLocker(int size = 210)
	{
		_lockCache = new MemoryCache(new MemoryCacheOptions());

		// LOCKING
		_lockPoolSize = size;
		_lockPool = new object[_lockPoolSize];
		for (var i = 0; i < _lockPool.Length; i++)
		{
			_lockPool[i] = new object();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetLockIndex(string key)
	{
		return unchecked((uint)key.GetHashCode()) % (uint)_lockPoolSize;
	}

	private SemaphoreSlim GetSemaphore(string cacheName, string cacheInstanceId, string key, ILogger? logger)
	{
		object? _semaphore;

		if (_lockCache.TryGetValue(key, out _semaphore))
			return (SemaphoreSlim)_semaphore!;

		lock (_lockPool[GetLockIndex(key)])
		{
			if (_lockCache.TryGetValue(key, out _semaphore))
				return (SemaphoreSlim)_semaphore!;

			_semaphore = new SemaphoreSlim(1, 1);

			using var entry = _lockCache.CreateEntry(key);
			entry.Value = _semaphore;
			entry.SlidingExpiration = _slidingExpiration;
			entry.RegisterPostEvictionCallback((key, value, _, _) =>
			{
				try
				{
					((SemaphoreSlim?)value)?.Dispose();
				}
				catch (Exception exc)
				{
					if (logger?.IsEnabled(LogLevel.Warning) ?? false)
						logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): an error occurred while trying to dispose a SemaphoreSlim in the memory locker", cacheName, cacheInstanceId, key);
				}
			});

			return (SemaphoreSlim)_semaphore;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var semaphore = GetSemaphore(cacheName, cacheInstanceId, key, logger);

		var acquired = await semaphore.WaitAsync(timeout, token).ConfigureAwait(false);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var semaphore = GetSemaphore(cacheName, cacheInstanceId, key, logger);

		var acquired = semaphore.Wait(timeout, token);

		return acquired ? semaphore : null;
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
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
				_lockCache.Compact(1.0);
				_lockCache.Dispose();
			}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_lockCache = null;
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
