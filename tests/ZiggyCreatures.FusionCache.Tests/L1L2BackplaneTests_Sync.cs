using System.Diagnostics;
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
	public void BackplaneWorks(SerializerType serializerType)
	{
		var key = Guid.NewGuid().ToString("N");
		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

		cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

		cache1.GetOrSet(key, _ => 1, TimeSpan.FromMinutes(10));
		cache2.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
		cache3.GetOrSet(key, _ => 3, TimeSpan.FromMinutes(10));

		Assert.Equal(1, cache1.GetOrDefault<int>(key));
		Assert.Equal(1, cache2.GetOrDefault<int>(key));
		Assert.Equal(1, cache3.GetOrDefault<int>(key));

		cache1.Set(key, 21, TimeSpan.FromMinutes(10));

		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(21, cache1.GetOrDefault<int>(key));
		Assert.Equal(1, cache2.GetOrDefault<int>(key));
		Assert.Equal(1, cache3.GetOrDefault<int>(key));

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set(key, 42, TimeSpan.FromMinutes(10));

		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(42, cache1.GetOrDefault<int>(key));
		Assert.Equal(42, cache2.GetOrDefault<int>(key));
		Assert.Equal(42, cache3.GetOrDefault<int>(key));

		cache1.Remove(key);

		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(0, cache1.GetOrDefault<int>(key));
		Assert.Equal(0, cache2.GetOrDefault<int>(key));
		Assert.Equal(0, cache3.GetOrDefault<int>(key));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanIgnoreIncomingBackplaneNotifications(SerializerType serializerType)
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

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set(key, 1);
		cache2.Set(key, 2);
		cache3.Set(key, 3);

		Thread.Sleep(MultiNodeOperationsDelay);

		cache1.Set(key, 4);

		Thread.Sleep(MultiNodeOperationsDelay);

		var v1 = cache1.GetOrSet(key, _ => 10, TimeSpan.FromHours(10));
		var v2 = cache2.GetOrSet(key, _ => 20, TimeSpan.FromHours(10));
		var v3 = cache3.GetOrSet(key, _ => 30, TimeSpan.FromHours(10));

		Assert.Equal(4, v1);
		Assert.Equal(4, v2);
		Assert.Equal(3, v3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanUseMultiNodeCachesWithSizeLimit(SerializerType serializerType)
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

		Thread.Sleep(InitialBackplaneDelay);

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		cache1.Set(key1, 1, options => options.SetSize(1));

		Thread.Sleep(MultiNodeOperationsDelay);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = cache2.TryGet<int>(key1);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		cache3.Set(key2, 2);

		Thread.Sleep(MultiNodeOperationsDelay);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe1 = cache1.TryGet<int>(key2, options => options.SetSize(1));

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);
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

		Thread.Sleep(InitialBackplaneDelay);

		// SET ON CACHE A
		cacheA.Set<int>("foo", 42);

		// GET ON CACHE A
		var maybeFooA1 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		Assert.True(maybeFooA1.HasValue);
		Assert.Equal(42, maybeFooA1.Value);

		// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
		var maybeFooB1 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		Assert.True(maybeFooB1.HasValue);
		Assert.Equal(42, maybeFooB1.Value);

		// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

		// EXPIRE ON CACHE A, WHIS WILL:
		// - EXPIRE ON CACHE A
		// - REMOVE ON DISTRIBUTED CACHE
		// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
		//   - EXPIRE ON CACHE B
		//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
		cacheA.Expire("foo");

		Thread.Sleep(MultiNodeOperationsDelay);

		// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooA2 = cacheA.TryGet<int>("foo");

		// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
		var maybeFooB2 = cacheB.TryGet<int>("foo");

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC2 = cacheC.TryGet<int>("foo");

		Assert.False(maybeFooA2.HasValue);
		Assert.False(maybeFooB2.HasValue);
		Assert.False(maybeFooC2.HasValue);

		TestOutput.WriteLine($"BEFORE");

		// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooA3 = cacheA.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		Assert.True(maybeFooA3.HasValue);
		Assert.Equal(42, maybeFooA3.Value);

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = cacheB.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		Assert.True(maybeFooB3.HasValue);
		Assert.Equal(42, maybeFooB3.Value);

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = cacheC.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly());

		Assert.False(maybeFooC3.HasValue);
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

		Thread.Sleep(InitialBackplaneDelay);

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

		Thread.Sleep(MultiNodeOperationsDelay);

		// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
		var fooA3 = cacheA.GetOrDefault<int>("foo");
		var fooB3 = cacheB.GetOrDefault<int>("foo");

		Assert.Equal(30, fooA3);
		Assert.Equal(30, fooB3);
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

		Thread.Sleep(InitialBackplaneDelay);

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

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanRemoveByTag(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var backplaneConnectionId = FusionCacheInternalUtils.GenerateOperationId();

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C3");

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set<int>("foo", 1, tags: ["x", "y"]);
		cache2.Set<int>("bar", 2, tags: ["y", "z"]);
		cache3.GetOrSet<int>("baz", _ => 3, tags: ["x", "z"]);

		logger.LogInformation("STEP 1");

		var foo1 = cache1.GetOrSet<int>("foo", _ => 11, tags: ["x", "y"]);
		var bar1 = cache2.GetOrSet<int>("bar", _ => 22, tags: ["y", "z"]);
		var baz1 = cache3.GetOrSet<int>("baz", _ => 33, tags: ["x", "z"]);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		logger.LogInformation("STEP 2");

		cache1.RemoveByTag("x");
		Thread.Sleep(250);

		logger.LogInformation("STEP 3");

		var foo2 = cache3.GetOrDefault<int>("foo");
		var bar2 = cache1.GetOrSet<int>("bar", _ => 222, tags: ["y", "z"]);
		var baz2 = cache2.GetOrSet<int>("baz", _ => 333, tags: ["x", "z"]);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		logger.LogInformation("STEP 4");

		cache3.RemoveByTag("y");
		Thread.Sleep(250);

		logger.LogInformation("STEP 5");

		var bar3 = cache3.GetOrSet<int>("bar", _ => 2222, tags: ["y", "z"]);
		var foo3 = cache2.GetOrSet<int>("foo", _ => 1111, tags: ["x", "y"]);
		var baz3 = cache1.GetOrSet<int>("baz", _ => 3333, tags: ["x", "z"]);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanRemoveByTagWithCacheKeyPrefix(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C3");

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set<int>("milk", 1, tags: ["beverage", "white"]);
		cache1.Set<int>("coconut", 1, tags: ["food", "white"]);

		cache2.Set<int>("orange", 1, tags: ["fruit", "orange"]);
		cache2.GetOrSet<int>("banana", (ctx, _) =>
		{
			ctx.Tags = ["fruit", "yellow"];
			return 1;
		});

		cache2.Set<int>("red_wine", 1, tags: ["beverage", "red"]);

		cache3.Set<int>("trippa", 1, tags: ["food", "red"]);
		cache3.Set<int>("risotto_milanese", 1, tags: ["food", "yellow"]);
		cache3.Set<int>("kimchi", 1, tags: ["food", "red"]);

		var milk1 = cache1.GetOrDefault<int>("milk");
		var coconut1 = cache1.GetOrDefault<int>("coconut");
		var orange1 = cache1.GetOrDefault<int>("orange");
		var banana1 = cache1.GetOrDefault<int>("banana");
		var redwine1 = cache1.GetOrDefault<int>("red_wine");
		var trippa1 = cache1.GetOrDefault<int>("trippa");
		var risotto1 = cache1.GetOrDefault<int>("risotto_milanese");
		var kimchi1 = cache1.GetOrDefault<int>("kimchi");

		Assert.Equal(1, milk1);
		Assert.Equal(1, coconut1);
		Assert.Equal(1, orange1);
		Assert.Equal(1, banana1);
		Assert.Equal(1, redwine1);
		Assert.Equal(1, trippa1);
		Assert.Equal(1, risotto1);
		Assert.Equal(1, kimchi1);

		cache3.RemoveByTag("red");

		Thread.Sleep(100);

		var milk2 = cache1.GetOrDefault<int>("milk");
		var coconut2 = cache1.GetOrDefault<int>("coconut");
		var orange2 = cache1.GetOrDefault<int>("orange");
		var banana2 = cache1.GetOrDefault<int>("banana");
		var redwine2 = cache1.GetOrDefault<int>("red_wine");
		var trippa2 = cache1.GetOrDefault<int>("trippa");
		var risotto2 = cache1.GetOrDefault<int>("risotto_milanese");
		var kimchi2 = cache1.GetOrDefault<int>("kimchi");

		Assert.Equal(1, milk2);
		Assert.Equal(1, coconut2);
		Assert.Equal(1, orange2);
		Assert.Equal(1, banana2);
		Assert.Equal(0, redwine2);
		Assert.Equal(0, trippa2);
		Assert.Equal(1, risotto2);
		Assert.Equal(0, kimchi2);

		cache2.RemoveByTag("yellow");

		Thread.Sleep(100);

		var milk3 = cache1.GetOrDefault<int>("milk");
		var coconut3 = cache1.GetOrDefault<int>("coconut");
		var orange3 = cache1.GetOrDefault<int>("orange");
		var banana3 = cache1.GetOrDefault<int>("banana");
		var redwine3 = cache1.GetOrDefault<int>("red_wine");
		var trippa3 = cache1.GetOrDefault<int>("trippa");
		var risotto3 = cache1.GetOrDefault<int>("risotto_milanese");
		var kimchi3 = cache1.GetOrDefault<int>("kimchi");

		Assert.Equal(1, milk3);
		Assert.Equal(1, coconut3);
		Assert.Equal(1, orange3);
		Assert.Equal(0, banana3);
		Assert.Equal(0, redwine3);
		Assert.Equal(0, trippa3);
		Assert.Equal(0, risotto3);
		Assert.Equal(0, kimchi3);

		cache2.Clear();

		Thread.Sleep(100);

		var milk4 = cache1.GetOrDefault<int>("milk");
		var coconut4 = cache1.GetOrDefault<int>("coconut");
		var orange4 = cache1.GetOrDefault<int>("orange");
		var banana4 = cache1.GetOrDefault<int>("banana");
		var redwine4 = cache1.GetOrDefault<int>("red_wine");
		var trippa4 = cache1.GetOrDefault<int>("trippa");
		var risotto4 = cache1.GetOrDefault<int>("risotto_milanese");
		var kimchi4 = cache1.GetOrDefault<int>("kimchi");

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
	public void CanRemoveByTagMulti(SerializerType serializerType)
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

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set<int>("foo", 1, tags: ["x", "y"]);
		cache1.Set<int>("bar", 2, tags: ["y"]);
		cache1.GetOrSet<int>("baz", _ => 3, tags: ["z"]);

		Thread.Sleep(100);

		var cache1_foo1 = cache1.GetOrDefault<int>("foo");
		var cache1_bar1 = cache1.GetOrDefault<int>("bar");
		var cache1_baz1 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(1, cache1_foo1);
		Assert.Equal(2, cache1_bar1);
		Assert.Equal(3, cache1_baz1);

		var cache2_foo1 = cache1.GetOrDefault<int>("foo");
		var cache2_bar1 = cache1.GetOrDefault<int>("bar");
		var cache2_baz1 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(1, cache2_foo1);
		Assert.Equal(2, cache2_bar1);
		Assert.Equal(3, cache2_baz1);

		var cache3_foo1 = cache1.GetOrDefault<int>("foo");
		var cache3_bar1 = cache1.GetOrDefault<int>("bar");
		var cache3_baz1 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(1, cache3_foo1);
		Assert.Equal(2, cache3_bar1);
		Assert.Equal(3, cache3_baz1);

		cache1.RemoveByTag(["x", "z"]);
		Thread.Sleep(100);

		var cache2_foo2 = cache2.GetOrDefault<int>("foo");
		var cache2_bar2 = cache2.GetOrDefault<int>("bar");
		var cache2_baz2 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, cache2_foo2);
		Assert.Equal(2, cache2_bar2);
		Assert.Equal(0, cache2_baz2);

		var cache3_foo2 = cache2.GetOrDefault<int>("foo");
		var cache3_bar2 = cache2.GetOrDefault<int>("bar");
		var cache3_baz2 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, cache3_foo2);
		Assert.Equal(2, cache3_bar2);
		Assert.Equal(0, cache3_baz2);

		cache3.RemoveByTag((string[])null!);
		Thread.Sleep(100);
		cache3.RemoveByTag([]);
		Thread.Sleep(100);

		var cache1_foo3 = cache1.GetOrDefault<int>("foo");
		var cache2_bar3 = cache2.GetOrDefault<int>("bar");
		var cache3_baz3 = cache3.GetOrDefault<int>("baz");

		Assert.Equal(0, cache1_foo3);
		Assert.Equal(2, cache2_bar3);
		Assert.Equal(0, cache3_baz3);

		cache3.RemoveByTag(["y", "non-existing"]);
		Thread.Sleep(100);

		var cache1_foo5 = cache1.GetOrDefault<int>("foo");
		var cache1_bar5 = cache1.GetOrDefault<int>("bar");
		var cache1_baz5 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(0, cache1_foo5);
		Assert.Equal(0, cache1_bar5);
		Assert.Equal(0, cache1_baz5);

		var cache2_foo5 = cache2.GetOrDefault<int>("foo");
		var cache2_bar5 = cache2.GetOrDefault<int>("bar");
		var cache2_baz5 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, cache2_foo5);
		Assert.Equal(0, cache2_bar5);
		Assert.Equal(0, cache2_baz5);

		var cache3_foo5 = cache3.GetOrDefault<int>("foo");
		var cache3_bar5 = cache3.GetOrDefault<int>("bar");
		var cache3_baz5 = cache3.GetOrDefault<int>("baz");

		Assert.Equal(0, cache3_foo5);
		Assert.Equal(0, cache3_bar5);
		Assert.Equal(0, cache3_baz5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void RemoveByTagDoesNotRemoveTaggingData(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set<int>("foo", 1, tags: ["x", "y", "z"]);
		cache1.Set<int>("bar", 1, tags: ["x", "y", "z"]);
		cache1.Set<int>("baz", 1, tags: ["x", "y", "z"]);

		var foo1 = cache2.GetOrDefault<int>("foo");
		var bar1 = cache2.GetOrDefault<int>("bar");
		var baz1 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(1, foo1);
		Assert.Equal(1, bar1);
		Assert.Equal(1, baz1);

		cache1.RemoveByTag("blah");

		Thread.Sleep(100);

		var foo2 = cache1.GetOrDefault<int>("foo");
		var bar2 = cache2.GetOrDefault<int>("bar");
		var baz2 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(1, foo2);
		Assert.Equal(1, bar2);
		Assert.Equal(1, baz2);

		cache2.RemoveByTag("y");

		Thread.Sleep(100);

		var foo3 = cache2.GetOrDefault<int>("foo");
		var bar3 = cache1.GetOrDefault<int>("bar");
		var baz3 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, foo3);
		Assert.Equal(0, bar3);
		Assert.Equal(0, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanClear(SerializerType serializerType)
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

		Thread.Sleep(InitialBackplaneDelay);

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cache1.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 2");

		var cache1_foo_1 = cache1.GetOrDefault<int>("foo");
		var cache1_bar_1 = cache1.GetOrDefault<int>("bar");

		Assert.Equal(1, cache1_foo_1);
		Assert.Equal(2, cache1_bar_1);

		logger.LogInformation("STEP 3");

		var cache2_foo_1 = cache2.GetOrDefault<int>("foo");
		var cache2_bar_1 = cache2.GetOrDefault<int>("bar");

		Assert.Equal(2, cache2_bar_1);
		Assert.Equal(1, cache2_foo_1);

		logger.LogInformation("STEP 4");

		cache2.Clear();
		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 5");

		cache2.Set<int>("bar", 22, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 6");

		var cache1_foo_2 = cache1.GetOrDefault<int>("foo");
		var cache1_bar_2 = cache1.GetOrDefault<int>("bar");

		Assert.Equal(0, cache1_foo_2);
		Assert.Equal(22, cache1_bar_2);

		var cache2_foo_2 = cache2.GetOrDefault<int>("foo");
		var cache2_bar_2 = cache2.GetOrDefault<int>("bar");

		Assert.Equal(0, cache2_foo_2);
		Assert.Equal(22, cache2_bar_2);

		logger.LogInformation("STEP 7");

		var cache1_foo_3 = cache1.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly());
		var cache1_bar_3 = cache1.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly());

		Assert.Equal(1, cache1_foo_3);
		Assert.Equal(22, cache1_bar_3);

		var cache2_foo_3 = cache2.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly());
		var cache2_bar_3 = cache2.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly());

		Assert.Equal(1, cache2_foo_3);
		Assert.Equal(22, cache2_bar_3);

		logger.LogInformation("STEP 8");

		cache2.Clear(false);
		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 9");

		var cache1_foo_4 = cache1.GetOrDefault<int>("foo");
		var cache1_bar_4 = cache1.GetOrDefault<int>("bar");

		Assert.Equal(0, cache1_foo_4);
		Assert.Equal(0, cache1_bar_4);

		var cache2_foo_4 = cache2.GetOrDefault<int>("foo");
		var cache2_bar_4 = cache2.GetOrDefault<int>("bar");

		Assert.Equal(0, cache2_foo_4);
		Assert.Equal(0, cache2_bar_4);

		logger.LogInformation("STEP 10");

		var cache1_foo_5 = cache1.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly());
		var cache1_bar_5 = cache1.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly());

		Assert.Equal(0, cache1_foo_5);
		Assert.Equal(0, cache1_bar_5);

		var cache2_foo_5 = cache2.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly());
		var cache2_bar_5 = cache2.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly());

		Assert.Equal(0, cache2_foo_5);
		Assert.Equal(0, cache2_bar_5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanClearWithColdStarts(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cache1.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		var foo1_1 = cache1.GetOrDefault<int>("foo");
		var bar1_1 = cache1.GetOrDefault<int>("bar");

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);

		cache1.Clear();
		Thread.Sleep(MultiNodeOperationsDelay);

		var foo1_2 = cache1.GetOrDefault<int>("foo");

		Assert.Equal(0, foo1_2);

		// SIMULATE A COLD START BY ADDING A NEW CACHE INSTANCE LATER
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		var bar2_2 = cache2.GetOrDefault<int>("bar");

		Assert.Equal(0, bar2_2);
	}
}
