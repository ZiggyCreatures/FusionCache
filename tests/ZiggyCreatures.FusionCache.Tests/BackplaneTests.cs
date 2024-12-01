using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;

namespace FusionCacheTests;

public class BackplaneTests
	: AbstractTests
{
	public BackplaneTests(ITestOutputHelper output)
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
			options.SetInstanceId(cacheInstanceId!);

		options.CacheName = cacheName!;
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

		await cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10));
		await cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
		await cache3.GetOrSetAsync(key, async _ => 3, TimeSpan.FromMinutes(10));

		Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key));
		Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key));

		await cache1.SetAsync(key, 21);

		await Task.Delay(1_000);

		Assert.Equal(21, await cache1.GetOrDefaultAsync<int>(key));
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key));
		Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key));

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

		await Task.Delay(1_000);

		await cache1.SetAsync(key, 42);

		await Task.Delay(1_000);

		Assert.Equal(42, await cache1.GetOrDefaultAsync<int>(key));
		Assert.Equal(42, await cache2.GetOrDefaultAsync<int>(key));
		Assert.Equal(42, await cache3.GetOrDefaultAsync<int>(key));

		await cache1.RemoveAsync(key);

		await Task.Delay(1_000);

		Assert.Equal(0, cache1.GetOrDefault<int>(key));
		Assert.Equal(0, cache2.GetOrDefault<int>(key));
		Assert.Equal(0, cache3.GetOrDefault<int>(key));
	}

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

		Thread.Sleep(1_000);

		Assert.Equal(21, cache1.GetOrDefault<int>(key));
		Assert.Equal(1, cache2.GetOrDefault<int>(key));
		Assert.Equal(1, cache3.GetOrDefault<int>(key));

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
		cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

		Thread.Sleep(1_000);

		cache1.Set(key, 42, TimeSpan.FromMinutes(10));

		Thread.Sleep(1_000);

		Assert.Equal(42, cache1.GetOrDefault<int>(key));
		Assert.Equal(42, cache2.GetOrDefault<int>(key));
		Assert.Equal(42, cache3.GetOrDefault<int>(key));

		cache1.Remove(key);

		Thread.Sleep(1_000);

		Assert.Equal(0, cache1.GetOrDefault<int>(key));
		Assert.Equal(0, cache2.GetOrDefault<int>(key));
		Assert.Equal(0, cache3.GetOrDefault<int>(key));
	}

	[Fact]
	public async Task WorksWithDifferentCachesAsync()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));
		using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));

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
		using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));
		using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));

		Thread.Sleep(1_000);

		cache1.GetOrSet(key, _ => 1, TimeSpan.FromMinutes(10));
		cache2.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
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
		var maybeFooA3 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

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
		var maybeFooA3 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		TestOutput.WriteLine($"AFTER");

		// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
		var maybeFooB3 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(true));

		// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
		var maybeFooC3 = cacheC.TryGet<int>("foo", opt => opt.SetFailSafe(true));

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

		await cache1.SetAsync(key, 1);
		await cache2.SetAsync(key, 2);
		await cache3.SetAsync(key, 3);

		await Task.Delay(1_000);

		await cache1.SetAsync(key, 4);

		await Task.Delay(1_000);

		var v1 = await cache1.GetOrSetAsync(key, async _ => 10, TimeSpan.FromHours(10));
		var v2 = await cache2.GetOrSetAsync(key, async _ => 20, TimeSpan.FromHours(10));
		var v3 = await cache3.GetOrSetAsync(key, async _ => 30, TimeSpan.FromHours(10));

		Assert.Equal(4, v1);
		Assert.Equal(4, v2);
		Assert.Equal(3, v3);
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

		cache1.Set(key, 1);
		cache2.Set(key, 2);
		cache3.Set(key, 3);

		Thread.Sleep(1_000);

		cache1.Set(key, 4);

		Thread.Sleep(1_000);

		var v1 = cache1.GetOrSet(key, _ => 10, TimeSpan.FromHours(10));
		var v2 = cache2.GetOrSet(key, _ => 20, TimeSpan.FromHours(10));
		var v3 = cache3.GetOrSet(key, _ => 30, TimeSpan.FromHours(10));

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

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		await cache1.SetAsync(key1, 1, options => options.SetSize(1));

		await Task.Delay(1_000);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = await cache2.TryGetAsync<int>(key1);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		await cache3.SetAsync(key2, 2);

		await Task.Delay(1_000);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe1 = await cache1.TryGetAsync<int>(key2, options => options.SetSize(1));

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);
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

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		cache1.Set(key1, 1, options => options.SetSize(1));

		Thread.Sleep(1_000);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = cache2.TryGet<int>(key1);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		cache3.Set(key2, 2);

		Thread.Sleep(1_000);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe1 = cache1.TryGet<int>(key2, options => options.SetSize(1));

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagAsync(SerializerType serializerType)
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

		await cache1.SetAsync<int>(fooKey, 1, tags: [xTag, yTag]);
		await cache2.SetAsync<int>(barKey, 2, tags: [yTag, zTag]);
		await cache3.GetOrSetAsync<int>(bazKey, async (_, _) => 3, tags: [xTag, zTag]);

		var foo1 = await cache1.GetOrSetAsync<int>(fooKey, async (_, _) => 11, tags: [xTag, yTag]);
		var bar1 = await cache2.GetOrSetAsync<int>(barKey, async (_, _) => 22, tags: [yTag, zTag]);
		var baz1 = await cache3.GetOrSetAsync<int>(bazKey, async (_, _) => 33, tags: [xTag, zTag]);

		await cache1.RemoveByTagAsync(xTag);

		await Task.Delay(100);

		var foo2 = await cache3.GetOrDefaultAsync<int>(fooKey);
		var bar2 = await cache1.GetOrSetAsync<int>(barKey, async (_, _) => 222, tags: [yTag, zTag]);
		var baz2 = await cache2.GetOrSetAsync<int>(bazKey, async (_, _) => 333, tags: [xTag, zTag]);

		await cache3.RemoveByTagAsync(yTag);

		await Task.Delay(100);

		var bar3 = await cache3.GetOrSetAsync<int>(barKey, async (_, _) => 2222, tags: [yTag, zTag]);
		var foo3 = await cache2.GetOrSetAsync<int>(fooKey, async (_, _) => 1111, tags: [xTag, yTag]);
		var baz3 = await cache1.GetOrSetAsync<int>(bazKey, async (_, _) => 3333, tags: [xTag, zTag]);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanRemoveByTag(SerializerType serializerType)
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

		cache1.Set<int>(fooKey, 1, tags: [xTag, yTag]);
		cache2.Set<int>(barKey, 2, tags: [yTag, zTag]);
		cache3.GetOrSet<int>(bazKey, (_, _) => 3, tags: [xTag, zTag]);

		var foo1 = cache1.GetOrSet<int>(fooKey, (_, _) => 11, tags: [xTag, yTag]);
		var bar1 = cache2.GetOrSet<int>(barKey, (_, _) => 22, tags: [yTag, zTag]);
		var baz1 = cache3.GetOrSet<int>(bazKey, (_, _) => 33, tags: [xTag, zTag]);

		cache1.RemoveByTag(xTag);

		Thread.Sleep(100);

		var foo2 = cache3.GetOrDefault<int>(fooKey);
		var bar2 = cache1.GetOrSet<int>(barKey, (_, _) => 222, tags: [yTag, zTag]);
		var baz2 = cache2.GetOrSet<int>(bazKey, (_, _) => 333, tags: [xTag, zTag]);

		cache3.RemoveByTag(yTag);

		Thread.Sleep(100);

		var bar3 = cache3.GetOrSet<int>(barKey, (_, _) => 2222, tags: [yTag, zTag]);
		var foo3 = cache2.GetOrSet<int>(fooKey, (_, _) => 1111, tags: [xTag, yTag]);
		var baz3 = cache1.GetOrSet<int>(bazKey, (_, _) => 3333, tags: [xTag, zTag]);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

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

		await cache1.SetAsync<int>("milk", 1, tags: ["beverage", "white"]);
		await cache1.SetAsync<int>("coconut", 1, tags: ["food", "white"]);

		await cache2.SetAsync<int>("orange", 1, tags: ["fruit", "orange"]);
		await cache2.GetOrSetAsync<int>("banana", async (ctx, _) =>
		{
			ctx.Tags = ["fruit", "yellow"];
			return 1;
		});

		await cache2.SetAsync<int>("red_wine", 1, tags: ["beverage", "red"]);

		await cache3.SetAsync<int>("trippa", 1, tags: ["food", "red"]);
		await cache3.SetAsync<int>("risotto_milanese", 1, tags: ["food", "yellow"]);
		await cache3.SetAsync<int>("kimchi", 1, tags: ["food", "red"]);

		var milk1 = await cache1.GetOrDefaultAsync<int>("milk");
		var coconut1 = await cache1.GetOrDefaultAsync<int>("coconut");
		var orange1 = await cache1.GetOrDefaultAsync<int>("orange");
		var banana1 = await cache1.GetOrDefaultAsync<int>("banana");
		var redwine1 = await cache1.GetOrDefaultAsync<int>("red_wine");
		var trippa1 = await cache1.GetOrDefaultAsync<int>("trippa");
		var risotto1 = await cache1.GetOrDefaultAsync<int>("risotto_milanese");
		var kimchi1 = await cache1.GetOrDefaultAsync<int>("kimchi");

		Assert.Equal(1, milk1);
		Assert.Equal(1, coconut1);
		Assert.Equal(1, orange1);
		Assert.Equal(1, banana1);
		Assert.Equal(1, redwine1);
		Assert.Equal(1, trippa1);
		Assert.Equal(1, risotto1);
		Assert.Equal(1, kimchi1);

		await cache3.RemoveByTagAsync("red");

		await Task.Delay(100);

		var milk2 = await cache1.GetOrDefaultAsync<int>("milk");
		var coconut2 = await cache1.GetOrDefaultAsync<int>("coconut");
		var orange2 = await cache1.GetOrDefaultAsync<int>("orange");
		var banana2 = await cache1.GetOrDefaultAsync<int>("banana");
		var redwine2 = await cache1.GetOrDefaultAsync<int>("red_wine");
		var trippa2 = await cache1.GetOrDefaultAsync<int>("trippa");
		var risotto2 = await cache1.GetOrDefaultAsync<int>("risotto_milanese");
		var kimchi2 = await cache1.GetOrDefaultAsync<int>("kimchi");

		Assert.Equal(1, milk2);
		Assert.Equal(1, coconut2);
		Assert.Equal(1, orange2);
		Assert.Equal(1, banana2);
		Assert.Equal(0, redwine2);
		Assert.Equal(0, trippa2);
		Assert.Equal(1, risotto2);
		Assert.Equal(0, kimchi2);

		await cache2.RemoveByTagAsync("yellow");

		await Task.Delay(100);

		var milk3 = await cache1.GetOrDefaultAsync<int>("milk");
		var coconut3 = await cache1.GetOrDefaultAsync<int>("coconut");
		var orange3 = await cache1.GetOrDefaultAsync<int>("orange");
		var banana3 = await cache1.GetOrDefaultAsync<int>("banana");
		var redwine3 = await cache1.GetOrDefaultAsync<int>("red_wine");
		var trippa3 = await cache1.GetOrDefaultAsync<int>("trippa");
		var risotto3 = await cache1.GetOrDefaultAsync<int>("risotto_milanese");
		var kimchi3 = await cache1.GetOrDefaultAsync<int>("kimchi");

		Assert.Equal(1, milk3);
		Assert.Equal(1, coconut3);
		Assert.Equal(1, orange3);
		Assert.Equal(0, banana3);
		Assert.Equal(0, redwine3);
		Assert.Equal(0, trippa3);
		Assert.Equal(0, risotto3);
		Assert.Equal(0, kimchi3);

		await cache2.ClearAsync();

		await Task.Delay(100);

		var milk4 = await cache1.GetOrDefaultAsync<int>("milk");
		var coconut4 = await cache1.GetOrDefaultAsync<int>("coconut");
		var orange4 = await cache1.GetOrDefaultAsync<int>("orange");
		var banana4 = await cache1.GetOrDefaultAsync<int>("banana");
		var redwine4 = await cache1.GetOrDefaultAsync<int>("red_wine");
		var trippa4 = await cache1.GetOrDefaultAsync<int>("trippa");
		var risotto4 = await cache1.GetOrDefaultAsync<int>("risotto_milanese");
		var kimchi4 = await cache1.GetOrDefaultAsync<int>("kimchi");

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
	public void CanRemoveByTagWithCacheKeyPrefix(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C2");
		using var cache3 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), options => { options.CacheKeyPrefix = $"{cacheName}:"; }, cacheInstanceId: "C3");

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
	public async Task RemoveByTagDoesNotRemoveTaggingDataAsync(SerializerType serializerType)
	{
		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y", "z"]);
		await cache1.SetAsync<int>("bar", 1, tags: ["x", "y", "z"]);
		await cache1.SetAsync<int>("baz", 1, tags: ["x", "y", "z"]);

		var foo1 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar1 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz1 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo1);
		Assert.Equal(1, bar1);
		Assert.Equal(1, baz1);

		await cache1.RemoveByTagAsync("blah");

		await Task.Delay(100);

		var foo2 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar2 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2 = await cache1.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo2);
		Assert.Equal(1, bar2);
		Assert.Equal(1, baz2);

		await cache2.RemoveByTagAsync("y");

		await Task.Delay(100);

		var foo3 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar3 = await cache1.GetOrDefaultAsync<int>("bar");
		var baz3 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(0, foo3);
		Assert.Equal(0, bar3);
		Assert.Equal(0, baz3);
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
	public async Task CanClearAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();

		using var cache1 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		logger.LogInformation("STEP 1");

		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cache1.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 2");

		var foo1_1 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar1_1 = await cache1.GetOrDefaultAsync<int>("bar");
		var baz1_1 = await cache1.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);
		Assert.Equal(3, baz1_1);

		logger.LogInformation("STEP 3");

		var foo2_1 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_1 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2_1 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);
		Assert.Equal(3, baz2_1);

		logger.LogInformation("STEP 4");

		await cache2.ClearAsync();

		logger.LogInformation("STEP 5");

		await cache2.SetAsync<int>("bar", 22, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 6");

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		logger.LogInformation("STEP 7");

		var foo1_2 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar1_2 = await cache1.GetOrDefaultAsync<int>("bar");
		var baz1_2 = await cache1.GetOrDefaultAsync<int>("baz");

		Assert.Equal(0, foo1_2);
		Assert.Equal(22, bar1_2);
		Assert.Equal(0, baz1_2);

		logger.LogInformation("STEP 8");

		var foo2_2 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_2 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2_2 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(0, foo2_2);
		Assert.Equal(22, bar2_2);
		Assert.Equal(0, baz2_2);
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
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cache1.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cache1.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 2");

		var foo1_1 = cache1.GetOrDefault<int>("foo");
		var bar1_1 = cache1.GetOrDefault<int>("bar");
		var baz1_1 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);
		Assert.Equal(3, baz1_1);

		logger.LogInformation("STEP 3");

		var foo2_1 = cache2.GetOrDefault<int>("foo");
		var bar2_1 = cache2.GetOrDefault<int>("bar");
		var baz2_1 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);
		Assert.Equal(3, baz2_1);

		logger.LogInformation("STEP 4");

		cache2.Clear();

		logger.LogInformation("STEP 5");

		cache2.Set<int>("bar", 22, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 6");

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		logger.LogInformation("STEP 7");

		var foo1_2 = cache1.GetOrDefault<int>("foo");
		var bar1_2 = cache1.GetOrDefault<int>("bar");
		var baz1_2 = cache1.GetOrDefault<int>("baz");

		Assert.Equal(0, foo1_2);
		Assert.Equal(22, bar1_2);
		Assert.Equal(0, baz1_2);

		logger.LogInformation("STEP 8");

		var foo2_2 = cache2.GetOrDefault<int>("foo");
		var bar2_2 = cache2.GetOrDefault<int>("bar");
		var baz2_2 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, foo2_2);
		Assert.Equal(22, bar2_2);
		Assert.Equal(0, baz2_2);
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

		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		var foo1_1 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar1_1 = await cache1.GetOrDefaultAsync<int>("bar");

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);

		await cache1.ClearAsync();

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		var foo1_2 = await cache1.GetOrDefaultAsync<int>("foo");

		Assert.Equal(0, foo1_2);

		// SIMULATE A COLD START BY ADDING A NEW CACHE INSTANCE LATER
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		var bar2_2 = await cache2.GetOrDefaultAsync<int>("bar");

		Assert.Equal(0, bar2_2);
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

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cache1.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		var foo1_1 = cache1.GetOrDefault<int>("foo");
		var bar1_1 = cache1.GetOrDefault<int>("bar");

		Assert.Equal(1, foo1_1);
		Assert.Equal(2, bar1_1);

		cache1.Clear();

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var foo1_2 = cache1.GetOrDefault<int>("foo");

		Assert.Equal(0, foo1_2);

		// SIMULATE A COLD START BY ADDING A NEW CACHE INSTANCE LATER
		using var cache2 = CreateFusionCache(cacheName, serializerType, distributedCache, CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2");

		var bar2_2 = cache2.GetOrDefault<int>("bar");

		Assert.Equal(0, bar2_2);
	}
}
