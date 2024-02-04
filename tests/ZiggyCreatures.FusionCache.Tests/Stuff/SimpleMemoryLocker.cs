using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Locking;

namespace FusionCacheTests.Stuff;

internal class SimpleMemoryLocker
	: IFusionCacheMemoryLocker
{
	public object? AcquireLock(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string key, string operationId, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public void ReleaseLock(string cacheName, string cacheInstanceId, string key, string operationId, object? lockObj, ILogger? logger)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		// EMPTY
	}
}
