using System.Diagnostics;
using System.Text.RegularExpressions;
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
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public partial class L1L2BackplaneTests
{
	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task BackplaneWorksAsync(SerializerType serializerType)
	{
		var key = Guid.NewGuid().ToString("N");
		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

		cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

		await cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		await cache3.GetOrSetAsync(key, async _ => 3, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));

		await cache1.SetAsync(key, 21, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		Assert.Equal(21, await cache1.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync(key, 42, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		Assert.Equal(42, await cache1.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(42, await cache2.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(42, await cache3.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));

		await cache1.RemoveAsync(key, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(0, cache2.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(0, cache3.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanIgnoreIncomingBackplaneNotificationsAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		var key = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId));
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options =>
		{
			options.IgnoreIncomingBackplaneNotifications = true;
		});

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync(key, 1, token: TestContext.Current.CancellationToken);
		await cache2.SetAsync(key, 2, token: TestContext.Current.CancellationToken);
		await cache3.SetAsync(key, 3, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync(key, 4, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		var v1 = await cache1.GetOrSetAsync(key, async _ => 10, TimeSpan.FromHours(10), token: TestContext.Current.CancellationToken);
		var v2 = await cache2.GetOrSetAsync(key, async _ => 20, TimeSpan.FromHours(10), token: TestContext.Current.CancellationToken);
		var v3 = await cache3.GetOrSetAsync(key, async _ => 30, TimeSpan.FromHours(10), token: TestContext.Current.CancellationToken);

		Assert.Equal(4, v1);
		Assert.Equal(4, v2);
		Assert.Equal(3, v3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseMultiNodeCachesWithSizeLimitAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		var key1 = Guid.NewGuid().ToString("N");
		var key2 = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var memoryCache1 = new MemoryCache(new MemoryCacheOptions()
		{
			SizeLimit = 10
		});
		using var memoryCache2 = new MemoryCache(new MemoryCacheOptions()
		{
			SizeLimit = 10
		});
		using var memoryCache3 = new MemoryCache(new MemoryCacheOptions()
		{
			//SizeLimit = 10
		});
		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), memoryCache: memoryCache1);
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), memoryCache: memoryCache2);
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), memoryCache: memoryCache3);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		await cache1.SetAsync(key1, 1, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = await cache2.TryGetAsync<int>(key1, token: TestContext.Current.CancellationToken);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		await cache3.SetAsync(key2, 2, token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe1 = await cache1.TryGetAsync<int>(key2, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);
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

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET ON CACHE A
		await cacheA.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);

		// GET ON CACHE A
		var maybeFooA1 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.True(maybeFooA1.HasValue);
		Assert.Equal(42, maybeFooA1.Value);

		// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
		var maybeFooB1 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.True(maybeFooB1.HasValue);
		Assert.Equal(42, maybeFooB1.Value);

		// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

		// EXPIRE ON CACHE A, WHIS WILL:
		// - EXPIRE ON CACHE A
		// - REMOVE ON DISTRIBUTED CACHE
		// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
		//   - EXPIRE ON CACHE B
		//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
		await cacheA.ExpireAsync("foo", token: TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooA2 = await cacheA.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooB2 = await cacheB.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC2 = await cacheC.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		Assert.False(maybeFooA2.HasValue);
		Assert.False(maybeFooB2.HasValue);
		Assert.False(maybeFooC2.HasValue);

		TestOutput.WriteLine($"BEFORE");

		// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooA3 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.True(maybeFooA3.HasValue);
		Assert.Equal(42, maybeFooA3.Value);

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.True(maybeFooB3.HasValue);
		Assert.Equal(42, maybeFooB3.Value);

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

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
		optionsA.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsA.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheA = new FusionCache(optionsA, logger: CreateXUnitLogger<FusionCache>());
		cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));

		var optionsB = CreateFusionCacheOptions();
		optionsB.SetInstanceId("B");
		optionsB.DefaultEntryOptions.IsFailSafeEnabled = true;
		optionsB.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;
		using var cacheB = new FusionCache(optionsB, logger: CreateXUnitLogger<FusionCache>());
		cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// SET 10 ON CACHE-A AND DIST CACHE
		var fooA1 = await cacheA.GetOrSetAsync("foo", async _ => 10, duration1, token: TestContext.Current.CancellationToken);

		// GET 10 FROM DIST CACHE AND SET ON CACHE-B
		var fooB1 = await cacheB.GetOrSetAsync("foo", async _ => 20, duration1, token: TestContext.Current.CancellationToken);

		Assert.Equal(10, fooA1);
		Assert.Equal(10, fooB1);

		// WAIT FOR THE CACHE ENTRIES TO EXPIRE
		await Task.Delay(duration1.PlusALittleBit(), TestContext.Current.CancellationToken);

		// EXECUTE THE FACTORY ON CACHE-A, WHICH WILL TAKE 3 SECONDS, BUT
		// THE FACTORY SOFT TIMEOUT IS 50 MILLISECONDS, SO IT WILL FAIL
		// AND THE STALE VALUE WILL BE RETURNED
		// THE FACTORY WILL BE KEPT RUNNING IN THE BACKGROUND, AND WHEN
		// IT WILL COMPLETE SUCCESSFULLY UPDATE CACHE-A, THE DIST
		// CACHE AND NOTIFY THE OTHER NODES
		// SUCESSFULLY UPDATE CACHE-A, THE DIST CACHE AND NOTIFY THE OTHER NODES
		var fooA2 = await cacheA.GetOrSetAsync("foo", async _ =>
			{
				await Task.Delay(simulatedFactoryDuration);
				return 30;
			}, duration2
, token: TestContext.Current.CancellationToken);

		// IMMEDIATELY GET OR SET FROM CACHE-B: THE VALUE THERE IS
		// EXPIRED, SO THE NEW VALUE WILL BE SAVED AND RETURNED
		var fooB2 = await cacheB.GetOrSetAsync("foo", 40, duration2
, token: TestContext.Current.CancellationToken);

		Assert.Equal(10, fooA2);
		Assert.Equal(40, fooB2);

		// WAIT FOR THE SIMULATED FACTORY TO COMPLETE: A NOTIFICATION
		// WILL BE SENT TO THE OTHER NODES, WHICH IN TURN WILL UPDATE
		// THEIR CACHE ENTRIES
		await Task.Delay(simulatedFactoryDuration.PlusALittleBit(), TestContext.Current.CancellationToken);

		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
		var fooA3 = await cacheA.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var fooB3 = await cacheB.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);

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

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		chaosBackplane.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		await fusionCache.SetAsync<int>("foo", 21, eo, token: TestContext.Current.CancellationToken);
		sw.Stop();

		await Task.Delay(simulatedDelay, TestContext.Current.CancellationToken);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace($"Elapsed (with extra pad): {elapsedMs} ms");

		Assert.True(elapsedMs >= simulatedDelay.TotalMilliseconds);
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds * 2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var backplaneConnectionId = FusionCacheInternalUtils.GenerateOperationId();

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C3");

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		await cache2.SetAsync<int>("bar", 2, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		await cache3.GetOrSetAsync<int>("baz", async _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 1");

		var foo1 = await cache1.GetOrSetAsync<int>("foo", async _ => 11, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar1 = await cache2.GetOrSetAsync<int>("bar", async _ => 22, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz1 = await cache3.GetOrSetAsync<int>("baz", async _ => 33, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		logger.LogInformation("STEP 2");

		await cache1.RemoveByTagAsync("x", token: TestContext.Current.CancellationToken);
		await Task.Delay(250, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 3");

		var foo2 = await cache3.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = await cache1.GetOrSetAsync<int>("bar", async _ => 222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz2 = await cache2.GetOrSetAsync<int>("baz", async _ => 333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		logger.LogInformation("STEP 4");

		await cache3.RemoveByTagAsync("y", token: TestContext.Current.CancellationToken);
		await Task.Delay(250, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 5");

		var bar3 = await cache3.GetOrSetAsync<int>("bar", async _ => 2222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var foo3 = await cache2.GetOrSetAsync<int>("foo", async _ => 1111, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var baz3 = await cache1.GetOrSetAsync<int>("baz", async _ => 3333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagWithCacheKeyPrefixAsync(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C3");

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync<int>("milk", 1, tags: ["beverage", "white"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("coconut", 1, tags: ["food", "white"], token: TestContext.Current.CancellationToken);

		await cache2.SetAsync<int>("orange", 1, tags: ["fruit", "orange"], token: TestContext.Current.CancellationToken);
		await cache2.GetOrSetAsync<int>("banana", async (ctx, _) =>
		{
			ctx.Tags = ["fruit", "yellow"];
			return 1;
		}, token: TestContext.Current.CancellationToken);

		await cache2.SetAsync<int>("red_wine", 1, tags: ["beverage", "red"], token: TestContext.Current.CancellationToken);

		await cache3.SetAsync<int>("trippa", 1, tags: ["food", "red"], token: TestContext.Current.CancellationToken);
		await cache3.SetAsync<int>("risotto_milanese", 1, tags: ["food", "yellow"], token: TestContext.Current.CancellationToken);
		await cache3.SetAsync<int>("kimchi", 1, tags: ["food", "red"], token: TestContext.Current.CancellationToken);

		var milk1 = await cache1.GetOrDefaultAsync<int>("milk", token: TestContext.Current.CancellationToken);
		var coconut1 = await cache1.GetOrDefaultAsync<int>("coconut", token: TestContext.Current.CancellationToken);
		var orange1 = await cache1.GetOrDefaultAsync<int>("orange", token: TestContext.Current.CancellationToken);
		var banana1 = await cache1.GetOrDefaultAsync<int>("banana", token: TestContext.Current.CancellationToken);
		var redwine1 = await cache1.GetOrDefaultAsync<int>("red_wine", token: TestContext.Current.CancellationToken);
		var trippa1 = await cache1.GetOrDefaultAsync<int>("trippa", token: TestContext.Current.CancellationToken);
		var risotto1 = await cache1.GetOrDefaultAsync<int>("risotto_milanese", token: TestContext.Current.CancellationToken);
		var kimchi1 = await cache1.GetOrDefaultAsync<int>("kimchi", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, milk1);
		Assert.Equal(1, coconut1);
		Assert.Equal(1, orange1);
		Assert.Equal(1, banana1);
		Assert.Equal(1, redwine1);
		Assert.Equal(1, trippa1);
		Assert.Equal(1, risotto1);
		Assert.Equal(1, kimchi1);

		await cache3.RemoveByTagAsync("red", token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var milk2 = await cache1.GetOrDefaultAsync<int>("milk", token: TestContext.Current.CancellationToken);
		var coconut2 = await cache1.GetOrDefaultAsync<int>("coconut", token: TestContext.Current.CancellationToken);
		var orange2 = await cache1.GetOrDefaultAsync<int>("orange", token: TestContext.Current.CancellationToken);
		var banana2 = await cache1.GetOrDefaultAsync<int>("banana", token: TestContext.Current.CancellationToken);
		var redwine2 = await cache1.GetOrDefaultAsync<int>("red_wine", token: TestContext.Current.CancellationToken);
		var trippa2 = await cache1.GetOrDefaultAsync<int>("trippa", token: TestContext.Current.CancellationToken);
		var risotto2 = await cache1.GetOrDefaultAsync<int>("risotto_milanese", token: TestContext.Current.CancellationToken);
		var kimchi2 = await cache1.GetOrDefaultAsync<int>("kimchi", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, milk2);
		Assert.Equal(1, coconut2);
		Assert.Equal(1, orange2);
		Assert.Equal(1, banana2);
		Assert.Equal(0, redwine2);
		Assert.Equal(0, trippa2);
		Assert.Equal(1, risotto2);
		Assert.Equal(0, kimchi2);

		await cache2.RemoveByTagAsync("yellow", token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var milk3 = await cache1.GetOrDefaultAsync<int>("milk", token: TestContext.Current.CancellationToken);
		var coconut3 = await cache1.GetOrDefaultAsync<int>("coconut", token: TestContext.Current.CancellationToken);
		var orange3 = await cache1.GetOrDefaultAsync<int>("orange", token: TestContext.Current.CancellationToken);
		var banana3 = await cache1.GetOrDefaultAsync<int>("banana", token: TestContext.Current.CancellationToken);
		var redwine3 = await cache1.GetOrDefaultAsync<int>("red_wine", token: TestContext.Current.CancellationToken);
		var trippa3 = await cache1.GetOrDefaultAsync<int>("trippa", token: TestContext.Current.CancellationToken);
		var risotto3 = await cache1.GetOrDefaultAsync<int>("risotto_milanese", token: TestContext.Current.CancellationToken);
		var kimchi3 = await cache1.GetOrDefaultAsync<int>("kimchi", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, milk3);
		Assert.Equal(1, coconut3);
		Assert.Equal(1, orange3);
		Assert.Equal(0, banana3);
		Assert.Equal(0, redwine3);
		Assert.Equal(0, trippa3);
		Assert.Equal(0, risotto3);
		Assert.Equal(0, kimchi3);

		await cache2.ClearAsync(token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var milk4 = await cache1.GetOrDefaultAsync<int>("milk", token: TestContext.Current.CancellationToken);
		var coconut4 = await cache1.GetOrDefaultAsync<int>("coconut", token: TestContext.Current.CancellationToken);
		var orange4 = await cache1.GetOrDefaultAsync<int>("orange", token: TestContext.Current.CancellationToken);
		var banana4 = await cache1.GetOrDefaultAsync<int>("banana", token: TestContext.Current.CancellationToken);
		var redwine4 = await cache1.GetOrDefaultAsync<int>("red_wine", token: TestContext.Current.CancellationToken);
		var trippa4 = await cache1.GetOrDefaultAsync<int>("trippa", token: TestContext.Current.CancellationToken);
		var risotto4 = await cache1.GetOrDefaultAsync<int>("risotto_milanese", token: TestContext.Current.CancellationToken);
		var kimchi4 = await cache1.GetOrDefaultAsync<int>("kimchi", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, milk4);
		Assert.Equal(0, coconut4);
		Assert.Equal(0, orange4);
		Assert.Equal(0, banana4);
		Assert.Equal(0, redwine4);
		Assert.Equal(0, trippa4);
		Assert.Equal(0, risotto4);
		Assert.Equal(0, kimchi4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagMultiAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		var fooKey = "foo:" + Guid.NewGuid().ToString("N");
		var barKey = "bar:" + Guid.NewGuid().ToString("N");
		var bazKey = "baz:" + Guid.NewGuid().ToString("N");

		var xTag = "tag:x:" + Guid.NewGuid().ToString("N");
		var yTag = "tag:y:" + Guid.NewGuid().ToString("N");
		var zTag = "tag:z:" + Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C3");

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, tags: ["y"], token: TestContext.Current.CancellationToken);
		await cache1.GetOrSetAsync<int>("baz", async _ => 3, tags: ["z"], token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var cache1_foo1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache1_baz1 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache1_foo1);
		Assert.Equal(2, cache1_bar1);
		Assert.Equal(3, cache1_baz1);

		var cache2_foo1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache2_baz1 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache2_foo1);
		Assert.Equal(2, cache2_bar1);
		Assert.Equal(3, cache2_baz1);

		var cache3_foo1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache3_bar1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache3_baz1 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache3_foo1);
		Assert.Equal(2, cache3_bar1);
		Assert.Equal(3, cache3_baz1);

		await cache1.RemoveByTagAsync(["x", "z"], token: TestContext.Current.CancellationToken);
		await Task.Delay(100, TestContext.Current.CancellationToken);

		var cache2_foo2 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache2_baz2 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo2);
		Assert.Equal(2, cache2_bar2);
		Assert.Equal(0, cache2_baz2);

		var cache3_foo2 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache3_bar2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache3_baz2 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache3_foo2);
		Assert.Equal(2, cache3_bar2);
		Assert.Equal(0, cache3_baz2);

		await cache3.RemoveByTagAsync((string[])null!, token: TestContext.Current.CancellationToken);
		await Task.Delay(100, TestContext.Current.CancellationToken);
		await cache3.RemoveByTagAsync([], token: TestContext.Current.CancellationToken);
		await Task.Delay(100, TestContext.Current.CancellationToken);

		var cache1_foo3 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar3 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache3_baz3 = await cache3.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1_foo3);
		Assert.Equal(2, cache2_bar3);
		Assert.Equal(0, cache3_baz3);

		await cache3.RemoveByTagAsync(["y", "non-existing"], token: TestContext.Current.CancellationToken);
		await Task.Delay(100, TestContext.Current.CancellationToken);

		var cache1_foo5 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar5 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache1_baz5 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1_foo5);
		Assert.Equal(0, cache1_bar5);
		Assert.Equal(0, cache1_baz5);

		var cache2_foo5 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar5 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache2_baz5 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo5);
		Assert.Equal(0, cache2_bar5);
		Assert.Equal(0, cache2_baz5);

		var cache3_foo5 = await cache3.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache3_bar5 = await cache3.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache3_baz5 = await cache3.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache3_foo5);
		Assert.Equal(0, cache3_bar5);
		Assert.Equal(0, cache3_baz5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task RemoveByTagDoesNotRemoveTaggingDataAsync(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y", "z"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 1, tags: ["x", "y", "z"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("baz", 1, tags: ["x", "y", "z"], token: TestContext.Current.CancellationToken);

		var foo1 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz1 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(1, bar1);
		Assert.Equal(1, baz1);

		await cache1.RemoveByTagAsync("blah", token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var foo2 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz2 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2);
		Assert.Equal(1, bar2);
		Assert.Equal(1, baz2);

		await cache2.RemoveByTagAsync("y", token: TestContext.Current.CancellationToken);

		await Task.Delay(100, TestContext.Current.CancellationToken);

		var foo3 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar3 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz3 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo3);
		Assert.Equal(0, bar3);
		Assert.Equal(0, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanClearAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache1.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");
		cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache2.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 1");

		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var cache1_foo_1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache1_foo_1);
		Assert.Equal(2, cache1_bar_1);

		logger.LogInformation("STEP 3");

		var cache2_foo_1 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar_1 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(2, cache2_bar_1);
		Assert.Equal(1, cache2_foo_1);

		logger.LogInformation("STEP 4");

		await cache2.ClearAsync(token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 5");

		await cache2.SetAsync<int>("bar", 22, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 6");

		var cache1_foo_2 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_2 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1_foo_2);
		Assert.Equal(22, cache1_bar_2);

		var cache2_foo_2 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar_2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo_2);
		Assert.Equal(22, cache2_bar_2);

		logger.LogInformation("STEP 7");

		var cache1_foo_3 = await cache1.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache1_bar_3 = await cache1.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache1_foo_3);
		Assert.Equal(22, cache1_bar_3);

		var cache2_foo_3 = await cache2.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache2_bar_3 = await cache2.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache2_foo_3);
		Assert.Equal(22, cache2_bar_3);

		logger.LogInformation("STEP 8");

		await cache2.ClearAsync(false, token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 9");

		var cache1_foo_4 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_4 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1_foo_4);
		Assert.Equal(0, cache1_bar_4);

		var cache2_foo_4 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar_4 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo_4);
		Assert.Equal(0, cache2_bar_4);

		logger.LogInformation("STEP 10");

		var cache1_foo_5 = await cache1.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache1_bar_5 = await cache1.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache1_foo_5);
		Assert.Equal(0, cache1_bar_5);

		var cache2_foo_5 = await cache2.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache2_bar_5 = await cache2.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo_5);
		Assert.Equal(0, cache2_bar_5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanClearWithColdStartsAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		var foo1_1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1_1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);

		await cache1.ClearAsync(token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		var foo1_2 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo1_2);

		// SIMULATE A COLD START BY ADDING A NEW CACHE INSTANCE LATER
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		var bar2_2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, bar2_2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseCustomInternalStringsAsync(SerializerType serializerType)
	{
		static FusionCacheOptions _CreateOptions(string name, string instanceId, ILogger logger)
		{
			var cacheName = FusionCacheInternalUtils.GenerateOperationId();
			var backplaneConnectionId = FusionCacheInternalUtils.GenerateOperationId();

			var options = new FusionCacheOptions()
			{
				CacheName = name,
				EnableSyncEventHandlersExecution = true,
				IncludeTagsInLogs = true,
				WaitForInitialBackplaneSubscribe = true,
			};
			// LIMIT THE INTERNAL STRINGS
			options.SetLimitedInternalStrings();
			options.SetInstanceId(instanceId);
			options.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
			options.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			options.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
			options.DefaultEntryOptions.ReThrowBackplaneExceptions = true;

			logger.LogInformation("INTERNAL STRINGS: [{InternalStrings}]", string.Join(',', options.GetInternalStrings()));

			return options;
		}

		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();
		var backplaneConnectionId = FusionCacheInternalUtils.GenerateOperationId();

		var innerDistributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var distributedCache = new LimitedCharsDistributedCache(innerDistributedCache, static key => Regex.IsMatch(key, "^[a-zA-Z0-9_-]+$"));

		var options1 = _CreateOptions(cacheName, "C1", logger);
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var innerBackplane1 = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		var backplane1 = new LimitedCharsBackplane(innerBackplane1, static key => Regex.IsMatch(key, "^[a-zA-Z0-9_-]+$"));
		cache1.SetupBackplane(backplane1);

		var options2 = _CreateOptions(cacheName, "C2", logger);
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var innerBackplane2 = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		var backplane2 = new LimitedCharsBackplane(innerBackplane2, static key => Regex.IsMatch(key, "^[a-zA-Z0-9_-]+$"));
		cache2.SetupBackplane(backplane2);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		// START DOING STUFF

		// SET
		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), tags: ["tag-1", "tag-2"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		// GET OR DEFAULT
		var cache1_foo_1 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_1 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache1_baz_1 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache1_foo_1);
		Assert.Equal(2, cache1_bar_1);
		Assert.Equal(3, cache1_baz_1);

		// REMOVE BY TAG
		await cache1.RemoveByTagAsync("tag-1", token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET OR DEFAULT
		var cache2_foo_1 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache2_bar_1 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache2_baz_1 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache2_foo_1);
		Assert.Equal(0, cache2_bar_1);
		Assert.Equal(3, cache2_baz_1);

		// CLEAR (ALLOW FAIL-SAFE -> EXPIRE ALL)
		await cache1.ClearAsync(token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET OR DEFAULT (ALLOW STALE)
		var cache2_foo_2 = await cache2.GetOrDefaultAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache2_bar_2 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache2_baz_2 = await cache2.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);
		var cache1_foo_2 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_2 = await cache1.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var cache1_baz_2 = await cache1.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache2_foo_2);
		Assert.Equal(0, cache2_bar_2);
		Assert.Equal(0, cache2_baz_2);
		Assert.Equal(0, cache1_foo_2);
		Assert.Equal(0, cache1_bar_2);
		Assert.Equal(0, cache1_baz_2);

		// CLEAR (NO FAIL-SAFE -> REMOVE ALL)
		await cache1.ClearAsync(false, token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		// GET OR DEFAULT
		var cache2_foo_3 = await cache2.GetOrDefaultAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache2_bar_3 = await cache2.GetOrDefaultAsync<int>("bar", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache2_baz_3 = await cache2.GetOrDefaultAsync<int>("baz", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache1_foo_3 = await cache1.GetOrDefaultAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache1_bar_3 = await cache1.GetOrDefaultAsync<int>("bar", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var cache1_baz_3 = await cache1.GetOrDefaultAsync<int>("baz", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, cache2_foo_3);
		Assert.Equal(0, cache2_bar_3);
		Assert.Equal(0, cache2_baz_3);
		Assert.Equal(0, cache1_foo_3);
		Assert.Equal(0, cache1_bar_3);
		Assert.Equal(0, cache1_baz_3);
	}
}
