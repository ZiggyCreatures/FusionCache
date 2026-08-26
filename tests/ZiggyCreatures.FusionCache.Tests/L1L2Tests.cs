using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace FusionCacheTests;

public partial class L1L2Tests
	: AbstractTests
{
	public enum EagerRefreshDistributedEntryState
	{
		Eager,
		Expired,
		Missing
	}

	private static readonly bool UseRedis = false;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public L1L2Tests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
	}

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

	private FusionCacheOptions CreateEagerRefreshTestOptions()
	{
		return CreateFusionCacheOptions(configure: options =>
		{
			options.DistributedCacheKeyModifierMode = CacheKeyModifierMode.None;
			options.EnableSyncEventHandlersExecution = true;
		});
	}

	private static FusionCacheEntryOptions CreateEagerRefreshEntryOptions(bool factoryOnly = true)
	{
		return new FusionCacheEntryOptions(TimeSpan.FromMinutes(5))
		{
			EagerRefreshThreshold = 0.8f,
			EagerRefreshFactoryOnly = factoryOnly,
			AllowBackgroundDistributedCacheOperations = false
		};
	}

	private string GetProcessedCacheKey(string key)
	{
		return TestsUtils.MaybePreProcessCacheKey(key, TestingCacheKeyPrefix);
	}

	private IFusionCacheMemoryEntry GetMemoryEntry(MemoryCache memoryCache, string key)
	{
		var entry = memoryCache.Get<IFusionCacheMemoryEntry>(GetProcessedCacheKey(key));
		if (entry is null)
			throw new InvalidOperationException("The expected memory entry was not found.");

		return entry;
	}

	private IFusionCacheMemoryEntry MakeMemoryEntryEager(MemoryCache memoryCache, string key)
	{
		var entry = GetMemoryEntry(memoryCache, key);
		if (entry.Metadata is null)
			throw new InvalidOperationException("The expected eager refresh metadata was not found.");

		entry.Metadata.EagerExpirationTimestamp = 0;

		return entry;
	}

	private static IDistributedCache CreateDistributedCache()
	{
		if (UseRedis)
			return new RedisCache(new RedisCacheOptions() { Configuration = RedisConnection });

		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

	private static string CreateRandomCacheName(string cacheName)
	{
		return cacheName + "_" + Guid.NewGuid().ToString("N");
	}

	private static string CreateRandomCacheKey(string key)
	{
		return key + "_" + Guid.NewGuid().ToString("N");
	}
}
