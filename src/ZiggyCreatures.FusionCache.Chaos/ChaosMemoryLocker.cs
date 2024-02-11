using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;
using ZiggyCreatures.Caching.Fusion.Locking;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheMemoryLocker"/> with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosMemoryLocker
		: AbstractChaosComponent
		, IFusionCacheMemoryLocker
	{
		private readonly IFusionCacheMemoryLocker _innerMemoryLocker;

		/// <summary>
		/// Initializes a new instance of the ChaosMemoryLocker class.
		/// </summary>
		/// <param name="innerMemoryLocker">The actual <see cref="IFusionCacheMemoryLocker"/> used if and when chaos does not happen.</param>
		/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
		public ChaosMemoryLocker(IFusionCacheMemoryLocker innerMemoryLocker, ILogger<ChaosMemoryLocker>? logger = null)
			: base(logger)
		{
			_innerMemoryLocker = innerMemoryLocker ?? throw new ArgumentNullException(nameof(innerMemoryLocker));
		}

		/// <inheritdoc/>
		public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			MaybeChaos();
			return _innerMemoryLocker.AcquireLock(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
		}

		/// <inheritdoc/>
		public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			MaybeChaos();
			return await _innerMemoryLocker.AcquireLockAsync(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
		}

		/// <inheritdoc/>
		public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
		{
			MaybeChaos();
			_innerMemoryLocker.ReleaseLock(cacheName, cacheInstanceId, operationId, key, lockObj, logger);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// EMPTY
		}
	}
}
