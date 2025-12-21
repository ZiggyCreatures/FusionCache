using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking.Redis;

public partial class RedisDistributedLocker
{
	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		EnsureConnection(token);

		if (_provider is null)
			return null;

		return _provider.AcquireLock(lockName, timeout, token);
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
	{
		if (lockObj is null)
			return;

		try
		{
			((IDistributedSynchronizationHandle)lockObj).Dispose();
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a distributed lock named {LockName}", cacheName, cacheInstanceId, operationId, key, lockName);
		}
	}
}
