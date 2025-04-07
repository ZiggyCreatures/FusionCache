using System.Runtime.CompilerServices;
using AsyncKeyedLock;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking.AsyncKeyed;

/// <summary>
/// An implementation of <see cref="IFusionCacheMemoryLocker"/> based on AsyncKeyedLock.
/// </summary>
public sealed class StripedAsyncKeyedMemoryLocker
	: IFusionCacheMemoryLocker
{
	private readonly StripedAsyncKeyedLocker<string> _locker;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncKeyedLocker"/> class.
	/// </summary>
	public StripedAsyncKeyedMemoryLocker(int numberOfStripes = 4049, int maxCount = 1, IEqualityComparer<string>? comparer = null)
	{
		_locker = new StripedAsyncKeyedLocker<string>(numberOfStripes, maxCount, comparer);
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
	private void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				//_locker?.Dispose();
			}

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
