using System;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;

namespace FusionCacheTests;

public class AutoRecoveryTests
	: AbstractTests
{
	public AutoRecoveryTests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
	}

	private FusionCacheOptions CreateFusionCacheOptions()
	{
		var res = new FusionCacheOptions
		{
			CacheKeyPrefix = TestingCacheKeyPrefix
		};

		return res;
	}

	private static readonly string? RedisConnection = null;
	//private static readonly string? RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False";

	private static IDistributedCache CreateDistributedCache()
	{
		if (string.IsNullOrWhiteSpace(RedisConnection))
			return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

		return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });
	}

	private IFusionCacheBackplane CreateBackplane(string connectionId)
	{
		if (string.IsNullOrWhiteSpace(RedisConnection))
			return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: CreateXUnitLogger<MemoryBackplane>());

		return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: CreateXUnitLogger<RedisBackplane>());
	}

	private ChaosBackplane CreateChaosBackplane(string connectionId)
	{
		return new ChaosBackplane(CreateBackplane(connectionId));
	}

	private IFusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCacheBackplane? backplane, Action<FusionCacheOptions>? setupAction = null)
	{
		var options = CreateFusionCacheOptions();

		options.CacheName = cacheName!;
		options.EnableSyncEventHandlersExecution = true;

		setupAction?.Invoke(options);
		var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
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
	public async Task CanRecoverAsync(SerializerType serializerType)
	{
		var defaultOptions = new FusionCacheOptions();

		var _value = 0;

		var key = "foo";

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		await Task.Delay(1_000);

		// 1
		_value = 1;
		await cache1.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		// 2
		_value = 2;
		await cache2.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		// 3
		_value = 3;
		await cache3.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		Assert.Equal(3, await cache1.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(3, await cache2.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanRecover(SerializerType serializerType)
	{
		var defaultOptions = new FusionCacheOptions();

		var _value = 0;

		var key = "foo";

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		Thread.Sleep(1_000);

		// 1
		_value = 1;
		cache1.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		// 2
		_value = 2;
		cache2.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		// 3
		_value = 3;
		cache3.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		Assert.Equal(1, cache1.GetOrSet<int>(key, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		Assert.Equal(3, cache1.GetOrSet<int>(key, _ => _value));
		Assert.Equal(3, cache2.GetOrSet<int>(key, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanBeDisabledAsync(SerializerType serializerType)
	{
		var defaultOptions = new FusionCacheOptions();

		var _value = 0;

		var key = "foo";

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = false; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = false; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = false; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		await Task.Delay(1_000);

		// 1
		_value = 1;
		await cache1.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		// 2
		_value = 2;
		await cache2.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		// 3
		_value = 3;
		await cache3.SetAsync(key, _value, TimeSpan.FromMinutes(10));
		await Task.Delay(200);

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanBeDisabled(SerializerType serializerType)
	{
		var defaultOptions = new FusionCacheOptions();

		var _value = 0;

		var key = "foo";

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = false; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = false; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = false; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		Thread.Sleep(1_000);

		// 1
		_value = 1;
		cache1.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		// 2
		_value = 2;
		cache2.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		// 3
		_value = 3;
		cache3.Set(key, _value, TimeSpan.FromMinutes(10));
		Thread.Sleep(200);

		Assert.Equal(1, cache1.GetOrSet<int>(key, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		Assert.Equal(1, cache1.GetOrSet<int>(key, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task RespectsMaxItemsAsync(SerializerType serializerType)
	{
		var _value = 0;

		var key1 = "foo";
		var key2 = "bar";

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		await Task.Delay(1_000);

		// 1
		_value = 1;
		await cache1.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
		await cache1.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
		await Task.Delay(200);

		// 2
		_value = 2;
		await cache2.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
		await cache2.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
		await Task.Delay(200);

		// 3
		_value = 3;
		await cache3.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
		await cache3.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
		await Task.Delay(200);

		_value = 21;

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key1, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key1, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key1, async _ => _value));

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		_value = 42;

		Assert.Equal(3, await cache1.GetOrSetAsync<int>(key1, async _ => _value));
		Assert.Equal(3, await cache2.GetOrSetAsync<int>(key1, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key1, async _ => _value));

		Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => _value));
		Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => _value));
		Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void RespectsMaxItems(SerializerType serializerType)
	{
		var _value = 0;

		var key1 = "foo";
		var key2 = "bar";

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = CreateDistributedCache();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var backplane1 = CreateChaosBackplane(backplaneConnectionId);
		var backplane2 = CreateChaosBackplane(backplaneConnectionId);
		var backplane3 = CreateChaosBackplane(backplaneConnectionId);

		using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });
		using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });
		using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableAutoRecovery = true; opt.AutoRecoveryMaxItems = 1; });

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		// DISABLE THE BACKPLANE
		backplane1.SetAlwaysThrow();
		backplane2.SetAlwaysThrow();
		backplane3.SetAlwaysThrow();

		Thread.Sleep(1_000);

		// 1
		_value = 1;
		cache1.Set(key1, _value, TimeSpan.FromMinutes(10));
		cache1.Set(key2, _value, TimeSpan.FromMinutes(5));
		Thread.Sleep(200);

		// 2
		_value = 2;
		cache2.Set(key1, _value, TimeSpan.FromMinutes(10));
		cache2.Set(key2, _value, TimeSpan.FromMinutes(5));
		Thread.Sleep(200);

		// 3
		_value = 3;
		cache3.Set(key1, _value, TimeSpan.FromMinutes(10));
		cache3.Set(key2, _value, TimeSpan.FromMinutes(5));
		Thread.Sleep(200);

		_value = 21;

		Assert.Equal(1, cache1.GetOrSet<int>(key1, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key1, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key1, _ => _value));

		Assert.Equal(1, cache1.GetOrSet<int>(key2, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key2, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key2, _ => _value));

		// RE-ENABLE THE BACKPLANE
		backplane1.SetNeverThrow();
		backplane2.SetNeverThrow();
		backplane3.SetNeverThrow();

		// WAIT FOR THE AUTO-RECOVERY DELAY
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		_value = 42;

		Assert.Equal(3, cache1.GetOrSet<int>(key1, _ => _value));
		Assert.Equal(3, cache2.GetOrSet<int>(key1, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key1, _ => _value));

		Assert.Equal(1, cache1.GetOrSet<int>(key2, _ => _value));
		Assert.Equal(2, cache2.GetOrSet<int>(key2, _ => _value));
		Assert.Equal(3, cache3.GetOrSet<int>(key2, _ => _value));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleIssuesWithBothDistributedCacheAndBackplaneAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE DISTRIBUTED CACHE AND BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosDistributedCache.SetNeverThrow();
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA3 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60);

		// GET FROM CACHE B
		var vB3 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleIssuesWithBothDistributedCacheAndBackplane(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = cacheA.GetOrSet<int>("foo", _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = cacheB.GetOrSet<int>("foo", _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		cacheB.Set<int>("foo", 30);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = cacheA.GetOrDefault<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = cacheB.GetOrDefault<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE DISTRIBUTED CACHE AND BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosDistributedCache.SetNeverThrow();
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA3 = cacheA.GetOrSet<int>("foo", _ => 60);

		// GET FROM CACHE B
		var vB3 = cacheB.GetOrSet<int>("foo", _ => 70);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleReconnectedBackplaneWithoutReconnectedDistributedCacheAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS, BUT SINCE DIST CACHE IS DOWN THEY WILL BE KEPT IN THE QUEUE)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA3 = await cacheA.GetOrDefaultAsync<int>("foo");

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB3 = await cacheB.GetOrDefaultAsync<int>("foo");

		Assert.Equal(10, vA3);
		Assert.Equal(30, vB3);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// GIVE IT SOME TIME TO RETRY AUTOMATICALLY
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA4 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60);

		// GET FROM CACHE B
		var vB4 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70);

		await Task.Delay(TimeSpan.FromMilliseconds(500));

		Assert.Equal(30, vA4);
		Assert.Equal(30, vB4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleReconnectedBackplaneWithoutReconnectedDistributedCache(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = cacheA.GetOrSet<int>("foo", _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = cacheB.GetOrSet<int>("foo", _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE DISTRIBUTED CACHE AND BACKPLANE
		chaosDistributedCache.SetAlwaysThrow();
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO DISTRIBUTED CACHE OR BACKPLANE, BECAUSE CHAOS)
		cacheB.Set<int>("foo", 30);

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = cacheA.GetOrDefault<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = cacheB.GetOrDefault<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS, BUT SINCE DIST CACHE IS DOWN THEY WILL BE KEPT IN THE QUEUE)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA3 = cacheA.GetOrDefault<int>("foo");

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB3 = cacheB.GetOrDefault<int>("foo");

		Assert.Equal(10, vA3);
		Assert.Equal(30, vB3);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// GIVE IT SOME TIME TO RETRY AUTOMATICALLY
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (UPDATE FROM DISTRIBUTED)
		var vA4 = cacheA.GetOrSet<int>("foo", _ => 60);

		// GET FROM CACHE B
		var vB4 = cacheB.GetOrSet<int>("foo", _ => 70);

		Thread.Sleep(TimeSpan.FromMilliseconds(500));

		Assert.Equal(30, vA4);
		Assert.Equal(30, vB4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleDistributedCacheErrorsWithBackplaneRetryAsync(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA0 = cacheA.GetOrSet<int>("foo", _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB0 = cacheB.GetOrSet<int>("foo", _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA0);
		Assert.Equal(10, vB0);

		// DISABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetAlwaysThrow();

		// SET ON CACHE B
		await cacheB.SetAsync<int>("foo", 30);

		// GET FROM CACHE A
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 31);

		// GET FROM CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 40);

		Assert.Equal(10, vA1);
		Assert.Equal(30, vB1);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// WAIT FOR AUTO-RECOVERY TO KICK IN
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA2 = await cacheA.GetOrSetAsync<int>("foo", async _ => 50);

		await Task.Delay(TimeSpan.FromMilliseconds(500));

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB2 = await cacheB.GetOrSetAsync<int>("foo", async _ => 60);

		Assert.Equal(30, vA2);
		Assert.Equal(30, vB2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleDistributedCacheErrorsWithBackplaneRetry(SerializerType serializerType)
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var defaultOptions = new FusionCacheOptions();

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateXUnitLogger<ChaosDistributedCache>());

		// SETUP CACHE A
		var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheA.SetupBackplane(chaosBackplaneA);

		// SETUP CACHE B
		var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
		var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateXUnitLogger<ChaosBackplane>());
		using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cacheB.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		cacheB.SetupBackplane(chaosBackplaneB);

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA0 = cacheA.GetOrSet<int>("foo", _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB0 = cacheB.GetOrSet<int>("foo", _ => 20);

		// IN-SYNC
		Assert.Equal(10, vA0);
		Assert.Equal(10, vB0);

		// DISABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetAlwaysThrow();

		// SET ON CACHE B
		cacheB.Set<int>("foo", 30);

		// GET FROM CACHE A
		var vA1 = cacheA.GetOrSet<int>("foo", _ => 31);

		// GET FROM CACHE B
		var vB1 = cacheB.GetOrSet<int>("foo", _ => 40);

		Assert.Equal(10, vA1);
		Assert.Equal(30, vB1);

		// RE-ENABLE DISTRIBUTED CACHE
		chaosDistributedCache.SetNeverThrow();

		// WAIT FOR AUTO-RECOVERY TO KICK IN
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA2 = cacheA.GetOrSet<int>("foo", _ => 50);

		Thread.Sleep(TimeSpan.FromMilliseconds(500));

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB2 = cacheB.GetOrSet<int>("foo", _ => 60);

		Assert.Equal(30, vA2);
		Assert.Equal(30, vB2);
	}

	[Fact]
	public async Task CanHandleIssuesWithOnlyAndBackplaneAsync()
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

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = await cacheA.GetOrSetAsync<int>("foo", async _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 10);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE BACKPLANE
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO BACKPLANE, BECAUSE CHAOS)
		await cacheB.SetAsync<int>("foo", 30, opt => opt.SetSkipBackplaneNotifications(false));

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = await cacheA.GetOrDefaultAsync<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = await cacheB.GetOrDefaultAsync<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		await Task.Delay(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (NOTIFICATION FROM CACHE B EXPIRED THE ENTRY, SO IT WILL BE TAKEN AGAIN VIA THE FACTORY)
		var vA3 = await cacheA.GetOrSetAsync<int>("foo", async _ => 30);

		// GET FROM CACHE B
		var vB3 = await cacheB.GetOrSetAsync<int>("foo", async _ => 30);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}

	[Fact]
	public void CanHandleIssuesWithOnlyAndBackplane()
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

		// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
		var vA1 = cacheA.GetOrSet<int>("foo", _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
		var vB1 = cacheB.GetOrSet<int>("foo", _ => 10);

		// IN-SYNC
		Assert.Equal(10, vA1);
		Assert.Equal(10, vB1);

		// DISABLE BACKPLANE
		chaosBackplaneA.SetAlwaysThrow();
		chaosBackplaneB.SetAlwaysThrow();

		// SET ON CACHE B (NO BACKPLANE, BECAUSE CHAOS)
		cacheB.Set<int>("foo", 30, opt => opt.SetSkipBackplaneNotifications(false));

		// GET FROM CACHE A (MEMORY CACHE)
		var vA2 = cacheA.GetOrDefault<int>("foo", 40);

		// GET FROM CACHE B (MEMORY CACHE)
		var vB2 = cacheB.GetOrDefault<int>("foo", 50);

		// NOT IN-SYNC
		Assert.Equal(10, vA2);
		Assert.Equal(30, vB2);

		// RE-ENABLE BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
		chaosBackplaneA.SetNeverThrow();
		chaosBackplaneB.SetNeverThrow();

		// GIVE IT SOME TIME
		Thread.Sleep(defaultOptions.AutoRecoveryDelay.PlusASecond());

		// GET FROM CACHE A (NOTIFICATION FROM CACHE B EXPIRED THE ENTRY, SO IT WILL BE TAKEN AGAIN VIA THE FACTORY)
		var vA3 = cacheA.GetOrSet<int>("foo", _ => 30);

		// GET FROM CACHE B
		var vB3 = cacheB.GetOrSet<int>("foo", _ => 30);

		Assert.Equal(30, vA3);
		Assert.Equal(30, vB3);
	}
}
