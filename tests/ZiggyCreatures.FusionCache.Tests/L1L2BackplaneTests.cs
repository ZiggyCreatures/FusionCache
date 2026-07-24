using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public partial class L1L2BackplaneTests
	: AbstractTests
{
	public L1L2BackplaneTests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
		if (UseRedis)
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
	private static readonly bool UseAzureServiceBus = true;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	// DEFAULTS TO THE AZURE SERVICE BUS EMULATOR'S WELL-KNOWN LOCAL CONNECTION STRING (SEE MICROSOFT'S EMULATOR DOCS);
	// SET THE FUSIONCACHE_TESTS_AZURESERVICEBUS_CONNECTIONSTRING ENV VAR TO POINT AT A REAL NAMESPACE INSTEAD
	private static readonly string AzureServiceBusConnectionString =
		Environment.GetEnvironmentVariable("FUSIONCACHE_TESTS_AZURESERVICEBUS_CONNECTIONSTRING")
		?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

	private readonly TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
	private readonly TimeSpan MultiNodeOperationsDelay = TimeSpan.FromMilliseconds(300);

	private IFusionCacheBackplane CreateBackplane(string connectionId)
	{
		if (UseRedis)
			return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: CreateXUnitLogger<RedisBackplane>());
		if (UseAzureServiceBus)
		{
			// USE THE SHARED connectionId AS THE TOPIC NAME, SO ALL THE BACKPLANE INSTANCES CREATED FOR THE SAME
			// LOGICAL TEST "BUS" (E.G. cache1/cache2/cache3 IN A GIVEN TEST) END UP TALKING ON THE SAME SERVICE BUS
			// TOPIC. EACH INSTANCE STILL NEEDS ITS OWN, UNIQUE SUBSCRIPTION (OTHERWISE THEY'D BE COMPETING CONSUMERS
			// ON A SHARED SUBSCRIPTION, INSTEAD OF EACH RECEIVING EVERY MESSAGE AS A BACKPLANE REQUIRES).
			var topicName = AzureServiceBusNaming.SanitizeEntityName($"fusioncache-tests-{connectionId}", AzureServiceBusNaming.MaxTopicNameLength);
			var subscriptionName = AzureServiceBusClientWrapper.GenerateId();
			var adminClient = new ServiceBusAdministrationClient(AzureServiceBusConnectionString);
			var client = new ServiceBusClient(AzureServiceBusConnectionString);
			var communicator = new AzureServiceBusClientWrapper(client, topicName, subscriptionName, CreateXUnitLogger<AzureServiceBusClientWrapper>());
			var provisioner = new AzureServiceBusAdminProvisioner(adminClient, topicName, subscriptionName, CreateXUnitLogger<AzureServiceBusAdminProvisioner>());

			return new AzureServiceBusBackplane(communicator, provisioner, CreateXUnitLogger<AzureServiceBusBackplane>());
		}
		return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: CreateXUnitLogger<MemoryBackplane>());
	}

	private static IDistributedCache CreateDistributedCache()
	{
		if (UseRedis)
			return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });

		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

	private FusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCacheBackplane? backplane, Action<FusionCacheOptions>? setupAction = null, IMemoryCache? memoryCache = null, string? cacheInstanceId = null)
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
