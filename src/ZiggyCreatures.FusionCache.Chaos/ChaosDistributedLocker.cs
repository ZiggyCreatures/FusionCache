using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;
using ZiggyCreatures.Caching.Fusion.Locking;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IFusionCacheDistributedLocker"/> with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosDistributedLocker
	: AbstractChaosComponent
	, IFusionCacheDistributedLocker
{
	private readonly IFusionCacheDistributedLocker _innerDistributedLocker;

	/// <summary>
	/// Initializes a new instance of the ChaosDistributedLocker class.
	/// </summary>
	/// <param name="innerDistributedLocker">The actual <see cref="IFusionCacheDistributedLocker"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosDistributedLocker(IFusionCacheDistributedLocker innerDistributedLocker, ILogger<ChaosDistributedLocker>? logger = null)
		: base(logger)
	{
		_innerDistributedLocker = innerDistributedLocker ?? throw new ArgumentNullException(nameof(innerDistributedLocker));
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		MaybeChaos(token);
		return _innerDistributedLocker.AcquireLock(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		await MaybeChaosAsync(token);
		return await _innerDistributedLocker.AcquireLockAsync(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger, CancellationToken token)
	{
		MaybeChaos(token);
		_innerDistributedLocker.ReleaseLock(cacheName, cacheInstanceId, operationId, key, lockObj, logger, token);
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger, CancellationToken token)
	{
		await MaybeChaosAsync(token);
		await _innerDistributedLocker.ReleaseLockAsync(cacheName, cacheInstanceId, operationId, key, lockObj, logger, token);
	}
}
