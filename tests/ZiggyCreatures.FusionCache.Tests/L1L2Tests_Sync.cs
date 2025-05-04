using System.Diagnostics;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.DangerZone;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public partial class L1L2Tests
{
	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ReturnsDataFromDistributedCacheIfNoDataInMemoryCache(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		var initialValue = fusionCache.GetOrSet<int>(keyFoo, _ => 42, new FusionCacheEntryOptions().SetDurationSec(10), token: TestContext.Current.CancellationToken);
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var newValue = fusionCache.GetOrSet<int>(keyFoo, _ => 21, new FusionCacheEntryOptions().SetDurationSec(10), token: TestContext.Current.CancellationToken);
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
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		var initialValue = fusionCache.GetOrSet<int>(keyFoo, _ => 42, new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		chaosDistributedCache.SetAlwaysThrow();
		var newValue = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
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

		fusionCache.Set<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
		Assert.Throws<Exception>(() =>
		{
			_ = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout), token: TestContext.Current.CancellationToken);
		});
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void AppliesDistributedCacheSoftTimeout(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");
		var logger = CreateXUnitLogger<FusionCache>();

		var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
		var softTimeout = TimeSpan.FromMilliseconds(100);
		var hardTimeout = TimeSpan.FromMilliseconds(500);
		var duration = TimeSpan.FromSeconds(1);

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		var options = CreateFusionCacheOptions();
		options.TagsDefaultEntryOptions.DistributedCacheSoftTimeout = softTimeout;
		options.TagsDefaultEntryOptions.DistributedCacheHardTimeout = hardTimeout;
		using var fusionCache = new FusionCache(options, logger: logger);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		fusionCache.Set<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(duration.PlusALittleBit());

		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		var res = fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout)
, token: TestContext.Current.CancellationToken);
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.Equal(42, res);
		Assert.True(elapsedMs >= 100, "Distributed cache soft timeout not applied (1)");
		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds, "Distributed cache soft timeout not applied (2)");
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

		fusionCache.Set<int>(keyFoo, 1, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		chaosDistributedCache.SetAlwaysThrow();
		fusionCache.Set<int>(keyFoo, 2, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		chaosDistributedCache.SetNeverThrow();
		fusionCache.Set<int>(keyFoo, 3, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(circuitBreakerDuration.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var res = fusionCache.GetOrDefault<int>(keyFoo, -1, token: TestContext.Current.CancellationToken);

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
			CacheKeyModifierMode.Prefix => $"{FusionCacheOptions.DistributedCacheWireFormatVersion}{options.InternalStrings.DistributedCacheWireFormatSeparator}{preProcessedCacheKey}",
			CacheKeyModifierMode.Suffix => $"{preProcessedCacheKey}{options.InternalStrings.DistributedCacheWireFormatSeparator}{FusionCacheOptions.DistributedCacheWireFormatVersion}",
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
			fusionCache.Set<int>(keyFoo, 42, token: TestContext.Current.CancellationToken);
		});

		Assert.Throws<ChaosException>(() =>
		{
			_ = fusionCache.TryGet<int>(keyBar, token: TestContext.Current.CancellationToken);
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
			fusionCache.Set<int>(keyFoo, 42, token: TestContext.Current.CancellationToken);
		});

		Assert.Throws<FusionCacheDistributedCacheException>(() =>
		{
			_ = fusionCache.TryGet<int>(keyBar, token: TestContext.Current.CancellationToken);
		});
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

		cache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo1 = cache.GetOrDefault<string>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal("sloths, sloths everywhere", foo1);

		Thread.Sleep(TimeSpan.FromMilliseconds(100).PlusALittleBit());

		logger.LogInformation("STEP 3");

		serializer.SetAlwaysThrow();

		logger.LogInformation("STEP 4");
		string? foo2 = null;
		Assert.Throws<FusionCacheSerializationException>(() =>
		{
			foo2 = cache.GetOrDefault<string>("foo", token: TestContext.Current.CancellationToken);
		});

		Assert.Null(foo2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void SpecificDistributedCacheDurationWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value = fusionCache.GetOrDefault<int>(keyFoo, token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void SpecificDistributedCacheDurationWithFailSafeWorks(SerializerType serializerType)
	{
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>("foo", 21, opt => opt.SetFailSafe(true).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value = fusionCache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void DistributedCacheFailSafeMaxDurationWorks(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(2));
		var value1 = fusionCache.GetOrDefault<int>(keyFoo, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value1);
		var value2 = fusionCache.GetOrDefault<int>(keyFoo, token: TestContext.Current.CancellationToken);
		Assert.Equal(0, value2);
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
		fusionCache.Set<int>(keyFoo, 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration), token: TestContext.Current.CancellationToken);
		Thread.Sleep(maxDuration.PlusALittleBit());
		var value = fusionCache.GetOrDefault<int>(keyFoo, opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void MemoryExpirationAlignedWithDistributed(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var firstDuration = TimeSpan.FromSeconds(2);
		var secondDuration = TimeSpan.FromSeconds(10);

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions())
			.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
		;

		fusionCache1.Set<int>(keyFoo, 21, opt => opt.SetDuration(firstDuration), token: TestContext.Current.CancellationToken);
		Thread.Sleep(firstDuration / 2);
		var v1 = fusionCache2.GetOrDefault<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration), token: TestContext.Current.CancellationToken);
		Thread.Sleep(firstDuration + TimeSpan.FromSeconds(1));
		var v2 = fusionCache2.GetOrDefault<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration), token: TestContext.Current.CancellationToken);

		Assert.Equal(21, v1);
		Assert.Equal(42, v2);
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

		var v1 = fusionCache1.GetOrSet<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true), token: TestContext.Current.CancellationToken);
		var v2 = fusionCache2.GetOrSet<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, v1);
		Assert.Equal(2, v2);

		var v3 = fusionCache1.GetOrSet<int>(keyBar, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		var v4 = fusionCache2.GetOrSet<int>(keyBar, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true), token: TestContext.Current.CancellationToken);

		Assert.Equal(3, v3);
		Assert.Equal(4, v4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanSkipDistributedReadWhenStale(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = fusionCache1.GetOrSet<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);
		var v2 = fusionCache2.GetOrSet<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);

		Thread.Sleep(TimeSpan.FromSeconds(2).PlusALittleBit());

		v1 = fusionCache1.GetOrSet<int>(keyFoo, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		v2 = fusionCache2.GetOrSet<int>(keyFoo, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(3, v1);
		Assert.Equal(4, v2);
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

		fusionCache1.Set(keyFoo, 21, token: TestContext.Current.CancellationToken);

		var v1 = fusionCache1.TryGet<int>(keyFoo, token: TestContext.Current.CancellationToken);
		var v2 = fusionCache2.TryGet<int>(keyFoo, token: TestContext.Current.CancellationToken);

		Assert.True(v1.HasValue);
		Assert.True(v2.HasValue);
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
		var v1 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// CACHED -> NO INCR
		var v2 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = cache.GetOrSet<int>(keyFoo, (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

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
		var v1 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(500));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v6 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
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
		var v1 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// SET DELAY
		chaosDistributedCache.SetAlwaysDelayExactly(syntheticDelay);

		// EAGER REFRESH KICKS IN
		var sw = Stopwatch.StartNew();
		var v3 = cache.GetOrSet<long>(keyFoo, _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < syntheticDelay.TotalMilliseconds);
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
		var v1 = cache1.GetOrSet<int>(keyFoo, _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
		var v2 = cache2.GetOrSet<int>(keyFoo, _ => 20, token: TestContext.Current.CancellationToken);

		// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
		cache1.Set<int>(keyFoo, 30, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);

		// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
		var v3 = cache1.GetOrSet<int>(keyFoo, _ => 40, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
		var v4 = cache2.GetOrSet<int>(keyFoo, _ => 50, token: TestContext.Current.CancellationToken);

		// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
		var v5 = cache2.GetOrSet<int>(keyFoo, _ => 60, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);

		Assert.Equal(10, v1);
		Assert.Equal(10, v2);
		Assert.Equal(10, v3);
		Assert.Equal(10, v4);
		Assert.Equal(30, v5);
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

		fusionCache.Set<int>(keyFoo, 21, eo, token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var sw = Stopwatch.StartNew();
		// SHOULD RETURN IMMEDIATELY
		fusionCache.Set<int>(keyFoo, 42, eo, token: TestContext.Current.CancellationToken);
		sw.Stop();
		logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.GetElapsedWithSafePad().TotalMilliseconds);

		Thread.Sleep(TimeSpan.FromMilliseconds(200));
		chaosDistributedCache.SetNeverDelay();
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo1 = fusionCache.GetOrDefault<int>(keyFoo, -1, eo, TestContext.Current.CancellationToken);
		Thread.Sleep(simulatedDelay.PlusALittleBit());
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo2 = fusionCache.GetOrDefault<int>(keyFoo, -1, eo, TestContext.Current.CancellationToken);

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;
		logger.LogTrace("Elapsed (with extra pad): {ElapsedMs} ms", elapsedMs);

		Assert.True(elapsedMs < simulatedDelay.TotalMilliseconds);
		Assert.Equal(21, foo1);
		Assert.Equal(42, foo2);
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

		fusionCache.Set<int>(keyFoo, 21, token: TestContext.Current.CancellationToken);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				Thread.Sleep(throttleDuration.PlusALittleBit());
				fusionCache.GetOrSet<int>(keyFoo, _ => throw new Exception(exceptionMessage), token: TestContext.Current.CancellationToken);
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
	public void CanRemoveByTag(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var duration = TimeSpan.FromSeconds(4);
		var durationOverride = TimeSpan.FromMilliseconds(500);

		var distributedCache = CreateDistributedCache();
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
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

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
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		cache1.Set<int>("bar", 2, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		cache2.GetOrSet<int>("baz", _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo1 = cache2.GetOrSet<int>("foo", _ => 11, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar1 = cache2.GetOrSet<int>("bar", _ => 22, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz1 = cache1.GetOrSet<int>("baz", (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		}, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		logger.LogInformation("STEP 3");

		cache1.RemoveByTag("x", token: TestContext.Current.CancellationToken);
		Thread.Sleep(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 4");

		var foo2 = cache1.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = cache1.GetOrSet<int>("bar", _ => 222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz2 = cache2.GetOrSet<int>("baz", _ => 333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		logger.LogInformation("STEP 5");

		cache2.RemoveByTag("y", token: TestContext.Current.CancellationToken);
		Thread.Sleep(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 6");

		var foo3 = cache2.GetOrSet<int>("foo", _ => 1111, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar3 = cache2.GetOrSet<int>("bar", _ => 2222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz3 = cache1.GetOrSet<int>("baz", _ => 3333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanClear(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var durationOverride = TimeSpan.FromSeconds(2);

		var distributedCache = CreateDistributedCache();

		// CACHE 1
		var options1 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
		};
		options1.TagsDefaultEntryOptions.Duration = durationOverride;
		options1.SetInstanceId("C1");
		using var cache1 = new FusionCache(options1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// CACHE 2
		var options2 = new FusionCacheOptions
		{
			CacheName = cacheName,
			CacheKeyPrefix = cacheName + ":",
		};
		options2.TagsDefaultEntryOptions.Duration = durationOverride;
		options2.SetInstanceId("C2");
		using var cache2 = new FusionCache(options2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, options => options.SetFailSafe(true).SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		cache1.Set<int>("bar", 2, options => options.SetFailSafe(true).SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo2_1 = cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_1 = cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);

		logger.LogInformation("STEP 3");

		cache1.Clear(token: TestContext.Current.CancellationToken);
		Thread.Sleep(durationOverride.PlusALittleBit());

		logger.LogInformation("STEP 4");

		var foo2_3 = cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_3 = cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);

		logger.LogInformation("STEP 5");

		var foo2_4 = cache2.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var bar2_4 = cache2.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2_4);
		Assert.Equal(2, bar2_4);

		logger.LogInformation("STEP 6");

		cache1.Clear(false, token: TestContext.Current.CancellationToken);
		Thread.Sleep(TimeSpan.FromSeconds(3));

		logger.LogInformation("STEP 7");

		var foo2_5 = cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_5 = cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_5);
		Assert.Equal(0, bar2_5);

		logger.LogInformation("STEP 8");

		var foo2_6 = cache2.GetOrDefault<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var bar2_6 = cache2.GetOrDefault<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_6);
		Assert.Equal(0, bar2_6);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanUseMultiNodeCachesWithSizeLimit(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

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

		using var cache1 = new FusionCache(CreateFusionCacheOptions(configure: opt => opt.DefaultEntryOptions.SetSize(1)), memoryCache1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache2 = new FusionCache(CreateFusionCacheOptions(), memoryCache2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache3 = new FusionCache(CreateFusionCacheOptions(), memoryCache3, logger: logger);
		cache3.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// SET THE ENTRY (WITH SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		cache1.Set(key1, 1, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		Thread.Sleep(1_000);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = cache2.TryGet<int>(key1, token: TestContext.Current.CancellationToken);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		cache3.Set(key2, 2, token: TestContext.Current.CancellationToken);

		Thread.Sleep(1_000);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE DEFAULT ENTRY OPTIONS
		var maybe1 = cache1.TryGet<int>(key2/*, options => options.SetSize(1)*/, token: TestContext.Current.CancellationToken);

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe2bis = cache2.TryGet<int>(key2, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		Assert.True(maybe2bis.HasValue);
		Assert.Equal(2, maybe2bis.Value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanUseMultiNodeCachesWithPriority(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var key1 = Guid.NewGuid().ToString("N");
		var key2 = Guid.NewGuid().ToString("N");

		var distributedCache = CreateDistributedCache();
		using var memoryCache1 = new MemoryCache(new MemoryCacheOptions());
		using var memoryCache2 = new MemoryCache(new MemoryCacheOptions());

		using var cache1 = new FusionCache(CreateFusionCacheOptions(configure: opt => opt.DisableTagging = true), memoryCache1, logger: logger);
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache2 = new FusionCache(CreateFusionCacheOptions(configure: opt => opt.DisableTagging = true), memoryCache2, logger: logger);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// SET ENTRY WITH Low PRIORITY
		cache1.Set(key1, 1, options => options.SetPriority(CacheItemPriority.Low), token: TestContext.Current.CancellationToken);
		// SET ENTRY WITH NeverRemove PRIORITY
		cache1.Set(key2, 1, options => options.SetPriority(CacheItemPriority.NeverRemove), token: TestContext.Current.CancellationToken);

		// CACHE2 HERE DOES NOT HAVE ENTRIES YET
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(0, memoryCache2.Count);

		cache2.TryGet<int>(key1, token: TestContext.Current.CancellationToken);
		cache2.TryGet<int>(key2, token: TestContext.Current.CancellationToken);

		// NOW BOTH CACHES HERE HAVE 2 ENTRIES
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);

		cache1.TryGet<int>(key1, token: TestContext.Current.CancellationToken);
		cache1.TryGet<int>(key2, token: TestContext.Current.CancellationToken);

		// SAME AS BEFORE
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);

		memoryCache1.Compact(1);
		memoryCache2.Compact(1);

		// NOW BOTH CACHES HERE HAVE ONLY 1 ENTRY
		Assert.Equal(1, memoryCache1.Count);
		Assert.Equal(1, memoryCache2.Count);

		cache2.TryGet<int>(key1, token: TestContext.Current.CancellationToken);
		cache2.TryGet<int>(key2, token: TestContext.Current.CancellationToken);

		// NOW CACHE2 HAS 2 ENTRIES AGAIN, CACHE1 ONLY 1
		Assert.Equal(1, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);
	}
}
