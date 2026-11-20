using FusionCacheTests.Stuff;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public partial class L1BackplaneTests
	: AbstractTests, IClassFixture<L1L2AzureServiceBusFixture>
{
	public L1BackplaneTests(ITestOutputHelper output, L1L2AzureServiceBusFixture azureServiceBusFixture)
		: base(output, "MyCache:")
	{
		_azureServiceBusFixture = azureServiceBusFixture;
		if (UseRedis || UseAzureServiceBus)
			InitialBackplaneDelay = TimeSpan.FromSeconds(5).PlusALittleBit();
	}

	private FusionCacheOptions CreateFusionCacheOptions()
	{
		var res = new FusionCacheOptions
		{
			WaitForInitialBackplaneSubscribe = true,
			CacheKeyPrefix = TestingCacheKeyPrefix,
			IncludeTagsInLogs = true,
		};

		return res;
	}

	private static readonly bool UseRedis = false;
	private static readonly bool UseAzureServiceBus = false;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	private readonly L1L2AzureServiceBusFixture _azureServiceBusFixture;

	private readonly TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
	private readonly TimeSpan MultiNodeOperationsDelay = TimeSpan.FromMilliseconds(300);

	private IFusionCacheBackplane CreateBackplane(string connectionId, ILogger? logger = null)
	{
		if (UseRedis)
			return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: (logger as ILogger<RedisBackplane>) ?? CreateXUnitLogger<RedisBackplane>());
		if (UseAzureServiceBus)
		{
			var emulator = _azureServiceBusFixture;
			var subscriptionName = emulator.GetNextSubscriptionName();
			var client = new ServiceBusClient(emulator.ConnectionString);
			var options = new AzureServiceBusBackplaneOptions
			{
				IsAdmin = false,
				SubscriptionName = subscriptionName,
			};
			var clientWrapper = new AzureServiceBusClientWrapper(client, emulator.TopicName, subscriptionName, CreateXUnitLogger<AzureServiceBusClientWrapper>(), options);

			return new AzureServiceBusBackplane(clientWrapper, NoOpAzureServiceBusAdminWrapper.Instance, CreateXUnitLogger<AzureServiceBusBackplane>());
		}

		return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: (logger as ILogger<MemoryBackplane>) ?? CreateXUnitLogger<MemoryBackplane>());
	}

	private FusionCache CreateFusionCache(string? cacheName, IFusionCacheBackplane? backplane, Action<FusionCacheOptions>? setupAction = null, IMemoryCache? memoryCache = null, string? cacheInstanceId = null, ILogger<FusionCache>? logger = null)
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
