using System.Diagnostics;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
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
	public async Task ReturnsDataFromDistributedCacheIfNoDataInMemoryCacheAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		var initialValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(42), new FusionCacheEntryOptions().SetDurationSec(10), token: TestContext.Current.CancellationToken);
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var newValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(21), new FusionCacheEntryOptions().SetDurationSec(10), token: TestContext.Current.CancellationToken);
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
		fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
		var initialValue = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => Task.FromResult(42), new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		chaosDistributedCache.SetAlwaysThrow();
		var newValue = await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
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

		await fusionCache.SetAsync<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit(), TestContext.Current.CancellationToken);
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
		await Assert.ThrowsAsync<Exception>(async () =>
		{
			_ = await fusionCache.GetOrSetAsync<int>(keyFoo, _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout), token: TestContext.Current.CancellationToken);
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

		var distributedCache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

		var options = CreateFusionCacheOptions();
		options.TagsDefaultEntryOptions.DistributedCacheSoftTimeout = softTimeout;
		options.TagsDefaultEntryOptions.DistributedCacheHardTimeout = hardTimeout;
		using var fusionCache = new FusionCache(options, logger: logger);
		fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		await fusionCache.SetAsync<int>(keyFoo, 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);

		var sw = Stopwatch.StartNew();
		var res = await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout)
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

		await fusionCache.SetAsync<int>(keyFoo, 1, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		chaosDistributedCache.SetAlwaysThrow();
		await fusionCache.SetAsync<int>(keyFoo, 2, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		chaosDistributedCache.SetNeverThrow();
		await fusionCache.SetAsync<int>(keyFoo, 3, options => options.SetDurationSec(60).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(circuitBreakerDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var res = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, res);
	}

	private async Task _DistributedCacheWireVersionModifierWorksAsync(SerializerType serializerType, CacheKeyModifierMode modifierMode)
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
		await fusionCache.SetAsync(keyFoo, value, new FusionCacheEntryOptions(TimeSpan.FromHours(24)) { AllowBackgroundDistributedCacheOperations = false });
		var nullValue = distributedCache.Get("foo42");
		var distributedValue = distributedCache.Get(distributedCacheKey);
		Assert.Null(nullValue);
		Assert.NotNull(distributedValue);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheWireVersionPrefixModeWorksAsync(SerializerType serializerType)
	{
		await _DistributedCacheWireVersionModifierWorksAsync(serializerType, CacheKeyModifierMode.Prefix);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheWireVersionSuffixModeWorksAsync(SerializerType serializerType)
	{
		await _DistributedCacheWireVersionModifierWorksAsync(serializerType, CacheKeyModifierMode.Suffix);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheWireVersionNoneModeWorksAsync(SerializerType serializerType)
	{
		await _DistributedCacheWireVersionModifierWorksAsync(serializerType, CacheKeyModifierMode.None);
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
			await fusionCache.SetAsync<int>(keyFoo, 42, token: TestContext.Current.CancellationToken);
		});

		await Assert.ThrowsAsync<ChaosException>(async () =>
		{
			_ = await fusionCache.TryGetAsync<int>(keyBar, token: TestContext.Current.CancellationToken);
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
			await fusionCache.SetAsync<int>(keyFoo, 42, token: TestContext.Current.CancellationToken);
		});

		await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(async () =>
		{
			_ = await fusionCache.TryGetAsync<int>(keyBar, token: TestContext.Current.CancellationToken);
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

		await cache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo1 = await cache.GetOrDefaultAsync<string>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal("sloths, sloths everywhere", foo1);

		await Task.Delay(TimeSpan.FromMilliseconds(100).PlusALittleBit(), TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 3");

		serializer.SetAlwaysThrow();

		logger.LogInformation("STEP 4");
		string? foo2 = null;
		await Assert.ThrowsAsync<FusionCacheSerializationException>(async () =>
		{
			foo2 = await cache.GetOrDefaultAsync<string>("foo", token: TestContext.Current.CancellationToken);
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
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
		var value = await fusionCache.GetOrDefaultAsync<int>(keyFoo, token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task SpecificDistributedCacheDurationWithFailSafeWorksAsync(SerializerType serializerType)
	{
		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetFailSafe(true).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
		var value = await fusionCache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task DistributedCacheFailSafeMaxDurationWorksAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
		var value1 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value1);
		var value2 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, token: TestContext.Current.CancellationToken);
		Assert.Equal(0, value2);
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
		await fusionCache.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration), token: TestContext.Current.CancellationToken);
		await Task.Delay(maxDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
		var value = await fusionCache.GetOrDefaultAsync<int>(keyFoo, opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task MemoryExpirationAlignedWithDistributedAsync(SerializerType serializerType)
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

		await fusionCache1.SetAsync<int>(keyFoo, 21, opt => opt.SetDuration(firstDuration), token: TestContext.Current.CancellationToken);
		await Task.Delay(firstDuration / 2, TestContext.Current.CancellationToken);
		var v1 = await fusionCache2.GetOrDefaultAsync<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration), token: TestContext.Current.CancellationToken);
		await Task.Delay(firstDuration + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		var v2 = await fusionCache2.GetOrDefaultAsync<int>(keyFoo, 42, opt => opt.SetDuration(secondDuration), token: TestContext.Current.CancellationToken);

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

		var v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true), token: TestContext.Current.CancellationToken);
		var v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, v1);
		Assert.Equal(2, v2);

		var v3 = await fusionCache1.GetOrSetAsync<int>(keyBar, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		var v4 = await fusionCache2.GetOrSetAsync<int>(keyBar, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true), token: TestContext.Current.CancellationToken);

		Assert.Equal(3, v3);
		Assert.Equal(4, v4);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedCacheOnReadOnlyAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var dc = new ChaosDistributedCache(CreateDistributedCache());
		dc.SetAlwaysThrow();
		using var fc = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

		var v1 = await fc.GetOrDefaultAsync<int>(
			keyFoo,
			options =>
			{
				options.ReThrowDistributedCacheExceptions = true;
				options.SkipDistributedCacheRead = true;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(0, v1);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanSkipDistributedReadWhenStaleAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);
		var v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);

		await Task.Delay(TimeSpan.FromSeconds(2).PlusALittleBit(), TestContext.Current.CancellationToken);

		v1 = await fusionCache1.GetOrSetAsync<int>(keyFoo, 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		v2 = await fusionCache2.GetOrSetAsync<int>(keyFoo, 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true), token: TestContext.Current.CancellationToken);

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

		await fusionCache1.SetAsync(keyFoo, 21, token: TestContext.Current.CancellationToken);

		var v1 = await fusionCache1.TryGetAsync<int>(keyFoo, token: TestContext.Current.CancellationToken);
		var v2 = await fusionCache2.TryGetAsync<int>(keyFoo, token: TestContext.Current.CancellationToken);

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
		var v1 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// CACHED -> NO INCR
		var v2 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = await cache.GetOrSetAsync<int>(keyFoo, async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

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
		var v1 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN
		var v3 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v6 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

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
		var v1 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// SET DELAY
		chaosDistributedCache.SetAlwaysDelayExactly(syntheticDelay);

		// EAGER REFRESH KICKS IN
		var sw = Stopwatch.StartNew();
		var v3 = await cache.GetOrSetAsync<long>(keyFoo, async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);
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
		var v1 = await cache1.GetOrSetAsync<int>(keyFoo, async _ => 10, token: TestContext.Current.CancellationToken);

		// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
		var v2 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 20, token: TestContext.Current.CancellationToken);

		// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
		await cache1.SetAsync<int>(keyFoo, 30, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);

		// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
		var v3 = await cache1.GetOrSetAsync<int>(keyFoo, async _ => 40, token: TestContext.Current.CancellationToken);

		// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
		var v4 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 50, token: TestContext.Current.CancellationToken);

		// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
		var v5 = await cache2.GetOrSetAsync<int>(keyFoo, async _ => 60, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);

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

		await fusionCache.SetAsync<int>(keyFoo, 21, eo, token: TestContext.Current.CancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit(), TestContext.Current.CancellationToken);
		chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
		var sw = Stopwatch.StartNew();
		// SHOULD RETURN IMMEDIATELY
		await fusionCache.SetAsync<int>(keyFoo, 42, eo, token: TestContext.Current.CancellationToken);
		sw.Stop();
		logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.GetElapsedWithSafePad().TotalMilliseconds);

		await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

		chaosDistributedCache.SetNeverDelay();
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo1 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1, eo, TestContext.Current.CancellationToken);
		await Task.Delay(simulatedDelay.PlusALittleBit(), TestContext.Current.CancellationToken);
		memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey(keyFoo, TestingCacheKeyPrefix));
		var foo2 = await fusionCache.GetOrDefaultAsync<int>(keyFoo, -1, eo, TestContext.Current.CancellationToken);

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

		await fusionCache.SetAsync<int>(keyFoo, 21, token: TestContext.Current.CancellationToken);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
				await fusionCache.GetOrSetAsync<int>(keyFoo, async _ => throw new Exception(exceptionMessage), token: TestContext.Current.CancellationToken);
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
	public async Task CanRemoveByTagAsync(SerializerType serializerType)
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

		await cache1.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		await cache2.GetOrSetAsync<int>("baz", async _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo1 = await cache2.GetOrSetAsync<int>("foo", async _ => 11, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar1 = await cache2.GetOrSetAsync<int>("bar", async _ => 22, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz1 = await cache1.GetOrSetAsync<int>("baz", async (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		}, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		logger.LogInformation("STEP 3");

		await cache1.RemoveByTagAsync("x", token: TestContext.Current.CancellationToken);
		await Task.Delay(durationOverride.PlusALittleBit(), TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 4");

		var foo2 = await cache1.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = await cache1.GetOrSetAsync<int>("bar", async _ => 222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz2 = await cache2.GetOrSetAsync<int>("baz", async _ => 333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		logger.LogInformation("STEP 5");

		await cache2.RemoveByTagAsync("y", token: TestContext.Current.CancellationToken);
		await Task.Delay(durationOverride.PlusALittleBit(), TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 6");

		var foo3 = await cache2.GetOrSetAsync<int>("foo", async _ => 1111, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar3 = await cache2.GetOrSetAsync<int>("bar", async _ => 2222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz3 = await cache1.GetOrSetAsync<int>("baz", async _ => 3333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

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

		await cache1.SetAsync<int>("foo", 1, options => options.SetFailSafe(true).SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		await cache1.SetAsync<int>("bar", 2, options => options.SetFailSafe(true).SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo2_1 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_1 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2_1);
		Assert.Equal(2, bar2_1);

		logger.LogInformation("STEP 3");

		await cache1.ClearAsync(token: TestContext.Current.CancellationToken);
		await Task.Delay(durationOverride.PlusALittleBit(), TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 4");

		var foo2_3 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_3 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);

		logger.LogInformation("STEP 5");

		var foo2_4 = await cache2.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var bar2_4 = await cache2.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2_4);
		Assert.Equal(2, bar2_4);

		logger.LogInformation("STEP 6");

		await cache1.ClearAsync(false, token: TestContext.Current.CancellationToken);
		await Task.Delay(durationOverride.PlusALittleBit(), TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 7");

		var foo2_5 = await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_5 = await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_5);
		Assert.Equal(0, bar2_5);

		logger.LogInformation("STEP 8");

		var foo2_6 = await cache2.GetOrDefaultAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var bar2_6 = await cache2.GetOrDefaultAsync<int>("bar", opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_6);
		Assert.Equal(0, bar2_6);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseMultiNodeCachesWithSizeLimitAsync(SerializerType serializerType)
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
		await cache1.SetAsync(key1, 1, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		await Task.Delay(1_000, TestContext.Current.CancellationToken);

		// GET THE ENTRY (WITH SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		var maybe2 = await cache2.TryGetAsync<int>(key1, token: TestContext.Current.CancellationToken);

		Assert.True(maybe2.HasValue);
		Assert.Equal(1, maybe2.Value);

		// SET THE ENTRY (WITH NO SIZE) ON CACHE 3 (WITH NO SIZE LIMIT)
		await cache3.SetAsync(key2, 2, token: TestContext.Current.CancellationToken);

		await Task.Delay(1_000, TestContext.Current.CancellationToken);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 1 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE DEFAULT ENTRY OPTIONS
		var maybe1 = await cache1.TryGetAsync<int>(key2/*, options => options.SetSize(1)*/, token: TestContext.Current.CancellationToken);

		Assert.True(maybe1.HasValue);
		Assert.Equal(2, maybe1.Value);

		// GET THE ENTRY (WITH NO SIZE) ON CACHE 2 (WITH SIZE LIMIT)
		// -> FALLBACK TO THE SIZE IN THE ENTRY OPTIONS
		var maybe2bis = await cache2.TryGetAsync<int>(key2, options => options.SetSize(1), token: TestContext.Current.CancellationToken);

		Assert.True(maybe2bis.HasValue);
		Assert.Equal(2, maybe2bis.Value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanUseMultiNodeCachesWithPriorityAsync(SerializerType serializerType)
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
		await cache1.SetAsync(key1, 1, options => options.SetPriority(CacheItemPriority.Low), token: TestContext.Current.CancellationToken);
		// SET ENTRY WITH NeverRemove PRIORITY
		await cache1.SetAsync(key2, 1, options => options.SetPriority(CacheItemPriority.NeverRemove), token: TestContext.Current.CancellationToken);

		// CACHE2 HERE DOES NOT HAVE ENTRIES YET
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(0, memoryCache2.Count);

		await cache2.TryGetAsync<int>(key1, token: TestContext.Current.CancellationToken);
		await cache2.TryGetAsync<int>(key2, token: TestContext.Current.CancellationToken);

		// NOW BOTH CACHES HERE HAVE 2 ENTRIES
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);

		await cache1.TryGetAsync<int>(key1, token: TestContext.Current.CancellationToken);
		await cache1.TryGetAsync<int>(key2, token: TestContext.Current.CancellationToken);

		// SAME AS BEFORE
		Assert.Equal(2, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);

		memoryCache1.Compact(1);
		memoryCache2.Compact(1);

		// NOW BOTH CACHES HERE HAVE ONLY 1 ENTRY
		Assert.Equal(1, memoryCache1.Count);
		Assert.Equal(1, memoryCache2.Count);

		await cache2.TryGetAsync<int>(key1, token: TestContext.Current.CancellationToken);
		await cache2.TryGetAsync<int>(key2, token: TestContext.Current.CancellationToken);

		// NOW CACHE2 HAS 2 ENTRIES AGAIN, CACHE1 ONLY 1
		Assert.Equal(1, memoryCache1.Count);
		Assert.Equal(2, memoryCache2.Count);
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

		await fusionCache.SetAsync<int>(keyFoo, 21, token: TestContext.Current.CancellationToken);
		var v1 = await fusionCache.TryGetAsync<int>(keyFoo, token: TestContext.Current.CancellationToken);

		Assert.True(v1.HasValue);
		Assert.Equal(21, v1.Value);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync(SerializerType serializerType)
	{
		var keyFoo = CreateRandomCacheKey("foo");

		var distributedCache = CreateDistributedCache();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
		var task = fusionCache.GetOrSetAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await Task.Delay(500, TestContext.Current.CancellationToken);
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

		await fusionCache.GetOrSetAsync<int>(keyBar, async _ => { return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		Assert.NotNull(distributedCache.GetString(preProcessedCacheKey));

		preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(keyFoo, options.CacheKeyPrefix);
		var task = fusionCache.GetOrSetAsync<int>(keyFoo, async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await Task.Delay(500, TestContext.Current.CancellationToken);
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
	public async Task MemoryCacheDurationIsRespectedAsync(SerializerType serializerType)
	{
		var duration = TimeSpan.FromSeconds(10);
		var memoryCacheDuration = TimeSpan.FromSeconds(1);

		var dcache = CreateDistributedCache();
		var chaosDistributedCache = new ChaosDistributedCache(dcache);
		var options = CreateFusionCacheOptions(Guid.NewGuid().ToString("N"));
		options.DefaultEntryOptions = new FusionCacheEntryOptions
		{
			Duration = duration,
			MemoryCacheDuration = memoryCacheDuration
		};

		using var cacheA = new FusionCache(options);
		cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		using var cacheB = new FusionCache(options);
		cacheB.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

		var fooA1 = await cacheA.GetOrSetAsync<int>(
			"foo",
			async _ => 1,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(1, fooA1);

		var fooB1 = await cacheB.GetOrSetAsync<int>(
			"foo",
			async _ => 2,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(1, fooB1);

		// CHANGE VALUE ON CACHE B
		await cacheB.SetAsync<int>(
			"foo",
			3,
			token: TestContext.Current.CancellationToken
		);

		// IMMEDIATELY AFTER (NO WAIT)

		// CACHE A IS STILL = 1
		var fooA2 = await cacheA.GetOrSetAsync<int>(
			"foo",
			async _ => 10,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(1, fooA2);

		// CACHE B IS STILL = 3
		var fooB2 = await cacheB.GetOrSetAsync<int>(
			"foo",
			async _ => 20,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(3, fooB2);

		// WAIT FOR MEMORY CACHE TO EXPIRE (1 SEC)
		await Task.Delay(memoryCacheDuration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// CACHE A IS NOW 3 (FROM L2)
		var fooA3 = await cacheA.GetOrSetAsync<int>(
			"foo",
			async _ => 20,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(3, fooA3);

		// CACHE B IS STILL 3 (TAKEN AGAIN FROM L2)
		var fooB3 = await cacheB.GetOrSetAsync<int>(
			"foo",
			async _ => 30,
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal(3, fooB3);
	}

	//[Theory]
	//[ClassData(typeof(SerializerTypesClassData))]
	//public async Task MemoryExpirationPreservedWhenL1L2DurationsAreDifferentAsync(SerializerType serializerType)
	//{
	//	var memoryDuration = TimeSpan.FromSeconds(2);
	//	var distributedDuration = TimeSpan.FromSeconds(10);

	//	var dc = CreateDistributedCache();
	//	using var fc1 = new FusionCache(CreateFusionCacheOptions());
	//	fc1.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));
	//	using var fc2 = new FusionCache(CreateFusionCacheOptions());
	//	fc2.SetupDistributedCache(dc, TestsUtils.GetSerializer(serializerType));

	//	await fc1.SetAsync<int>("foo", 21, opt => opt.SetDuration(memoryDuration).SetDistributedCacheDuration(distributedDuration));
	//	//await Task.Delay(memoryDuration.PlusALittleBit());
	//	var v1 = await fc2.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(memoryDuration).SetDistributedCacheDuration(distributedDuration));
	//	await dc.RemoveAsync("foo");
	//	await Task.Delay(memoryDuration.PlusALittleBit());
	//	var v2 = await fc2.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(memoryDuration).SetDistributedCacheDuration(distributedDuration));

	//	Assert.Equal(21, v1);
	//	Assert.Equal(42, v2);
	//}
}
