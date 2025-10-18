using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.NATS;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public abstract partial class L1BackplaneTests<T> : AbstractTests where T : IFusionCacheBackplane
{
	protected TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
	protected TimeSpan MultiNodeOperationsDelay = TimeSpan.FromMilliseconds(300);

	protected L1BackplaneTests(ITestOutputHelper output) : base(output, "MyCache:")
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

	protected abstract T CreateBackplane(string connectionId, ILogger? logger = null);

	protected FusionCache CreateFusionCache(string? cacheName, T? backplane, Action<FusionCacheOptions>? setupAction = null, IMemoryCache? memoryCache = null, string? cacheInstanceId = null, ILogger<FusionCache>? logger = null)
	{
		var options = CreateFusionCacheOptions();

		if (string.IsNullOrWhiteSpace(cacheInstanceId) == false)
			options.SetInstanceId(cacheInstanceId);

		if (string.IsNullOrWhiteSpace(cacheName) == false)
			options.CacheName = cacheName;

		options.EnableSyncEventHandlersExecution = true;

		setupAction?.Invoke(options);
		var fusionCache = new FusionCache(options, memoryCache, logger: logger ?? CreateXUnitLogger<FusionCache>());
		fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		if (backplane is not null)
			fusionCache.SetupBackplane(backplane);

		return fusionCache;
	}
}

public class RedisL1BackplaneTests : L1BackplaneTests<RedisBackplane>
{
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public RedisL1BackplaneTests(ITestOutputHelper output) : base(output)
	{
		InitialBackplaneDelay = TimeSpan.FromSeconds(1).PlusALittleBit();
	}

	protected override RedisBackplane CreateBackplane(string connectionId, ILogger? logger = null)
	{
		return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: (logger as ILogger<RedisBackplane>) ?? CreateXUnitLogger<RedisBackplane>());
	}
}

public class MemoryL1BackplaneTests : L1BackplaneTests<MemoryBackplane>
{
	public MemoryL1BackplaneTests(ITestOutputHelper output) : base(output)
	{
	}

	protected override MemoryBackplane CreateBackplane(string connectionId, ILogger? logger = null)
	{
		return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: (logger as ILogger<MemoryBackplane>) ?? CreateXUnitLogger<MemoryBackplane>());
	}
}

public class NatsL1BackplaneTests : L1BackplaneTests<NatsBackplane>
{
	private static readonly string NatsConnection = "nats://localhost:4222";

	public NatsL1BackplaneTests(ITestOutputHelper output) : base(output)
	{
		InitialBackplaneDelay = TimeSpan.FromSeconds(1).PlusALittleBit();
	}

	protected override FusionCacheOptions CreateFusionCacheOptions()
	{
		var options = base.CreateFusionCacheOptions();
		options.InternalStrings.SetToSafeStrings();
		return options;
	}

	protected override NatsBackplane CreateBackplane(string connectionId, ILogger? logger = null)
	{
		var natsConnection = new NatsConnection(new NatsOpts() { Url = NatsConnection });
		return new NatsBackplane(natsConnection, logger: (logger as ILogger<NatsBackplane>) ?? CreateXUnitLogger<NatsBackplane>());
	}
}
