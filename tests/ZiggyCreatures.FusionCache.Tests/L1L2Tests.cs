using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public abstract partial class L1L2Tests(ITestOutputHelper output) : AbstractTests(output, "MyCache")
{
	private FusionCacheOptions CreateFusionCacheOptions(string? cacheName = null, Action<FusionCacheOptions>? configure = null)
	{
		var res = new FusionCacheOptions
		{
			CacheKeyPrefix = TestingCacheKeyPrefix
		};

		if (string.IsNullOrWhiteSpace(cacheName) == false)
		{
			res.CacheName = cacheName;
			res.CacheKeyPrefix = cacheName + ":";
		}

		configure?.Invoke(res);

		return res;
	}

	protected abstract IDistributedCache CreateDistributedCache();

	private static string CreateRandomCacheName(string cacheName)
	{
		return cacheName + "_" + Guid.NewGuid().ToString("N");
	}

	private static string CreateRandomCacheKey(string key)
	{
		return key + "_" + Guid.NewGuid().ToString("N");
	}
}
