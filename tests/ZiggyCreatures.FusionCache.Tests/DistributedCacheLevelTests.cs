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
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public class DistributedCacheLevelTests
	: AbstractTests
{
	private static readonly bool UseRedis = false;
	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

	public DistributedCacheLevelTests(ITestOutputHelper output)
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

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var initialValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(42), new FusionCacheEntryOptions().SetDurationSec(10));
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var newValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(21), new FusionCacheEntryOptions().SetDurationSec(10));
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ReturnsDataFromDistributedCacheIfNoDataInMemoryCache(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;

		var initialValue = fusionCache.GetOrSet<int>(keyFoo, _ => 42, options => options.SetDurationSec(10));
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var newValue = fusionCache.GetOrSet<int>(keyFoo, _ => 21, options => options.SetDurationSec(10));
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheFailuresAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		var initialValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(42), new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
		await Task.Delay(1_500);
		chaosDistributedCache.SetAlwaysThrow();
		var newValue = await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void HandlesDistributedCacheFailures(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		var initialValue = fusionCache.GetOrSet<int>(keyFoo, _ => 42, new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
		Thread.Sleep(1_500);
		chaosDistributedCache.SetAlwaysThrow();
		var newValue = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.Equal(initialValue, newValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var task = fusionCache.GetOrSetAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		await Task.Delay(500);
		fusionCache.RemoveDistributedCache();
		var value = await task;
		Assert.Equal(42, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheFailuresInTheMiddleOfAnOperationAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
		var options = CreateFusionCacheOptions();
		options.DistributedCacheKeyModifierMode = CacheKeyModifierMode.None;
		using var fusionCache = new FusionCache(options, memoryCache).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		var preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyBar, options.CacheKeyPrefix);

		await fusionCache.GetOrSetAsync<int>(keyBar, async _ => { return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		Assert.NotNull(distributedCache.GetString(preProcessedCacheKey));

		preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyFoo, options.CacheKeyPrefix);
		var task = fusionCache.GetOrSetAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		await Task.Delay(500);
		chaosDistributedCache.SetAlwaysThrow();
		var value = await task;
		chaosDistributedCache.SetNeverThrow();

		// END RESULT IS WHAT EXPECTED
		Assert.Equal(42, value);

		// MEMORY CACHE HAS BEEN UPDATED
		Assert.Equal(42, memoryCache.Get<IFusionCacheEntry>(preProcessedCacheKey)?.GetValue<int>());

		// DISTRIBUTED CACHE HAS -NOT- BEEN UPDATED
		Assert.Null(distributedCache.GetString(preProcessedCacheKey));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task AppliesDistributedCacheHardTimeoutAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(1_000);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await fusionCache.SetAsync<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
		await Assert.ThrowsAsync<Exception>(async () =>
		{
			_ = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void AppliesDistributedCacheHardTimeout(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(1_000);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache.Set<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
		Assert.Throws<Exception>(() =>
		{
			_ = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
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
		var hardTimeout = TimeSpan.FromMilliseconds(1_000);
		var duration = TimeSpan.FromSeconds(1);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true));
		await Task.Delay(duration.PlusALittleBit());
		var sw = Stopwatch.StartNew();
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var res = await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.Equal(42, res);
		Assert.True(elapsedMs >= 100, "Distributed cache soft timeout not applied");
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds, "Distributed cache soft timeout not applied");
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void AppliesDistributedCacheSoftTimeout(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var logger = CreateXUnitLogger<FusionCache>();

		var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(1_000);
		var duration = TimeSpan.FromSeconds(1);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true));
		Thread.Sleep(duration.PlusALittleBit());
		var sw = Stopwatch.StartNew();
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var res = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.Equal(42, res);
		Assert.True(elapsedMs >= 100, "Distributed cache soft timeout not applied");
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds, "Distributed cache soft timeout not applied");
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheCircuitBreakerActuallyWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var circuitBreakerDuration = TimeSpan.FromSeconds(2);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var options = CreateFusionCacheOptions();
		options.EnableAutoRecovery = false;
		options.DistributedCacheCircuitBreakerDuration = circuitBreakerDuration;
		using var fusionCache = new FusionCache(options, memoryCache);
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await fusionCache.SetAsync<int>(keyFoo, 1, options => options.SetDurationSec(60).SetFailSafe(true));
		chaosDistributedCache.SetAlwaysThrow();
		await fusionCache.SetAsync<int>(keyFoo, 2, options => options.SetDurationSec(60).SetFailSafe(true));
		chaosDistributedCache.SetNeverThrow();
		await fusionCache.SetAsync<int>(keyFoo, 3, options => options.SetDurationSec(60).SetFailSafe(true));
		await Task.Delay(circuitBreakerDuration.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var res = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1);

		Assert.Equal(1, res);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheCircuitBreakerActuallyWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var circuitBreakerDuration = TimeSpan.FromSeconds(2);
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var options = CreateFusionCacheOptions();
		options.EnableAutoRecovery = false;
		options.DistributedCacheCircuitBreakerDuration = circuitBreakerDuration;
		using var fusionCache = new FusionCache(options, memoryCache);
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache.Set<int>(keyFoo, 1, options => options.SetDurationSec(60).SetFailSafe(true));
		chaosDistributedCache.SetAlwaysThrow();
		fusionCache.Set<int>(keyFoo, 2, options => options.SetDurationSec(60).SetFailSafe(true));
		chaosDistributedCache.SetNeverThrow();
		fusionCache.Set<int>(keyFoo, 3, options => options.SetDurationSec(60).SetFailSafe(true));
		Thread.Sleep(circuitBreakerDuration.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var res = fusionCache.GetOrDefault<int>(keyFoo, -1);

		Assert.Equal(1, res);
	}

	private void _DistributedCacheWireVersionModifierWorks(SerializerType serializerType, CacheKeyModifierMode modifierMode)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		var options = CreateFusionCacheOptions();
		options.DistributedCacheKeyModifierMode = modifierMode;
		using var fusionCache = new FusionCache(options, memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyFoo, options.CacheKeyPrefix);
		var distributedCacheKey = modifierMode switch
		{
			CacheKeyModifierMode.Prefix => $"{FusionCacheOptions.DistributedCacheWireFormatVersion}{FusionCacheOptions.DistributedCacheWireFormatSeparator}{preProcessedCacheKey}",
			CacheKeyModifierMode.Suffix => $"{preProcessedCacheKey}{FusionCacheOptions.DistributedCacheWireFormatSeparator}{FusionCacheOptions.DistributedCacheWireFormatVersion}",
			_ => preProcessedCacheKey,
		};
		var value = "sloths";
		fusionCache.Set(keyFoo, value, new FusionCacheEntryOptions(TimeSpan.FromHours(24)) { AllowBackgroundDistributedCacheOperations = false });
		var nullValue = distributedCache.Get("foo42");
		var distributedValue = distributedCache.Get(distributedCacheKey);
		Assert.Null(nullValue);
		Assert.NotNull(distributedValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheWireVersionPrefixModeWorks(SerializerType serializerType)
	{
		_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.Prefix);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheWireVersionSuffixModeWorks(SerializerType serializerType)
	{
		_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.Suffix);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheWireVersionNoneModeWorks(SerializerType serializerType)
	{
		_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.None);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsOriginalExceptionsAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		chaosDistributedCache.SetAlwaysThrow();
		var options = CreateFusionCacheOptions();
		options.ReThrowOriginalExceptions = true;
		options.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
		using var fusionCache = new FusionCache(options);

		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await Assert.ThrowsAsync<ChaosException>(async () =>
		{
			await fusionCache.SetAsync<int>(keyFoo, 42);
		});

		await Assert.ThrowsAsync<ChaosException>(async () =>
		{
			_ = await fusionCache.TryGetAsync<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ReThrowsOriginalExceptions(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		chaosDistributedCache.SetAlwaysThrow();
		var options = CreateFusionCacheOptions();
		options.ReThrowOriginalExceptions = true;
		options.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
		using var fusionCache = new FusionCache(options);

		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		Assert.Throws<ChaosException>(() =>
		{
			fusionCache.Set<int>(keyFoo, 42);
		});

		Assert.Throws<ChaosException>(() =>
		{
			_ = fusionCache.TryGet<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsDistributedCacheExceptionsAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		chaosDistributedCache.SetAlwaysThrow();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions());
		fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(async () =>
		{
			await fusionCache.SetAsync<int>(keyFoo, 42);
		});

		await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(async () =>
		{
			_ = await fusionCache.TryGetAsync<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ReThrowsDistributedCacheExceptions(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		chaosDistributedCache.SetAlwaysThrow();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions());
		fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		Assert.Throws<FusionCacheDistributedCacheException>(() =>
		{
			fusionCache.Set<int>(keyFoo, 42);
		});

		Assert.Throws<FusionCacheDistributedCacheException>(() =>
		{
			_ = fusionCache.TryGet<int>(keyBar);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task ReThrowsSerializationExceptionsAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(CreateFusionCacheOptions(CreateRandomCacheName("foo")), logger: logger);
		var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));
		var distributedCache = CreateDistributedCache();
		cache.SetupDistributedCache(distributedCache, serializer);

		logger.LogInformation("STEP 1");

		await cache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

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
	public void ReThrowsSerializationExceptions(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(CreateFusionCacheOptions(CreateRandomCacheName("foo")), logger: logger);
		var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));
		var distributedCache = CreateDistributedCache();
		cache.SetupDistributedCache(distributedCache, serializer);

		logger.LogInformation("STEP 1");

		cache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

		logger.LogInformation("STEP 2");

		var foo1 = cache.GetOrDefault<string>("foo");

		Assert.Equal("sloths, sloths everywhere", foo1);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		logger.LogInformation("STEP 3");

		serializer.SetAlwaysThrow();

		logger.LogInformation("STEP 4");
		string? foo2 = null;
		Assert.Throws<FusionCacheSerializationException>(() =>
		{
			foo2 = cache.GetOrDefault<string>("foo");
		});

		Assert.Null(foo2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task SpecificDistributedCacheDurationWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
		await Task.Delay(TimeSpan.FromSeconds(2));
		var value = await fusionCache.GetOrDefaultAsync<int>(keyFoo);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void SpecificDistributedCacheDurationWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value = fusionCache.GetOrDefault<int>(keyFoo);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task SpecificDistributedCacheDurationWithFailSafeWorksAsync(SerializerType serializerType)
	{
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetFailSafe(true).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
		await Task.Delay(TimeSpan.FromSeconds(2));
		var value = await fusionCache.GetOrDefaultAsync<int>("foo");
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void SpecificDistributedCacheDurationWithFailSafeWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetFailSafe(true).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value = fusionCache.GetOrDefault<int>(keyFoo);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheFailSafeMaxDurationWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)));
		await Task.Delay(TimeSpan.FromSeconds(2));
		var value = await fusionCache.GetOrDefaultAsync<int>(keyFoo, opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheFailSafeMaxDurationWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)));
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value = fusionCache.GetOrDefault<int>(keyFoo, opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheFailSafeMaxDurationNormalizationOccursAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration));
		await Task.Delay(maxDuration.PlusALittleBit());
		var value = await fusionCache.GetOrDefaultAsync<int>(keyFoo, opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheFailSafeMaxDurationNormalizationOccurs(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration));
		Thread.Sleep(maxDuration.PlusALittleBit());
		var value = fusionCache.GetOrDefault<int>(keyFoo, opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task MemoryExpirationAlignedWithDistributedAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(4);
		var secondDuration = TimeSpan.FromSeconds(10);

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;

		await fusionCache1.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(firstDuration));
		await Task.Delay(firstDuration / 2);
		var v1 = await fusionCache2.GetOrDefaultAsync<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration));
		await Task.Delay(firstDuration + TimeSpan.FromSeconds(1));
		var v2 = await fusionCache2.GetOrDefaultAsync<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration));

		Assert.Equal(21, v1);
		Assert.Equal(42, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void MemoryExpirationAlignedWithDistributed(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(4);
		var secondDuration = TimeSpan.FromSeconds(10);

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;

		fusionCache1.Set<int>(keyFoo, 21, opt => opt.SetDuration(firstDuration));
		Thread.Sleep(firstDuration / 2);
		var v1 = fusionCache2.GetOrDefault<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration));
		Thread.Sleep(firstDuration + TimeSpan.FromSeconds(1));
		var v2 = fusionCache2.GetOrDefault<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration));

		Assert.Equal(21, v1);
		Assert.Equal(42, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedCacheAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true));
		var v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

		Assert.Equal(1, v1);
		Assert.Equal(2, v2);

		var v3 = await fusionCache1.GetOrSetAsync<int>(keyBar, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
		var v4 = await fusionCache2.GetOrSetAsync<int>(keyBar, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true));

		Assert.Equal(3, v3);
		Assert.Equal(4, v4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanSkipDistributedCache(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var keyBar = CreateRandomCacheKey("bar");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = fusionCache1.GetOrSet<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true));
		var v2 = fusionCache2.GetOrSet<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

		Assert.Equal(1, v1);
		Assert.Equal(2, v2);

		var v3 = fusionCache1.GetOrSet<int>(keyBar, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
		var v4 = fusionCache2.GetOrSet<int>(keyBar, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true));

		Assert.Equal(3, v3);
		Assert.Equal(4, v4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedReadWhenStaleAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));
		var v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);

		await Task.Delay(TimeSpan.FromSeconds(2).PlusALittleBit());

		v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
		v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

		Assert.Equal(3, v1);
		Assert.Equal(4, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanSkipDistributedReadWhenStale(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = fusionCache1.GetOrSet<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));
		var v2 = fusionCache2.GetOrSet<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);

		Thread.Sleep(TimeSpan.FromSeconds(2).PlusALittleBit());

		v1 = fusionCache1.GetOrSet<int>(keyFoo, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
		v2 = fusionCache2.GetOrSet<int>(keyFoo, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

		Assert.Equal(3, v1);
		Assert.Equal(4, v2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DoesNotSkipOnMemoryCacheMissWhenSkipDistributedCacheReadWhenStaleIsTrueAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache1.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		fusionCache2.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;

		await fusionCache1.SetAsync(keyFoo, 21);

		var v1 = await fusionCache1.TryGetAsync<int>(keyFoo);
		var v2 = await fusionCache2.TryGetAsync<int>(keyFoo);

		Assert.True(v1.HasValue);
		Assert.True(v2.HasValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DoesNotSkipOnMemoryCacheMissWhenSkipDistributedCacheReadWhenStaleIsTrue(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache1.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;
		fusionCache2.DefaultEntryOptions.SkipDistributedCacheReadWhenStale = true;

		fusionCache1.Set(keyFoo, 21);

		var v1 = fusionCache1.TryGet<int>(keyFoo);
		var v2 = fusionCache2.TryGet<int>(keyFoo);

		Assert.True(v1.HasValue);
		Assert.True(v2.HasValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleConditionalRefreshAsync(SerializerType serializerType)
	{
		static async Task<int> FakeGetAsync(FusionCacheFactoryExecutionContext<int> ctx, FakeHttpEndpoint endpoint)
		{
			FakeHttpResponse resp;

			if (ctx.HasETag && ctx.HasStaleValue)
			{
				// ETAG + STALE VALUE -> TRY WITH A CONDITIONAL GET
				resp = endpoint.Get(ctx.ETag);

				if (resp.StatusCode == 304)
				{
					// NOT MODIFIED -> RETURN STALE VALUE
					return ctx.NotModified();
				}
			}
			else
			{
				// NO STALE VALUE OR NO ETAG -> NORMAL (FULL) GET
				resp = endpoint.Get();
			}

			return ctx.Modified(
				resp.Content.GetValueOrDefault(),
				resp.ETag
			);
		}

		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(1);
		var endpoint = new FakeHttpEndpoint(1);

		var distributedCache = CreateDistributedCache();
		using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// TOT REQ + 1 / FULL RESP + 1
		var v1 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// CACHED -> NO INCR
		var v2 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		Assert.Equal(4, endpoint.TotalRequestsCount);
		Assert.Equal(3, endpoint.ConditionalRequestsCount);
		Assert.Equal(2, endpoint.FullResponsesCount);
		Assert.Equal(2, endpoint.NotModifiedResponsesCount);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(1, v4);
		Assert.Equal(42, v5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleConditionalRefresh(SerializerType serializerType)
	{
		static int FakeGet(FusionCacheFactoryExecutionContext<int> ctx, FakeHttpEndpoint endpoint)
		{
			FakeHttpResponse resp;

			if (ctx.HasETag && ctx.HasStaleValue)
			{
				// ETAG + STALE VALUE -> TRY WITH A CONDITIONAL GET
				resp = endpoint.Get(ctx.ETag);

				if (resp.StatusCode == 304)
				{
					// NOT MODIFIED -> RETURN STALE VALUE
					return ctx.NotModified();
				}
			}
			else
			{
				// NO STALE VALUE OR NO ETAG -> NORMAL (FULL) GET
				resp = endpoint.Get();
			}

			return ctx.Modified(
				resp.Content.GetValueOrDefault(),
				resp.ETag
			);
		}

		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(1);
		var endpoint = new FakeHttpEndpoint(1);

		var distributedCache = CreateDistributedCache();
		using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// TOT REQ + 1 / FULL RESP + 1
		var v1 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// CACHED -> NO INCR
		var v2 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		Assert.Equal(4, endpoint.TotalRequestsCount);
		Assert.Equal(3, endpoint.ConditionalRequestsCount);
		Assert.Equal(2, endpoint.FullResponsesCount);
		Assert.Equal(2, endpoint.NotModifiedResponsesCount);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(1, v4);
		Assert.Equal(42, v5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanHandleEagerRefreshAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var distributedCache = CreateDistributedCache();
		using var cache = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(500));

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v6 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanHandleEagerRefresh(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var distributedCache = CreateDistributedCache();
		using var cache = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(500));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v6 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

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

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		using var cache = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// SET DELAY
		chaosDistributedCache.SetAlwaysDelayExactly(syntheticDelay);

		// EAGER REFRESH KICKS IN
		var sw = Stopwatch.StartNew();
		var v3 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks);
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < syntheticDelay.TotalMilliseconds);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void EagerRefreshDoesNotBlock(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var logger = CreateXUnitLogger<FusionCache>();

		var duration = TimeSpan.FromSeconds(2);
		var syntheticDelay = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		using var cache = new FusionCache(CreateFusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// SET DELAY
		chaosDistributedCache.SetAlwaysDelayExactly(syntheticDelay);

		// EAGER REFRESH KICKS IN
		var sw = Stopwatch.StartNew();
		var v3 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks);
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

		var distributedCache = CreateDistributedCache();
		using var cache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var cache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		cache1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cache2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

		// SET ON CACHE 1 AND ON DISTRIBUTED CACHE
		var v1 = await cache1.GetOrSetAsync<int>(keyFoo, async _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
		var v2 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 20);

		// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
		await cache1.SetAsync<int>(keyFoo, 30, opt => opt.SetSkipMemoryCache());

		// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
		var v3 = await cache1.GetOrSetAsync<int>(keyFoo, async _ => 40);

		// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
		var v4 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 50);

		// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
		var v5 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 60, opt => opt.SetSkipMemoryCache());

		Assert.Equal(10, v1);
		Assert.Equal(10, v2);
		Assert.Equal(10, v3);
		Assert.Equal(10, v4);
		Assert.Equal(30, v5);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanSkipMemoryCache(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var cache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var cache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		cache1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		cache2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

		// SET ON CACHE 1 AND ON DISTRIBUTED CACHE
		var v1 = cache1.GetOrSet<int>(keyFoo, _ => 10);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
		var v2 = cache2.GetOrSet<int>(keyFoo, _ => 20);

		// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
		cache1.Set<int>(keyFoo, 30, opt => opt.SetSkipMemoryCache());

		// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
		var v3 = cache1.GetOrSet<int>(keyFoo, _ => 40);

		// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
		var v4 = cache2.GetOrSet<int>(keyFoo, _ => 50);

		// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
		var v5 = cache2.GetOrSet<int>(keyFoo, _ => 60, opt => opt.SetSkipMemoryCache());

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
		var eo = new FusionCacheEntryOptions().SetDurationSec(10);
		eo.AllowBackgroundDistributedCacheOperations = true;

		var logger = CreateXUnitLogger<FusionCache>();
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache, logger);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await fusionCache.SetAsync<int>(keyFoo, 21, eo);
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var sw = Stopwatch.StartNew();
		// SHOULD RETURN IMMEDIATELY
		await fusionCache.SetAsync<int>(keyFoo, 42, eo);
		sw.Stop();
		logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.GetElapsedWithSafePad().TotalMilliseconds);

		await Task.Delay(TimeSpan.FromMilliseconds(200));

		chaosDistributedCache.SetNeverDelay();
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo1 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1, eo);
		await Task.Delay(simulatedDelay.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo2 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1, eo);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds);
		Assert.Equal(21, foo1);
		Assert.Equal(42, foo2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanExecuteBackgroundDistributedCacheOperations(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
		var eo = new FusionCacheEntryOptions().SetDurationSec(10);
		eo.AllowBackgroundDistributedCacheOperations = true;

		var logger = CreateXUnitLogger<FusionCache>();
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateXUnitLogger<ChaosDistributedCache>());
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache, logger);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache.Set<int>(keyFoo, 21, eo);
		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var sw = Stopwatch.StartNew();
		// SHOULD RETURN IMMEDIATELY
		fusionCache.Set<int>(keyFoo, 42, eo);
		sw.Stop();
		logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.GetElapsedWithSafePad().TotalMilliseconds);
		Thread.Sleep(TimeSpan.FromMilliseconds(200));
		chaosDistributedCache.SetNeverDelay();
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo1 = fusionCache.GetOrDefault<int>(keyFoo, -1, eo);
		Thread.Sleep(simulatedDelay.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo2 = fusionCache.GetOrDefault<int>(keyFoo, -1, eo);

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

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fusionCache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		await fusionCache.SetAsync<int>(keyFoo, 21);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit());
				await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception(exceptionMessage));
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

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void FailSafeMaxDurationIsRespected(SerializerType serializerType)
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

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fusionCache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache.Set<int>(keyFoo, 21);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				Thread.Sleep(throttleDuration.PlusALittleBit());
				fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception(exceptionMessage));
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

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fusionCache.SetupDistributedCache(distributedCache, new SyncOnlySerializer());

		await fusionCache.SetAsync<int>(keyFoo, 21);
		var v1 = await fusionCache.TryGetAsync<int>(keyFoo);

		Assert.True(v1.HasValue);
		Assert.Equal(21, v1.Value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanRemoveByTagAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration = TimeSpan.FromSeconds(2);
		var durationOverride = TimeSpan.FromMilliseconds(500);

		var distributedCache = CreateDistributedCache();
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = durationOverride,
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options1.SetInstanceId("C1");
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = durationOverride,
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options2.SetInstanceId("C2");
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y"]);
		await cache1.SetAsync<int>("bar", 2, tags: ["y", "z"]);
		await cache2.GetOrSetAsync<int>("baz", async (_, _) => 3, tags: ["x", "z"]);

		var foo1 = await cache2.GetOrSetAsync<int>("foo", async (_, _) => 11, tags: ["x", "y"]);
		var bar1 = await cache2.GetOrSetAsync<int>("bar", async (_, _) => 22, tags: ["y", "z"]);
		var baz1 = await cache1.GetOrSetAsync<int>("baz", async (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		});

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		await cache1.RemoveByTagAsync("x");

		await Task.Delay(durationOverride);

		var foo2 = await cache1.GetOrDefaultAsync<int>("foo");
		var bar2 = await cache1.GetOrSetAsync<int>("bar", async (_, _) => 222, tags: ["y", "z"]);
		var baz2 = await cache2.GetOrSetAsync<int>("baz", async (_, _) => 333, tags: ["x", "z"]);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		await cache2.RemoveByTagAsync("y");

		await Task.Delay(durationOverride);

		var foo3 = await cache2.GetOrSetAsync<int>("foo", async (_, _) => 1111, tags: ["x", "y"]);
		var bar3 = await cache2.GetOrSetAsync<int>("bar", async (_, _) => 2222, tags: ["y", "z"]);
		var baz3 = await cache1.GetOrSetAsync<int>("baz", async (_, _) => 3333, tags: ["x", "z"]);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanRemoveByTag(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var duration = TimeSpan.FromSeconds(2);
		var durationOverride = TimeSpan.FromMilliseconds(500);

		var distributedCache = CreateDistributedCache();
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = durationOverride,
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options1.SetInstanceId("C1");
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = durationOverride,
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = duration
			}
		};
		options2.SetInstanceId("C2");
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		cache1.Set<int>("foo", 1, tags: ["x", "y"]);
		cache1.Set<int>("bar", 2, tags: ["y", "z"]);
		cache2.GetOrSet<int>("baz", (_, _) => 3, tags: ["x", "z"]);

		var foo1 = cache2.GetOrSet<int>("foo", (_, _) => 11, tags: ["x", "y"]);
		var bar1 = cache2.GetOrSet<int>("bar", (_, _) => 22, tags: ["y", "z"]);
		var baz1 = cache1.GetOrSet<int>("baz", (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		});

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		cache1.RemoveByTag("x");

		Thread.Sleep(durationOverride);

		var foo2 = cache1.GetOrDefault<int>("foo");
		var bar2 = cache1.GetOrSet<int>("bar", (_, _) => 222, tags: ["y", "z"]);
		var baz2 = cache2.GetOrSet<int>("baz", (_, _) => 333, tags: ["x", "z"]);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		cache2.RemoveByTag("y");

		Thread.Sleep(durationOverride);

		var foo3 = cache2.GetOrSet<int>("foo", (_, _) => 1111, tags: ["x", "y"]);
		var bar3 = cache2.GetOrSet<int>("bar", (_, _) => 2222, tags: ["y", "z"]);
		var baz3 = cache1.GetOrSet<int>("baz", (_, _) => 3333, tags: ["x", "z"]);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanClearAsync(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(4);
		var secondDuration = TimeSpan.FromSeconds(10);

		var distributedCache = CreateDistributedCache();
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = TimeSpan.FromSeconds(2)
		};
		options1.SetInstanceId("C1");
		using var cache1 = new FusionCache(options1);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = TimeSpan.FromSeconds(2)
		};
		options2.SetInstanceId("C2");
		using var cache2 = new FusionCache(options2);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		logger.LogInformation("STEP 1");

		await cache1.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromMinutes(10)));
		await cache1.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromMinutes(10)));
		await cache1.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromMinutes(10)));

		logger.LogInformation("STEP 2");

		var foo2_1 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_1 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2_1 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);
		Assert.Equal(3, baz2_1);

		logger.LogInformation("STEP 3");

		await cache1.ClearAsync();

		logger.LogInformation("STEP 4");

		var foo2_2 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_2 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2_2 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, foo2_2);
		Assert.Equal(2, bar2_2);
		Assert.Equal(3, baz2_2);

		logger.LogInformation("STEP 5");

		await Task.Delay(TimeSpan.FromSeconds(3));

		var foo2_3 = await cache2.GetOrDefaultAsync<int>("foo");
		var bar2_3 = await cache2.GetOrDefaultAsync<int>("bar");
		var baz2_3 = await cache2.GetOrDefaultAsync<int>("baz");

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);
		Assert.Equal(0, baz2_3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanClear(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = Guid.NewGuid().ToString("N");

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(4);
		var secondDuration = TimeSpan.FromSeconds(10);

		var distributedCache = CreateDistributedCache();
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = TimeSpan.FromSeconds(2)
		};
		options1.SetInstanceId("C1");
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
			TagsMemoryCacheDurationOverride = TimeSpan.FromSeconds(2)
		};
		options2.SetInstanceId("C2");
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromMinutes(10)));
		cache1.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromMinutes(10)));
		cache1.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromMinutes(10)));

		logger.LogInformation("STEP 2");

		var foo2_1 = cache2.GetOrDefault<int>("foo");
		var bar2_1 = cache2.GetOrDefault<int>("bar");
		var baz2_1 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);
		Assert.Equal(3, baz2_1);

		logger.LogInformation("STEP 3");

		cache1.Clear();

		logger.LogInformation("STEP 4");

		var foo2_2 = cache2.GetOrDefault<int>("foo");
		var bar2_2 = cache2.GetOrDefault<int>("bar");
		var baz2_2 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(1, foo2_2);
		Assert.Equal(2, bar2_2);
		Assert.Equal(3, baz2_2);

		logger.LogInformation("STEP 5");

		Thread.Sleep(TimeSpan.FromSeconds(3));

		var foo2_3 = cache2.GetOrDefault<int>("foo");
		var bar2_3 = cache2.GetOrDefault<int>("bar");
		var baz2_3 = cache2.GetOrDefault<int>("baz");

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);
		Assert.Equal(0, baz2_3);
	}
}
