using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Xunit;

namespace FusionCacheTests;

#if REDIS_TESTS

public sealed class L1L2Tests_Redis(ITestOutputHelper output) : L1L2Tests(output)
{
	private const string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	protected override IDistributedCache CreateDistributedCache()
	{
		return new RedisCache(new RedisCacheOptions() { Configuration = RedisConnection });

	}
}

#endif
