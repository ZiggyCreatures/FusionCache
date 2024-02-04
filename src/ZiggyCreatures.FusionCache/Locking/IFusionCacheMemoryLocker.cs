using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking;

/// <summary>
/// A FusionCache component to handle acquiring and releasing memory locks in a highly optimized way.
/// </summary>
public interface IFusionCacheMemoryLocker
	: IDisposable
{
	/// <summary>
	/// Acquire a generic lock, used to synchronize multiple factory operating on the same cache key, and return it.
	/// </summary>
	/// <param name="cacheName">The CacheName of the FusionCache instance.</param>
	/// <param name="cacheInstanceId">The InstanceId of the FusionCache instance.</param>
	/// <param name="key">The key for which to obtain a lock.</param>
	/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
	/// <param name="timeout">The optional timeout for the lock acquisition.</param>
	/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
	/// <returns>The acquired generic lock object, later released when the critical section is over.</returns>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token);

	/// <summary>
	/// Acquire a generic lock, used to synchronize multiple factory operating on the same cache key, and return it.
	/// </summary>
	/// <param name="cacheName">The name of the FusionCache instance.</param>
	/// <param name="cacheInstanceId">The InstanceId of the FusionCache instance.</param>
	/// <param name="key">The key for which to obtain a lock.</param>
	/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
	/// <param name="timeout">The optional timeout for the lock acquisition.</param>
	/// <returns>The acquired genericlock object, later released when the critical section is over.</returns>
	/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	object? AcquireLock(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token);

	/// <summary>
	/// Release the generic lock object.
	/// </summary>
	/// <param name="cacheName">The name of the FusionCache instance.</param>
	/// <param name="cacheInstanceId">The InstanceId of the FusionCache instance.</param>
	/// <param name="key">The key for which to obtain a lock.</param>
	/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
	/// <param name="lockObj">The generic lock object to release.</param>
	/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
	void ReleaseLock(string cacheName, string cacheInstanceId, string key, string operationId, object? lockObj, ILogger? logger);
}
