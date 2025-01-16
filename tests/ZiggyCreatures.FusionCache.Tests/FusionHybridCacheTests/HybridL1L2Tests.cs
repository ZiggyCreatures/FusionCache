using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.DangerZone;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace FusionCacheTests.FusionHybridCacheTests;

public class HybridL1L2Tests
	: AbstractTests
{
	private static readonly bool UseRedis = false;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public HybridL1L2Tests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
	}

	private FusionCacheOptions CreateFusionCacheOptions(string? cacheName = null, Action<FusionCacheOptions>? configure = null)
	{
		var res = new FusionCacheOptions
		{
			CacheKeyPrefix = TestingCacheKeyPrefix
		};

		if (string.IsNullOrWhiteSpace(cacheName) == false)
		{
			res.CacheName = cacheName;
			res.CacheKeyPrefix = cacheName + ":";
		}

		configure?.Invoke(res);

		return res;
	}

	private static IDistributedCache CreateDistributedCache()
	{
		if (UseRedis)
			return new RedisCache(new RedisCacheOptions() { Configuration = RedisConnection });

		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

	private static string CreateRandomCacheName(string cacheName)
	{
		return cacheName + "_" + Guid.NewGuid().ToString("N");
	}

	private static string CreateRandomCacheKey(string key)
	{
		return key + "_" + Guid.NewGuid().ToString("N");
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReturnsDataFromDistributedCacheIfNoDataInMemoryCacheAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		using var mc = new MemoryCache(new MemoryCacheOptions());
		var dc = CreateDistributedCache();
		using var fc = new FusionCache(CreateFusionCacheOptions(), mc).SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>(keyFoo, async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) });
		mc.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var newValue = await cache.GetOrCreateAsync<int>(keyFoo, async _ => 21, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) });
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheFailuresAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc = new FusionCache(options).SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>(keyFoo, async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) });
		await Task.Delay(1_500);
		chaosdc.SetAlwaysThrow();
		var newValue = await cache.GetOrCreateAsync<int>(keyFoo, async _ => throw new Exception("Generic error"), new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) });
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();
		using var fc = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var task = cache.GetOrCreateAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) });
		await Task.Delay(500);
		fc.RemoveDistributedCache();
		var value = await task;
		Assert.Equal(42, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheFailuresInTheMiddleOfAnOperationAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		using var mc = new MemoryCache(new MemoryCacheOptions());
		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);
		var options = CreateFusionCacheOptions();
		options.DistributedCacheKeyModifierMode = CacheKeyModifierMode.None;
		using var fc = new FusionCache(options, mc).SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyBar, options.CacheKeyPrefix);

		await cache.GetOrCreateAsync<int>(keyBar, async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) });
		Assert.NotNull(dc.GetString(preProcessedCacheKey));

		preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyFoo, options.CacheKeyPrefix);
		var task = cache.GetOrCreateAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) });
		await Task.Delay(500);
		chaosdc.SetAlwaysThrow();
		var value = await task;
		chaosdc.SetNeverThrow();

		// END RESULT IS WHAT EXPECTED
		Assert.Equal(42, value);

		// MEMORY CACHE HAS BEEN UPDATED
		Assert.Equal(42, mc.Get<IFusionCacheEntry>(preProcessedCacheKey)?.GetValue<int>());

		// DISTRIBUTED CACHE HAS -NOT- BEEN UPDATED
		Assert.Null(dc.GetString(preProcessedCacheKey));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task AppliesDistributedCacheHardTimeoutAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(1_000);
		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);

		using var mc = new MemoryCache(new MemoryCacheOptions());
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.DistributedCacheSoftTimeout = softTimeout;
		options.DefaultEntryOptions.DistributedCacheHardTimeout = hardTimeout;
		using var fc = new FusionCache(options, mc);
		var cache = new FusionHybridCache(fc);

		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		await cache.SetAsync<int>(keyFoo, 42);
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());
		mc.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		chaosdc.SetAlwaysDelayExactly(simulatedDelayMs);
		await Assert.ThrowsAsync<Exception>(async () =>
		{
			_ = await cache.GetOrCreateAsync<int>(keyFoo, _ => throw new Exception("Sloths are cool"));
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task AppliesDistributedCacheSoftTimeoutAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var logger = CreateXUnitLogger<FusionCache>();

		var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(500);
		var duration = TimeSpan.FromSeconds(1);

		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);

		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.DistributedCacheSoftTimeout = softTimeout;
		options.DefaultEntryOptions.DistributedCacheHardTimeout = hardTimeout;
		options.TagsDefaultEntryOptions.DistributedCacheSoftTimeout = softTimeout;
		options.TagsDefaultEntryOptions.DistributedCacheHardTimeout = hardTimeout;
		using var fc = new FusionCache(options, logger: logger);
		var cache = new FusionHybridCache(fc);

		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		await cache.SetAsync<int>(keyFoo, 42);
		await Task.Delay(duration.PlusALittleBit());

		chaosdc.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		var res = await cache.GetOrCreateAsync<int>(
			keyFoo,
			async _ => throw new Exception("Sloths are cool"),
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }
		);
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.Equal(42, res);
		Assert.True(elapsedMs >= 100, "Distributed cache soft timeout not applied (1)");
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds, "Distributed cache soft timeout not applied (2)");
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheCircuitBreakerActuallyWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var circuitBreakerDuration = TimeSpan.FromSeconds(2);
		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);

		using var mc = new MemoryCache(new MemoryCacheOptions());
		var options = CreateFusionCacheOptions();
		options.EnableAutoRecovery = false;
		options.DistributedCacheCircuitBreakerDuration = circuitBreakerDuration;
		using var fc = new FusionCache(options, mc);
		fc.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(60);
		fc.DefaultEntryOptions.IsFailSafeEnabled = true;
		fc.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 1);
		chaosdc.SetAlwaysThrow();
		await cache.SetAsync<int>(keyFoo, 2);
		chaosdc.SetNeverThrow();
		await cache.SetAsync<int>(keyFoo, 3);
		await Task.Delay(circuitBreakerDuration.PlusALittleBit());
		mc.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var res = await cache.GetOrDefaultAsync<int>(keyFoo, -1);

		Assert.Equal(1, res);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsOriginalExceptionsAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);

		chaosdc.SetAlwaysThrow();
		var options = CreateFusionCacheOptions();
		options.ReThrowOriginalExceptions = true;
		options.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		await Assert.ThrowsAsync<ChaosException>(async () =>
		{
			await cache.SetAsync<int>(keyFoo, 42);
		});

		await Assert.ThrowsAsync<ChaosException>(async () =>
		{
			_ = await cache.GetOrDefaultAsync<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsDistributedCacheExceptionsAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc);

		chaosdc.SetAlwaysThrow();
		using var fc = new FusionCache(CreateFusionCacheOptions());
		fc.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(async () =>
		{
			await cache.SetAsync<int>(keyFoo, 42);
		});

		await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(async () =>
		{
			_ = await cache.GetOrDefaultAsync<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsSerializationExceptionsAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();
		var options = CreateFusionCacheOptions(CreateRandomCacheName("foo"));
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromSeconds(10);
		using var fc = new FusionCache(options, logger: logger);
		var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));
		var dc = CreateDistributedCache();
		fc.SetupDistributedCache(dc, serializer);

		var cache = new FusionHybridCache(fc);

		logger.LogInformation("STEP 1");

		await cache.SetAsync<string>("foo", "sloths, sloths everywhere");

		logger.LogInformation("STEP 2");

		var foo1 = await cache.GetOrDefaultAsync<string>("foo");

		Assert.Equal("sloths, sloths everywhere", foo1);

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		logger.LogInformation("STEP 3");

		serializer.SetAlwaysThrow();

		logger.LogInformation("STEP 4");
		string? foo2 = null;
		await Assert.ThrowsAsync<FusionCacheSerializationException>(async () =>
		{
			foo2 = await cache.GetOrDefaultAsync<string>("foo");
		});

		Assert.Null(foo2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task SpecificDistributedCacheDurationWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromMinutes(1);
		using var fc = new FusionCache(options);
		fc.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 21);
		await Task.Delay(TimeSpan.FromSeconds(2));
		var value = await cache.GetOrDefaultAsync<int>(keyFoo);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task SpecificDistributedCacheDurationWithFailSafeWorksAsync(SerializerType serializerType)
	{
		var dc = CreateDistributedCache();
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromMinutes(1);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc = new FusionCache(options);
		fc.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 21);
		await Task.Delay(TimeSpan.FromSeconds(2));
		var value = await cache.GetOrDefaultAsync<int>("foo");
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheFailSafeMaxDurationNormalizationOccursAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		var dc = CreateDistributedCache();
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		options.DefaultEntryOptions.DistributedCacheFailSafeMaxDuration = maxDuration;
		using var fc = new FusionCache(options);
		fc.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 21);
		await Task.Delay(maxDuration.PlusALittleBit());
		var value = await cache.GetOrDefaultAsync<int>(keyFoo);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task MemoryExpirationAlignedWithDistributedAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(2);
		var secondDuration = TimeSpan.FromSeconds(10);

		var dc = CreateDistributedCache();
		using var fc1 = new FusionCache(CreateFusionCacheOptions());
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		using var fc2 = new FusionCache(CreateFusionCacheOptions());
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		await cache1.SetAsync<int>(keyFoo, 21, new HybridCacheEntryOptions { Expiration = firstDuration });
		await Task.Delay(firstDuration / 2);
		var v1 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 42, new HybridCacheEntryOptions { Expiration = secondDuration });
		await Task.Delay(firstDuration + TimeSpan.FromSeconds(1));
		var v2 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 42, new HybridCacheEntryOptions { Expiration = secondDuration });

		Assert.Equal(21, v1);
		Assert.Equal(42, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedCacheAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var dc = CreateDistributedCache();

		var options1 = CreateFusionCacheOptions();
		options1.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc1 = new FusionCache(options1);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc2 = new FusionCache(options2);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		var v1 = await cache1.GetOrCreateAsync<int>(keyFoo, async _ => 1, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10), Flags = HybridCacheEntryFlags.DisableDistributedCache });
		var v2 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 2);

		Assert.Equal(1, v1);
		Assert.Equal(2, v2);

		var v3 = await cache1.GetOrCreateAsync<int>(keyBar, async _ => 3);
		var v4 = await cache2.GetOrCreateAsync<int>(keyBar, async _ => 4, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(2), Flags = HybridCacheEntryFlags.DisableDistributedCache });

		Assert.Equal(3, v3);
		Assert.Equal(4, v4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedReadWhenStaleAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();

		var options1 = CreateFusionCacheOptions();
		options1.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(2);
		options1.DefaultEntryOptions.IsFailSafeEnabled = true;
		options1.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		using var fc1 = new FusionCache(options1);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		using var fc2 = new FusionCache(options2);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		var v1 = await cache1.GetOrCreateAsync<int>(keyFoo, async _ => 1);
		var v2 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 2);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);

		await Task.Delay(TimeSpan.FromSeconds(2).PlusALittleBit());

		v1 = await cache1.GetOrCreateAsync<int>(keyFoo, async _ => 3);
		v2 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 4);

		Assert.Equal(3, v1);
		Assert.Equal(4, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DoesNotSkipOnMemoryCacheMissWhenSkipDistributedCacheReadWhenStaleIsTrueAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();

		var options1 = CreateFusionCacheOptions();
		options1.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		using var fc1 = new FusionCache(options1);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		using var fc2 = new FusionCache(options2);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		await cache1.SetAsync(keyFoo, 21);

		var v1 = await cache1.GetOrDefaultAsync<int>(keyFoo);
		var v2 = await cache2.GetOrDefaultAsync<int>(keyFoo);

		Assert.True(v1 > 0);
		Assert.True(v2 > 0);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleEagerRefreshAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var dc = CreateDistributedCache();

		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(500));

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v6 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task EagerRefreshDoesNotBlockAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var logger = CreateXUnitLogger<FusionCache>();

		var duration = TimeSpan.FromSeconds(2);
		var syntheticDelay = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc, CreateXUnitLogger<ChaosDistributedCache>());

		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// SET DELAY
		chaosdc.SetAlwaysDelayExactly(syntheticDelay);

		// EAGER REFRESH KICKS IN
		var sw = Stopwatch.StartNew();
		var v3 = await cache.GetOrCreateAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < syntheticDelay.TotalMilliseconds);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipMemoryCacheAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = CreateDistributedCache();

		var options1 = CreateFusionCacheOptions();
		options1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		using var fc1 = new FusionCache(options1).SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		using var fc2 = new FusionCache(options2).SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		// SET ON CACHE 1 AND ON DISTRIBUTED CACHE
		var v1 = await cache1.GetOrCreateAsync<int>(keyFoo, async _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
		var v2 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 20);

		// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
		await cache1.SetAsync<int>(keyFoo, 30, new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache });

		// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
		var v3 = await cache1.GetOrCreateAsync<int>(keyFoo, async _ => 40);

		// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
		var v4 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 50);

		// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
		var v5 = await cache2.GetOrCreateAsync<int>(keyFoo, async _ => 60, new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache });

		Assert.Equal(10, v1);
		Assert.Equal(10, v2);
		Assert.Equal(10, v3);
		Assert.Equal(10, v4);
		Assert.Equal(30, v5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanExecuteBackgroundDistributedCacheOperationsAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var simulatedDelay = TimeSpan.FromMilliseconds(2_000);

		var logger = CreateXUnitLogger<FusionCache>();
		using var mc = new MemoryCache(new MemoryCacheOptions());
		var dc = CreateDistributedCache();
		var chaosdc = new ChaosDistributedCache(dc, CreateXUnitLogger<ChaosDistributedCache>());
		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(10);
		options.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = true;
		using var fc = new FusionCache(options, mc, logger);
		fc.SetupDistributedCache(chaosdc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 21);
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());

		chaosdc.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		// SHOULD RETURN IMMEDIATELY
		await cache.SetAsync<int>(keyFoo, 42);
		sw.Stop();
		logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.GetElapsedWithSafePad().TotalMilliseconds);

		await Task.Delay(TimeSpan.FromMilliseconds(200));

		chaosdc.SetNeverDelay();

		mc.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo1 = await cache.GetOrDefaultAsync<int>(keyFoo, -1);

		await Task.Delay(simulatedDelay.PlusALittleBit());

		mc.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo2 = await cache.GetOrDefaultAsync<int>(keyFoo, -1);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds);
		Assert.Equal(21, foo1);
		Assert.Equal(42, foo2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task FailSafeMaxDurationIsRespectedAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(2);
		var throttleDuration = TimeSpan.FromSeconds(1);
		var maxDuration = TimeSpan.FromSeconds(5);
		var exceptionMessage = "Sloths are cool";

		var options = CreateFusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;
		options.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;

		using var mc = new MemoryCache(new MemoryCacheOptions());
		var dc = CreateDistributedCache();
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 21);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit());
				await cache.GetOrCreateAsync<int>(keyFoo, async _ => throw new Exception(exceptionMessage));
			} while (sw.Elapsed < maxDuration + throttleDuration);
		}
		catch (Exception exc) when (exc.Message == exceptionMessage)
		{
			didThrow = true;
		}
		TestOutput.WriteLine($"-- END AT {DateTime.UtcNow}");
		sw.Stop();

		Assert.True(didThrow);
	}

	[Fact]
	public async Task CanPreferSyncSerializationAsync()
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var options = CreateFusionCacheOptions();
		options.PreferSyncSerialization = true;

		var dc = CreateDistributedCache();
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupDistributedCache(dc, new SyncOnlySerializer());

		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>(keyFoo, 21);
		var v1 = await cache.GetOrDefaultAsync<int>(keyFoo);

		Assert.Equal(21, v1);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var duration = TimeSpan.FromSeconds(4);
		var durationOverride = TimeSpan.FromMilliseconds(500);

		var dc = CreateDistributedCache();

		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options1.TagsDefaultEntryOptions.Duration = durationOverride;
		options1.SetInstanceId("C1");
		using var fc1 = new FusionCache(options1, logger: logger);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options2.TagsDefaultEntryOptions.Duration = durationOverride;
		options2.SetInstanceId("C2");
		using var fc2 = new FusionCache(options2, logger: logger);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		logger.LogInformation("STEP 1");

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y"]);
		await cache1.SetAsync<int>("bar", 2, tags: ["y", "z"]);
		await cache2.GetOrCreateAsync<int>("baz", async _ => 3, tags: ["x", "z"]);

		logger.LogInformation("STEP 2");

		var foo1 = await cache2.GetOrCreateAsync<int>("foo", async _ => 11, tags: ["x", "y"]);
		var bar1 = await cache2.GetOrCreateAsync<int>("bar", async _ => 22, tags: ["y", "z"]);
		var baz1 = await cache1.GetOrCreateAsync<int>("baz", async _ => 33, tags: ["x", "z"]);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		logger.LogInformation("STEP 3");

		await cache1.RemoveByTagAsync("x");
		await Task.Delay(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 4");

		var foo2 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar2 = await cache1.GetOrCreateAsync<int>("bar", async _ => 222, tags: ["y", "z"]);
		var baz2 = await cache2.GetOrCreateAsync<int>("baz", async _ => 333, tags: ["x", "z"]);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		logger.LogInformation("STEP 5");

		await cache2.RemoveByTagAsync("y");
		await Task.Delay(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 6");

		var foo3 = await cache2.GetOrCreateAsync<int>("foo", async _ => 1111, tags: ["x", "y"]);
		var bar3 = await cache2.GetOrCreateAsync<int>("bar", async _ => 2222, tags: ["y", "z"]);
		var baz3 = await cache1.GetOrCreateAsync<int>("baz", async _ => 3333, tags: ["x", "z"]);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanClearAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var durationOverride = TimeSpan.FromSeconds(2);

		var dc = CreateDistributedCache();

		// CACHE 1
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
		};
		options1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options1.DefaultEntryOptions.IsFailSafeEnabled = true;
		options1.TagsDefaultEntryOptions.Duration = durationOverride;
		options1.SetInstanceId("C1");
		using var fc1 = new FusionCache(options1, logger: logger);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		// CACHE 2
		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
		};
		options2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options2.DefaultEntryOptions.IsFailSafeEnabled = true;
		options2.TagsDefaultEntryOptions.Duration = durationOverride;
		options2.SetInstanceId("C2");
		using var fc2 = new FusionCache(options2, logger: logger);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		logger.LogInformation("STEP 1");

		await cache1.SetAsync<int>("foo", 1);
		await cache1.SetAsync<int>("bar", 2);

		logger.LogInformation("STEP 2");

		var foo2_1 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_1 = await cache2.GetOrDefaultAsync<int>("bar");

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);

		logger.LogInformation("STEP 3");

		await cache1.RemoveByTagAsync("*");
		await Task.Delay(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 4");

		var foo2_3 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_3 = await cache2.GetOrDefaultAsync<int>("bar");

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseMultiNodeCachesWithSizeLimitAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var key1 = "foo:" + Guid.NewGuid().ToString("N");
		var key2 = "bar:" + Guid.NewGuid().ToString("N");

		var dc = CreateDistributedCache();
		using var mc1 = new MemoryCache(new MemoryCacheOptions()
		{
			SizeLimit = 10
		});
		using var mc2 = new MemoryCache(new MemoryCacheOptions()
		{
			SizeLimit = 10
		});
		using var mc3 = new MemoryCache(new MemoryCacheOptions()
		{
			//SizeLimit = 10
		});

		var options1 = CreateFusionCacheOptions();
		options1.SetInstanceId("C1");
		options1.DefaultEntryOptions.Size = 1;
		using var fc1 = new FusionCache(options1, mc1, logger: logger);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.SetInstanceId("C2");
		using var fc2 = new FusionCache(options2, mc2, logger: logger);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options3 = CreateFusionCacheOptions();
		options3.SetInstanceId("C3");
		using var fc3 = new FusionCache(options3, mc3, logger: logger);
		fc3.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);
		var cache3 = new FusionHybridCache(fc3);

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		await cache1.SetAsync(key1, 1);

		await Task.Delay(1_000);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = await cache2.GetOrDefaultAsync<int>(key1);

		Assert.Equal(1, maybe2);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		await cache3.SetAsync(key2, 2);

		await Task.Delay(1_000);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE DEFAULT ENTRY OPTIONS
		var maybe1 = await cache1.GetOrDefaultAsync<int>(key2);

		Assert.Equal(2, maybe1);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe2bis = await cache2.GetOrDefaultAsync<int>(key2);

		Assert.Equal(2, maybe2bis);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseMultiNodeCachesWithPriorityAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var key1 = Guid.NewGuid().ToString("N");
		var key2 = Guid.NewGuid().ToString("N");

		var dc = CreateDistributedCache();
		using var mc1 = new MemoryCache(new MemoryCacheOptions());
		using var mc2 = new MemoryCache(new MemoryCacheOptions());

		var options1 = CreateFusionCacheOptions();
		options1.DisableTagging = true;
		options1.DefaultEntryOptions.Priority = CacheItemPriority.NeverRemove;
		using var fc1 = new FusionCache(options1, mc1, logger: logger);
		fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var options2 = CreateFusionCacheOptions();
		options2.DisableTagging = true;
		options2.DefaultEntryOptions.Priority = CacheItemPriority.NeverRemove;
		using var fc2 = new FusionCache(options2, mc2, logger: logger);
		fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var cache1 = new FusionHybridCache(fc1);
		var cache2 = new FusionHybridCache(fc2);

		// SET ENTRY WITH NeverRemove PRIORITY
		await cache1.SetAsync(key1, 1);
		// SET ENTRY WITH NeverRemove PRIORITY
		await cache1.SetAsync(key2, 1);

		// CACHE2 HERE DOES NOT HAVE ENTRIES YET
		Assert.Equal(2, mc1.Count);
		Assert.Equal(0, mc2.Count);

		var cache2_key1_1 = await cache2.GetOrCreateAsync<int>(key1, async _ => -1);
		var cache2_key2_1 = await cache2.GetOrCreateAsync<int>(key2, async _ => -1);

		Assert.Equal(1, cache2_key1_1);
		Assert.Equal(1, cache2_key2_1);

		// NOW BOTH CACHES HERE HAVE 2 ENTRIES
		Assert.Equal(2, mc1.Count);
		Assert.Equal(2, mc2.Count);

		await cache1.GetOrDefaultAsync<int>(key1);
		await cache1.GetOrDefaultAsync<int>(key2);

		// SAME AS BEFORE
		Assert.Equal(2, mc1.Count);
		Assert.Equal(2, mc2.Count);

		mc1.Compact(1);
		mc2.Compact(1);

		// BOTH CACHES HERE STILL HAVE 2 ENTRIES
		Assert.Equal(2, mc1.Count);
		Assert.Equal(2, mc2.Count);
	}
}
