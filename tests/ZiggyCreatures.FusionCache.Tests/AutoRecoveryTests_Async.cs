using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public partial class AutoRecoveryTests
{
	// TODO: RE-ENABLE THIS
	//
	//[Theory]
	//[ClassData(typeof(SerializerTypesClassData))]
	private async Task CanRecoverAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var defaultOptions = new FusionCacheOptions();
		defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

		var _value = 0;
		var key = "foo";

		var distributedCache = new ChaosDistributedCache(CreateDistributedCache(), CreateXUnitLogger<ChaosDistributedCache>());

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId, CreateXUnitLogger<ChaosBackplane>());
		var backplane2 = CreateChaosBackplane(backplaneConnectionId, CreateXUnitLogger<ChaosBackplane>());
		var backplane3 = CreateChaosBackplane(backplaneConnectionId, CreateXUnitLogger<ChaosBackplane>());

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; opt.SetInstanceId("C1"); });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; opt.SetInstanceId("C2"); });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; opt.SetInstanceId("C3"); });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		logger.LogInformation("## DISABLE DISTRIBUTED CACHE + BACKPLANE");

		// DISABLE DISTRIBUTED CACHE + BACKPLANE
		distributedCache.SetAlwaysThrow();
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		await Task.Delay(1_000, TestContext.Current.CancellationToken);

		// 1
		_value = 1;
		await cache1.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		// 2
		_value = 2;
		await cache2.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		// 3
		_value = 3;
		await cache3.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));

		logger.LogInformation("## RE-ENABLE DISTRIBUTED CACHE + BACKPLANE");

		// RE-ENABLE DISTRIBUTED CACHE + BACKPLANE
		distributedCache.SetNeverThrow();
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		logger.LogInformation("## WAIT FOR THE AUTO-RECOVERY DELAY");

		// WAIT FOR THE AUTO-RECOVERY DELAY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		Assert.Equal(3, await cache1.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, await cache2.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanBeDisabledAsync(SerializerType serializerType)
	{
		var defaultOptions = new FusionCacheOptions();
		defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

		var _value = 0;

		var key = "foo";

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = false; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = false; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = false; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		await Task.Delay(1_000, TestContext.Current.CancellationToken);

		// 1
		_value = 1;
		await cache1.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		// 2
		_value = 2;
		await cache2.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		// 3
		_value = 3;
		await cache3.SetAsync(key, _value, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await Task.Delay(200, TestContext.Current.CancellationToken);

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value, token: TestContext.Current.CancellationToken));
	}

	//[Theory]
	//[ClassData(typeof(SerializerTypesClassData))]
	//public async Task RespectsMaxItemsAsync(SerializerType serializerType)
	//{
	//	var logger = CreateXUnitLogger<FusionCache>();
	//	var value = 0;

	//	var key1 = "foo";
	//	var key2 = "bar";

	//	var defaultOptions = new FusionCacheOptions();
	//	defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

	//	var distributedCache = CreateDistributedCache();

	//	var backplaneConnectionId = Guid.NewGuid().ToString("N");

	//	var backplane1 = CreateChaosBackplane(backplaneConnectionId);
	//	var backplane2 = CreateChaosBackplane(backplaneConnectionId);
	//	var backplane3 = CreateChaosBackplane(backplaneConnectionId);

	//	using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });
	//	using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });
	//	using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; opt.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay; });

	//	cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
	//	cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
	//	cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

	//	await Task.Delay(InitialBackplaneDelay);

	//	logger.LogInformation("DISABLE THE BACKPLANE");

	//	// DISABLE THE BACKPLANE
	//	backplane1.SetAlwaysThrow();
	//	backplane2.SetAlwaysThrow();
	//	backplane3.SetAlwaysThrow();

	//	logger.LogInformation("WAIT");

	//	await Task.Delay(1_000);

	//	// 1
	//	value = 1;
	//	await cache1.SetAsync(key1, value, TimeSpan.FromMinutes(10));
	//	await cache1.SetAsync(key2, value, TimeSpan.FromMinutes(5));
	//	await Task.Delay(200);

	//	// 2
	//	value = 2;
	//	await cache2.SetAsync(key1, value, TimeSpan.FromMinutes(10));
	//	await cache2.SetAsync(key2, value, TimeSpan.FromMinutes(5));
	//	await Task.Delay(200);

	//	// 3
	//	value = 3;
	//	await cache3.SetAsync(key1, value, TimeSpan.FromMinutes(10));
	//	await cache3.SetAsync(key2, value, TimeSpan.FromMinutes(5));
	//	await Task.Delay(200);

	//	value = 21;

	//	logger.LogInformation("ASSERTS");

	//	Assert.Equal(1, await cache1.GetOrSetAsync<int>(key1, async _ => value));
	//	Assert.Equal(2, await cache2.GetOrSetAsync<int>(key1, async _ => value));
	//	Assert.Equal(3, await cache3.GetOrSetAsync<int>(key1, async _ => value));

	//	Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => value));
	//	Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => value));
	//	Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => value));

	//	logger.LogInformation("RE-ENABLE THE BACKPLANE");

	//	// RE-ENABLE THE BACKPLANE
	//	backplane1.SetNeverThrow();
	//	backplane2.SetNeverThrow();
	//	backplane3.SetNeverThrow();

	//	logger.LogInformation("WAIT FOR THE AUTO-RECOVERY DELAY");

	//	// WAIT FOR THE AUTO-RECOVERY DELAY
	//	await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

	//	value = 42;

	//	logger.LogInformation("ASSERTS");

	//	Assert.Equal(3, await cache1.GetOrSetAsync<int>(key1, async _ => value));
	//	Assert.Equal(3, await cache2.GetOrSetAsync<int>(key1, async _ => value));
	//	Assert.Equal(3, await cache3.GetOrSetAsync<int>(key1, async _ => value));

	//	Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => value));
	//	Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => value));
	//	Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => value));
	//}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleIssuesWithBothDistributedCacheAndBackplaneAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();
		defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsA = CreateFusionCacheOptions();
		optionsA.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsB = CreateFusionCacheOptions();
		optionsB.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 20, token: TestContext.Current.CancellationToken);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50, token: TestContext.Current.CancellationToken);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE DISTRIBUTED CACHE AND BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosDistributedCache.SetNeverThrow();
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA3 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B
		var vB3 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70, token: TestContext.Current.CancellationToken);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleReconnectedBackplaneWithoutReconnectedDistributedCacheAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();
		defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsA = CreateFusionCacheOptions();
		optionsA.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsB = CreateFusionCacheOptions();
		optionsB.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 20, token: TestContext.Current.CancellationToken);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50, token: TestContext.Current.CancellationToken);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS, BUT SINCE DIST CACHE IS DOWN THEY WILL BE KEPT IN THE QUEUE)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		var vA3 = await cacheA.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var vB3 = await cacheB.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal(10, vA3);
		Assert.Equal(30, vB3);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// GIVE IT SOME TIME TO RETRY AUTOMATICALLY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA4 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B
		var vB4 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70, token: TestContext.Current.CancellationToken);

		Assert.Equal(30, vA4);
		Assert.Equal(30, vB4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleDistributedCacheErrorsWithBackplaneRetryAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();
		defaultOptions.AutoRecoveryDelay = TimeSpan.FromSeconds(1);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsA = CreateFusionCacheOptions();
		optionsA.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		var optionsB = CreateFusionCacheOptions();
		optionsB.AutoRecoveryDelay = defaultOptions.AutoRecoveryDelay;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA0 = cacheA.GetOrSet<int>("foo", _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB0 = cacheB.GetOrSet<int>("foo", _ => 20, token: TestContext.Current.CancellationToken);

		// IN-SYNC
		Assert.Equal(10, vA0);
		Assert.Equal(10, vB0);

		// DISABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetAlwaysThrow();

		// SET ON CACHE B
		await cacheB.SetAsync<int>("foo", 30, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE A
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 31, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 40, token: TestContext.Current.CancellationToken);

		Assert.Equal(10, vA1);
		Assert.Equal(30, vB1);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// WAIT FOR AUTO-RECOVERY TO KICK IN
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA2 = await cacheA.GetOrSetAsync<int>("foo", async _ => 50, token: TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB2 = await cacheB.GetOrSetAsync<int>("foo", async _ => 60, token: TestContext.Current.CancellationToken);

		Assert.Equal(30, vA2);
		Assert.Equal(30, vB2);
	}

	[Fact]
	public async Task CanHandleIssuesWithOnlyMemoryCacheAndBackplaneAsync()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		// SETUP CACHE A
		var optionsA = CreateFusionCacheOptions();
		optionsA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		optionsA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsA.DefaultEntryOptions.SkipBackplaneNotifications = true;

		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var optionsB = CreateFusionCacheOptions();
		optionsB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		optionsB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsB.DefaultEntryOptions.SkipBackplaneNotifications = true;

		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupBackplane(chaosBackplaneB);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 10, token: TestContext.Current.CancellationToken);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE BACKPLANE
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30, opt => opt.SetSkipBackplaneNotifications(false), token: TestContext.Current.CancellationToken);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50, token: TestContext.Current.CancellationToken);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond(), TestContext.Current.CancellationToken);

		// GET FROM CACHE A (NOTIFICATION FROM CACHE B EXPIRED THE ENTRY, SO IT WILL BE TAKEN AGAIN VIA THE FACTORY)
		var vA3 = await cacheA.GetOrSetAsync<int>("foo", async _ => 30, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE B
		var vB3 = await cacheB.GetOrSetAsync<int>("foo", async _ => 30, token: TestContext.Current.CancellationToken);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}
}
