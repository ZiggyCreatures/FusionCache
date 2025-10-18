﻿using AsyncKeyedLock;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking.AsyncKeyed;

/// <summary>
/// An implementation of <see cref="IFusionCacheMemoryLocker"/> based on AsyncKeyedLocker.
/// </summary>
public sealed class AsyncKeyedMemoryLocker
	: IFusionCacheMemoryLocker
{
	private readonly AsyncKeyedLocker<string> _locker;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncKeyedLocker"/> class.
	/// </summary>
	public AsyncKeyedMemoryLocker(AsyncKeyedLockOptions? options = null)
	{
		options ??= new AsyncKeyedLockOptions();

		_locker = new AsyncKeyedLocker<string>(options);
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return await _locker.LockOrNullAsync(key, timeout, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return _locker.LockOrNull(key, timeout, token);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
	{
		if (lockObj is null)
			return;

		try
		{
			((IDisposable)lockObj).Dispose();
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release an AsyncKeyedLock result in the memory locker", cacheName, cacheInstanceId, operationId, key);
		}
	}

	// IDISPOSABLE
	private bool disposedValue;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (disposedValue)
		{
			return;
		}

		_locker?.Dispose();

		disposedValue = true;
	}
}
