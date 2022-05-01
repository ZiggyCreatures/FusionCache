using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Reactors
{
	/// <summary>
	/// Represents one of the core pieces of an instance of an <see cref="FusionCache"/>, dealing with acquiring and releasing locks in a highly optimized way.
	/// </summary>
	public interface IFusionCacheReactor
		: IDisposable
	{
		/// <summary>
		/// Acquire a generic lock, used to synchronize multiple factory operating on the same cache key, and return it.
		/// </summary>
		/// <param name="key">The key for which to obtain a lock.</param>
		/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
		/// <param name="timeout">The optional timeout for the lock acquisition.</param>
		/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The acquired genericlock object, later released when the critical section is over.</returns>
		ValueTask<object?> AcquireLockAsync(string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token);

		/// <summary>
		/// Acquire a generic lock, used to synchronize multiple factory operating on the same cache key, and return it.
		/// </summary>
		/// <param name="key">The key for which to obtain a lock.</param>
		/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
		/// <param name="timeout">The optional timeout for the lock acquisition.</param>
		/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
		/// <returns>The acquired genericlock object, later released when the critical section is over.</returns>
		object? AcquireLock(string key, string operationId, TimeSpan timeout, ILogger? logger);

		/// <summary>
		/// Release the generic lock object.
		/// </summary>
		/// <param name="key">The key for which to obtain a lock.</param>
		/// <param name="operationId">The operation id which uniquely identifies a high-level cache operation.</param>
		/// <param name="lockObj">The generic lock object to release.</param>
		/// <param name="logger">The <see cref="ILogger"/> to use, if any.</param>
		void ReleaseLock(string key, string operationId, object? lockObj, ILogger? logger);

		/// <summary>
		/// Exposes the eventual amount ofcollisions happened inside the reactor, for diagnostics purposes.
		/// </summary>
		int Collisions { get; }
	}
}
