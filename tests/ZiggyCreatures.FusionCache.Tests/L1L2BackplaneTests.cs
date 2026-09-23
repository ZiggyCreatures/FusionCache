using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

using NATS.Client.Core;

using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.NATS;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public abstract partial class L1L2BackplaneTests<TBackplane, TDistributedCache> : AbstractTests where TBackplane : class, IFusionCacheBackplane where TDistributedCache : IDistributedCache
{
	protected TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
	protected TimeSpan MultiNodeOperationsDelay = TimeSpan.FromMilliseconds(300);
	
	protected L1L2BackplaneTests(ITestOutputHelper output) : base(output, "MyCache:")
	{
	}

	protected virtual FusionCacheOptions CreateFusionCacheOptions()
	{
		var res = new FusionCacheOptions
		{
			WaitForInitialBackplaneSubscribe = true,
			CacheKeyPrefix = TestingCacheKeyPrefix,
			IncludeTagsInLogs = true,
		};

		return res;
	}
	
	protected abstract TBackplane CreateBackplane(string connectionId);
	protected abstract TDistributedCache CreateDistributedCache();
	
	protected FusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, TDistributedCache? distributedCache, TBackplane? backplane, Action<FusionCacheOptions>? setupAction = null, IMemoryCache? memoryCache = null, string? cacheInstanceId = null)
	{
		var options = CreateFusionCacheOptions();

		if (string.IsNullOrWhiteSpace(cacheInstanceId) == false)
			options.SetInstanceId(cacheInstanceId);

		if (string.IsNullOrWhiteSpace(cacheName) == false)
		{
			options.CacheName = cacheName;
			options.CacheKeyPrefix = cacheName + ":";
		}

		options.EnableSyncEventHandlersExecution = true;

		setupAction?.Invoke(options);
		var fusionCache = new FusionCache(options, memoryCache, logger: CreateXUnitLogger<FusionCache>());
		fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		if (distributedCache is not null && serializerType.HasValue)
			fusionCache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType.Value));
		if (backplane is not null)
			fusionCache.SetupBackplane(backplane);

		return fusionCache;
	}
}

public class RedisL1L2BackplaneTests : L1L2BackplaneTests<RedisBackplane, RedisCache>
{
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public RedisL1L2BackplaneTests(ITestOutputHelper output) : base(output)
	{
		InitialBackplaneDelay = TimeSpan.FromSeconds(1).PlusALittleBit();
	}

	protected override RedisBackplane CreateBackplane(string connectionId)
	{
		return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: CreateXUnitLogger<RedisBackplane>());
	}

	protected override RedisCache CreateDistributedCache()
	{
		return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });
	}
}

public class MemoryL1L2BackplaneTests : L1L2BackplaneTests<MemoryBackplane, MemoryDistributedCache>
{
	public MemoryL1L2BackplaneTests(ITestOutputHelper output) : base(output)
	{
	}
	protected override MemoryBackplane CreateBackplane(string connectionId)
	{
		return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: CreateXUnitLogger<MemoryBackplane>());
	}
	protected override MemoryDistributedCache CreateDistributedCache()
	{
		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}
}

public class NatsL1L2BackplaneTests : L1L2BackplaneTests<NatsBackplane, RedisCache>
{
	private static readonly string NatsConnection = "nats://localhost:4222";
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public NatsL1L2BackplaneTests(ITestOutputHelper output) : base(output)
	{
		InitialBackplaneDelay = TimeSpan.FromSeconds(1).PlusALittleBit();
	}

	protected override FusionCacheOptions CreateFusionCacheOptions()
	{
		var options = base.CreateFusionCacheOptions();
		options.InternalStrings.SetToSafeStrings();
		return options;
	}

	protected override NatsBackplane CreateBackplane(string connectionId)
	{
		var natsConnection = new NatsConnection(new NatsOpts() { Url = NatsConnection });
		return new NatsBackplane(natsConnection, logger: CreateXUnitLogger<NatsBackplane>());
	}

	protected override RedisCache CreateDistributedCache()
	{
		return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });
	}
}
