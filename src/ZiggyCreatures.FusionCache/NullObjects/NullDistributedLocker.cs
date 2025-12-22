using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCacheDistributedLocker"/> that implements the null object pattern, meaning that it does nothing. Consider this a kind of a pass-through implementation.
/// </summary>
public class NullDistributedLocker
	: IFusionCacheDistributedLocker
{
	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return null;
	}

	/// <inheritdoc/>
	public ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		return new ValueTask<object?>((object?)null);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// EMPTY
	}
}
