using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;

public partial class RedisDistributedLocker
{
	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		await EnsureConnectionAsync(token).ConfigureAwait(false);

		if (_provider is null)
			return null;

		return await _provider.AcquireLockAsync(lockName, timeout, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
	{
		if (lockObj is null)
			return;

		try
		{
			await ((IDistributedSynchronizationHandle)lockObj).DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a distributed lock named {LockName}", cacheName, cacheInstanceId, operationId, key, lockName);
		}
	}
}
