using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Locking;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCacheMemoryLocker"/> that implements the null object pattern, meaning that it does nothing. Consider this a kind of a pass-through implementation.
/// </summary>
public class NullMemoryLocker
	: IFusionCacheMemoryLocker
{
	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return null;
	}

	/// <inheritdoc/>
	public ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return new ValueTask<object?>((object?)null);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// EMTPY
	}
}
