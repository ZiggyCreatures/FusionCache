using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

public class L1BackplaneTests
	: AbstractTests
{
	public L1BackplaneTests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
	}

	private FusionCacheOptions CreateFusionCacheOptions()
	{
		var res = new FusionCacheOptions
		{
			CacheKeyPrefix = TestingCacheKeyPrefix,
			IncludeTagsInLogs = true,
		};

		return res;
	}

	private static readonly bool UseRedis = false;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	private IFusionCacheBackplane CreateBackplane(string connectionId)
	{
		if (UseRedis)
			return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: CreateXUnitLogger<RedisBackplane>());

		return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: CreateXUnitLogger<MemoryBackplane>());
	}

	private FusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCacheBackplane? backplane, Action<FusionCacheOptions>? setupAction = null, IMemoryCache? memoryCache = null, string? cacheInstanceId = null)
	{
		var options = CreateFusionCacheOptions();

		if (string.IsNullOrWhiteSpace(cacheInstanceId) == false)
			options.SetInstanceId(cacheInstanceId!);

		if (string.IsNullOrWhiteSpace(cacheName) == false)
			options.CacheName = cacheName;

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

	[Fact]
	public async Task WorksWithDifferentCachesAsync()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-01");
		using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-02");

		await Task.Delay(1_000);

		await cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10));
		await cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
		await Task.Delay(1_000);
		await cache2bis.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
		await Task.Delay(1_000);

		Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
		Assert.Equal(0, await cache2.GetOrDefaultAsync<int>(key));
		Assert.Equal(2, await cache2bis.GetOrDefaultAsync<int>(key));

		await cache1.SetAsync(key, 21);
		await cache2.SetAsync(key, 42);

		await Task.Delay(1_000);

		Assert.Equal(21, await cache1.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
		Assert.Equal(42, await cache2.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
		await Task.Delay(1_000);
		Assert.Equal(78, await cache2bis.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
		await Task.Delay(1_000);
		Assert.Equal(88, await cache2.GetOrSetAsync(key, async _ => 88, TimeSpan.FromMinutes(10)));
	}

	[Fact]
	public void WorksWithDifferentCaches()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-01");
		using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-02");

		Thread.Sleep(1_000);

		cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10));
		cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
		Thread.Sleep(1_000);
		cache2bis.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
		Thread.Sleep(1_000);

		Assert.Equal(1, cache1.GetOrDefault<int>(key));
		Assert.Equal(0, cache2.GetOrDefault<int>(key));
		Assert.Equal(2, cache2bis.GetOrDefault<int>(key));

		cache1.Set(key, 21);
		cache2.Set(key, 42);

		Thread.Sleep(1_000);

		Assert.Equal(21, cache1.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
		Assert.Equal(42, cache2.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
		Thread.Sleep(1_000);
		Assert.Equal(78, cache2bis.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
		Thread.Sleep(1_000);
		Assert.Equal(88, cache2.GetOrSet(key, _ => 88, TimeSpan.FromMinutes(10)));
	}

	[Fact]
	public async Task CanSkipNotificationsAsync()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
		using var cache3 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));

		cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache3.DefaultEntryOptions.SkipBackplaneNotifications = true;

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		await Task.Delay(1_000);

		await cache1.SetAsync(key, 1, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		await cache2.SetAsync(key, 2, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		await cache3.SetAsync(key, 3, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
		Assert.Equal(2, await cache2.GetOrDefaultAsync<int>(key));
		Assert.Equal(3, await cache3.GetOrDefaultAsync<int>(key));
	}

	[Fact]
	public void CanSkipNotifications()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
		using var cache3 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));

		cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache3.DefaultEntryOptions.SkipBackplaneNotifications = true;

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		Thread.Sleep(1_000);

		cache1.Set(key, 1, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		cache2.Set(key, 2, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		cache3.Set(key, 3, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		Assert.Equal(1, cache1.GetOrDefault<int>(key));
		Assert.Equal(2, cache2.GetOrDefault<int>(key));
		Assert.Equal(3, cache3.GetOrDefault<int>(key));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleExpireOnMultiNodesAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration = TimeSpan.FromMinutes(10);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheA.DefaultEntryOptions.Duration = duration;
		cacheA.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheB.DefaultEntryOptions.Duration = duration;
		cacheB.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		using var cacheC = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheC.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheC.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheC.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheC.DefaultEntryOptions.Duration = duration;
		cacheC.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheC.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		await Task.Delay(TimeSpan.FromMilliseconds(200));

		// SET ON CACHE A
		await cacheA.SetAsync<int>("foo", 42);

		// GET ON CACHE A
		var maybeFooA1 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

		// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
		var maybeFooB1 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

		// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

		// EXPIRE ON CACHE A, WHIS WILL:
		// - EXPIRE ON CACHE A
		// - REMOVE ON DISTRIBUTED CACHE
		// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
		//   - EXPIRE ON CACHE B
		//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
		await cacheA.ExpireAsync("foo");

		await Task.Delay(TimeSpan.FromMilliseconds(250));

		// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooA2 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

		// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooB2 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC2 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

		TestOutput.WriteLine($"BEFORE");

		// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooA3 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(true));

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(true));

		await Task.Delay(TimeSpan.FromMilliseconds(200));

		Assert.True(maybeFooA1.HasValue);
		Assert.Equal(42, maybeFooA1.Value);

		Assert.True(maybeFooB1.HasValue);
		Assert.Equal(42, maybeFooB1.Value);

		Assert.False(maybeFooA2.HasValue);
		Assert.False(maybeFooB2.HasValue);
		Assert.False(maybeFooC2.HasValue);

		Assert.True(maybeFooA3.HasValue);
		Assert.Equal(42, maybeFooA3.Value);

		Assert.True(maybeFooB3.HasValue);
		Assert.Equal(42, maybeFooB3.Value);

		Assert.False(maybeFooC3.HasValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleExpireOnMultiNodes(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration = TimeSpan.FromMinutes(10);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheA.DefaultEntryOptions.Duration = duration;
		cacheA.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheB.DefaultEntryOptions.Duration = duration;
		cacheB.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		using var cacheC = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cacheC.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheC.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cacheC.DefaultEntryOptions.IsFailSafeEnabled = true;
		cacheC.DefaultEntryOptions.Duration = duration;
		cacheC.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cacheC.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		Thread.Sleep(TimeSpan.FromMilliseconds(200));

		// SET ON CACHE A
		cacheA.Set<int>("foo", 42);

		// GET ON CACHE A
		var maybeFooA1 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
		var maybeFooB1 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

		// EXPIRE ON CACHE A, WHIS WILL:
		// - EXPIRE ON CACHE A
		// - REMOVE ON DISTRIBUTED CACHE
		// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
		//   - EXPIRE ON CACHE B
		//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
		cacheA.Expire("foo");

		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooA2 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(false));

		// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooB2 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(false));

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC2 = cacheC.TryGet<int>("foo", opt => opt.SetFailSafe(false));

		TestOutput.WriteLine($"BEFORE");

		// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooA3 = cacheA.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = cacheB.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = cacheC.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		Thread.Sleep(TimeSpan.FromMilliseconds(200));

		Assert.True(maybeFooA1.HasValue);
		Assert.Equal(42, maybeFooA1.Value);

		Assert.True(maybeFooB1.HasValue);
		Assert.Equal(42, maybeFooB1.Value);

		Assert.False(maybeFooA2.HasValue);
		Assert.False(maybeFooB2.HasValue);
		Assert.False(maybeFooC2.HasValue);

		Assert.True(maybeFooA3.HasValue);
		Assert.Equal(42, maybeFooA3.Value);

		Assert.True(maybeFooB3.HasValue);
		Assert.Equal(42, maybeFooB3.Value);

		Assert.False(maybeFooC3.HasValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task BackgroundFactoryCompleteNotifyOtherNodesAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration1 = TimeSpan.FromSeconds(1);
		var duration2 = TimeSpan.FromSeconds(10);
		var factorySoftTimeout = TimeSpan.FromMilliseconds(50);
		var simulatedFactoryDuration = TimeSpan.FromSeconds(3);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

		var optionsA = CreateFusionCacheOptions();
		optionsA.SetInstanceId("A");
		//optionsA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsA.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsA.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));

		var optionsB = CreateFusionCacheOptions();
		optionsB.SetInstanceId("B");
		//optionsB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsB.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsB.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));

		// SET 10 ON CACHE-A AND DIST CACHE
		var fooA1 = await cacheA.GetOrSetAsync("foo", async _ => 10, duration1);

		// GET 10 FROM DIST CACHE AND SET ON CACHE-B
		var fooB1 = await cacheB.GetOrSetAsync("foo", async _ => 20, duration1);

		Assert.Equal(10, fooA1);
		Assert.Equal(10, fooB1);

		// WAIT FOR THE CACHE ENTRIES TO EXPIRE
		await Task.Delay(duration1.PlusALittleBit());

		// EXECUTE THE FACTORY ON CACHE-A, WHICH WILL TAKE 3 SECONDS, BUT
		// THE FACTORY SOFT TIMEOUT IS 50 MILLISECONDS, SO IT WILL FAIL
		// AND THE STALE VALUE WILL BE RETURNED
		// THE FACTORY WILL BE KEPT RUNNING IN THE BACKGROUND, AND WHEN
		// IT WILL COMPLETE SUCCESSFULLY UPDATE CACHE-A, THE DIST
		// CACHE AND NOTIFY THE OTHER NODES
		// SUCESSFULLY UPDATE CACHE-A, THE DIST CACHE AND NOTIFY THE OTHER NODES
		var fooA2 = await cacheA.GetOrSetAsync(
			"foo",
			async _ =>
			{
				await Task.Delay(simulatedFactoryDuration);
				return 30;
			},
			duration2
		);

		// IMMEDIATELY GET OR SET FROM CACHE-B: THE VALUE THERE IS
		// EXPIRED, SO THE NEW VALUE WILL BE SAVED AND RETURNED
		var fooB2 = await cacheB.GetOrSetAsync(
			"foo",
			40,
			duration2
		);

		Assert.Equal(10, fooA2);
		Assert.Equal(40, fooB2);

		// WAIT FOR THE SIMULATED FACTORY TO COMPLETE: A NOTIFICATION
		// WILL BE SENT TO THE OTHER NODES, WHICH IN TURN WILL UPDATE
		// THEIR CACHE ENTRIES
		await Task.Delay(simulatedFactoryDuration.PlusALittleBit());

		// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
		var fooA3 = await cacheA.GetOrDefaultAsync<int>("foo");
		var fooB3 = await cacheB.GetOrDefaultAsync<int>("foo");

		Assert.Equal(30, fooA3);
		Assert.Equal(30, fooB3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BackgroundFactoryCompleteNotifyOtherNodes(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration1 = TimeSpan.FromSeconds(1);
		var duration2 = TimeSpan.FromSeconds(10);
		var factorySoftTimeout = TimeSpan.FromMilliseconds(50);
		var simulatedFactoryDuration = TimeSpan.FromSeconds(3);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

		var optionsA = CreateFusionCacheOptions();
		optionsA.SetInstanceId("A");
		//optionsA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsA.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsA.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));

		var optionsB = CreateFusionCacheOptions();
		optionsB.SetInstanceId("B");
		//optionsB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		optionsB.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsB.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));

		// SET 10 ON CACHE-A AND DIST CACHE
		var fooA1 = cacheA.GetOrSet("foo", _ => 10, duration1);

		// GET 10 FROM DIST CACHE AND SET ON CACHE-B
		var fooB1 = cacheB.GetOrSet("foo", _ => 20, duration1);

		Assert.Equal(10, fooA1);
		Assert.Equal(10, fooB1);

		// WAIT FOR THE CACHE ENTRIES TO EXPIRE
		Thread.Sleep(duration1.PlusALittleBit());

		// EXECUTE THE FACTORY ON CACHE-A, WHICH WILL TAKE 3 SECONDS, BUT
		// THE FACTORY SOFT TIMEOUT IS 50 MILLISECONDS, SO IT WILL FAIL
		// AND THE STALE VALUE WILL BE RETURNED
		// THE FACTORY WILL BE KEPT RUNNING IN THE BACKGROUND, AND WHEN
		// IT WILL COMPLETE SUCCESSFULLY UPDATE CACHE-A, THE DIST
		// CACHE AND NOTIFY THE OTHER NODES
		// SUCESSFULLY UPDATE CACHE-A, THE DIST CACHE AND NOTIFY THE OTHER NODES
		var fooA2 = cacheA.GetOrSet(
			"foo",
			_ =>
			{
				Thread.Sleep(simulatedFactoryDuration);
				return 30;
			},
			duration2
		);

		// IMMEDIATELY GET OR SET FROM CACHE-B: THE VALUE THERE IS
		// EXPIRED, SO THE NEW VALUE WILL BE SAVED AND RETURNED
		var fooB2 = cacheB.GetOrSet(
			"foo",
			40,
			duration2
		);

		Assert.Equal(10, fooA2);
		Assert.Equal(40, fooB2);

		// WAIT FOR THE SIMULATED FACTORY TO COMPLETE: A NOTIFICATION
		// WILL BE SENT TO THE OTHER NODES, WHICH IN TURN WILL UPDATE
		// THEIR CACHE ENTRIES
		Thread.Sleep(simulatedFactoryDuration.PlusALittleBit());

		// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
		var fooA3 = cacheA.GetOrDefault<int>("foo");
		var fooB3 = cacheB.GetOrDefault<int>("foo");

		Assert.Equal(30, fooA3);
		Assert.Equal(30, fooB3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanExecuteBackgroundBackplaneOperationsAsync(SerializerType serializerType)
	{
		var simulatedDelay = TimeSpan.FromMilliseconds(1_000);
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var eo = new FusionCacheEntryOptions().SetDurationSec(10);
		eo.AllowBackgroundDistributedCacheOperations = false;
		eo.AllowBackgroundBackplaneOperations = true;

		var logger = CreateXUnitLogger<FusionCache>();
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());

		var options = CreateFusionCacheOptions();
		using var fusionCache = new FusionCache(options, memoryCache, logger);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		var backplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		var chaosBackplane = new ChaosBackplane(backplane, CreateXUnitLogger<ChaosBackplane>());
		fusionCache.SetupBackplane(chaosBackplane);

		await Task.Delay(options.AutoRecoveryDelay.PlusALittleBit());

		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		chaosBackplane.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		await fusionCache.SetAsync<int>("foo", 21, eo);
		sw.Stop();

		await Task.Delay(simulatedDelay);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace($"Elapsed (with extra pad): {elapsedMs} ms");

		Assert.True(elapsedMs >= simulatedDelay.TotalMilliseconds);
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds * 2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanExecuteBackgroundBackplaneOperations(SerializerType serializerType)
	{
		var simulatedDelay = TimeSpan.FromMilliseconds(1_000);
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var eo = new FusionCacheEntryOptions().SetDurationSec(10);
		eo.AllowBackgroundDistributedCacheOperations = false;
		eo.AllowBackgroundBackplaneOperations = true;

		var logger = CreateXUnitLogger<FusionCache>();
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());

		var options = CreateFusionCacheOptions();
		using var fusionCache = new FusionCache(options, memoryCache, logger);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		var backplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		var chaosBackplane = new ChaosBackplane(backplane, CreateXUnitLogger<ChaosBackplane>());
		fusionCache.SetupBackplane(chaosBackplane);

		Thread.Sleep(options.AutoRecoveryDelay.PlusALittleBit());

		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		chaosBackplane.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		fusionCache.Set<int>("foo", 21, eo);
		sw.Stop();

		Thread.Sleep(simulatedDelay);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace($"Elapsed (with extra pad): {elapsedMs} ms");

		Assert.True(elapsedMs >= simulatedDelay.TotalMilliseconds);
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds * 2);
	}

	[Fact]
	public async Task ReThrowsBackplaneExceptionsAsync()
	{
		var backplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions()));
		var chaosBackplane = new ChaosBackplane(backplane);

		chaosBackplane.SetAlwaysThrow();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions());
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		fusionCache.DefaultEntryOptions.ReThrowBackplaneExceptions = true;

		fusionCache.SetupBackplane(chaosBackplane);

		await Assert.ThrowsAsync<FusionCacheBackplaneException>(async () =>
		{
			await fusionCache.SetAsync<int>("foo", 42);
		});
	}

	[Fact]
	public void ReThrowsBackplaneExceptions()
	{
		var backplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions()));
		var chaosBackplane = new ChaosBackplane(backplane);

		chaosBackplane.SetAlwaysThrow();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions());
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		fusionCache.DefaultEntryOptions.ReThrowBackplaneExceptions = true;

		fusionCache.SetupBackplane(chaosBackplane);

		Assert.Throws<FusionCacheBackplaneException>(() =>
		{
			fusionCache.Set<int>("foo", 42);
		});
	}
}
