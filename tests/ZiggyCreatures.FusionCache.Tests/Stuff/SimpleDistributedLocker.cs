using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Locking;

namespace FusionCacheTests.Stuff;

internal class SimpleDistributedLocker
	: IFusionCacheDistributedLocker
{
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		// EMPTY
	}
}
