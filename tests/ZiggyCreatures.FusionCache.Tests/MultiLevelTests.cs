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
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests
{
	public class MultiLevelTests
	{
		private readonly ITestOutputHelper _output;
		private static readonly string? _cacheKeyPrefix = "Foo:";

		public MultiLevelTests(ITestOutputHelper output)
		{
			_output = output;
		}

		private XUnitLogger<T> CreateLogger<T>(LogLevel minLevel = LogLevel.Trace)
		{
			return new XUnitLogger<T>(minLevel, _output);
		}

		private static FusionCacheOptions CreateFusionCacheOptions()
		{
			var res = new FusionCacheOptions();

			res.CacheKeyPrefix = _cacheKeyPrefix;

			return res;
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task ReturnsDataFromDistributedCacheIfNoDataInMemoryCacheAsync(SerializerType serializerType)
		{
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions().SetDurationSec(10));
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var newValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(21), new FusionCacheEntryOptions().SetDurationSec(10));
			Assert.Equal(initialValue, newValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void ReturnsDataFromDistributedCacheIfNoDataInMemoryCache(SerializerType serializerType)
		{
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;

			var initialValue = fusionCache.GetOrSet<int>("foo", _ => 42, options => options.SetDurationSec(10));
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var newValue = fusionCache.GetOrSet<int>("foo", _ => 21, options => options.SetDurationSec(10));
			Assert.Equal(initialValue, newValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task HandlesDistributedCacheFailuresAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
			await Task.Delay(1_500);
			chaosDistributedCache.SetAlwaysThrow();
			var newValue = await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
			Assert.Equal(initialValue, newValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void HandlesDistributedCacheFailures(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			var initialValue = fusionCache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
			Thread.Sleep(1_500);
			chaosDistributedCache.SetAlwaysThrow();
			var newValue = fusionCache.GetOrSet<int>("foo", _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
			Assert.Equal(initialValue, newValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
			await Task.Delay(500);
			fusionCache.RemoveDistributedCache();
			var value = await task;
			Assert.Equal(42, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task HandlesDistributedCacheFailuresInTheMiddleOfAnOperationAsync(SerializerType serializerType)
		{
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			var options = CreateFusionCacheOptions();
			options.DistributedCacheKeyModifierMode = CacheKeyModifierMode.None;
			using var fusionCache = new FusionCache(options, memoryCache).SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			var preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey("bar", options.CacheKeyPrefix);

			await fusionCache.GetOrSetAsync<int>("bar", async _ => { return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
			Assert.NotNull(distributedCache.GetString(preProcessedCacheKey));

			preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey("foo", options.CacheKeyPrefix);
			var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
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
			var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
			var softTimeout = TimeSpan.FromMilliseconds(100);
			var hardTimeout = TimeSpan.FromMilliseconds(1_000);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
			await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
			await Assert.ThrowsAsync<Exception>(async () =>
			{
				_ = await fusionCache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void AppliesDistributedCacheHardTimeout(SerializerType serializerType)
		{
			var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
			var softTimeout = TimeSpan.FromMilliseconds(100);
			var hardTimeout = TimeSpan.FromMilliseconds(1_000);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
			Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
			Assert.Throws<Exception>(() =>
			{
				_ = fusionCache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task AppliesDistributedCacheSoftTimeoutAsync(SerializerType serializerType)
		{
			var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
			var softTimeout = TimeSpan.FromMilliseconds(100);
			var hardTimeout = TimeSpan.FromMilliseconds(1_000);
			var duration = TimeSpan.FromSeconds(1);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true));
			await Task.Delay(duration.PlusALittleBit()).ConfigureAwait(false);
			var sw = Stopwatch.StartNew();
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
			var res = await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
			sw.Stop();

			Assert.Equal(42, res);
			Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
			Assert.True(sw.Elapsed < simulatedDelay, "Distributed cache soft timeout not applied");
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void AppliesDistributedCacheSoftTimeout(SerializerType serializerType)
		{
			var simulatedDelay = TimeSpan.FromMilliseconds(2_000);
			var softTimeout = TimeSpan.FromMilliseconds(100);
			var hardTimeout = TimeSpan.FromMilliseconds(1_000);
			var duration = TimeSpan.FromSeconds(1);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDuration(duration).SetFailSafe(true));
			Thread.Sleep(duration.PlusALittleBit());
			var sw = Stopwatch.StartNew();
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelay);
			var res = fusionCache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(softTimeout, hardTimeout));
			sw.Stop();

			Assert.Equal(42, res);
			Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
			Assert.True(sw.Elapsed < simulatedDelay, "Distributed cache soft timeout not applied");
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task DistributedCacheCircuitBreakerActuallyWorksAsync(SerializerType serializerType)
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var options = CreateFusionCacheOptions();
			options.DistributedCacheCircuitBreakerDuration = circuitBreakerDuration;
			using var fusionCache = new FusionCache(options, memoryCache);
			fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			await fusionCache.SetAsync<int>("foo", 1, options => options.SetDurationSec(60).SetFailSafe(true));
			chaosDistributedCache.SetAlwaysThrow();
			await fusionCache.SetAsync<int>("foo", 2, options => options.SetDurationSec(60).SetFailSafe(true));
			chaosDistributedCache.SetNeverThrow();
			await fusionCache.SetAsync<int>("foo", 3, options => options.SetDurationSec(60).SetFailSafe(true));
			await Task.Delay(circuitBreakerDuration.PlusALittleBit()).ConfigureAwait(false);
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var res = await fusionCache.GetOrDefaultAsync<int>("foo", -1);

			Assert.Equal(1, res);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void DistributedCacheCircuitBreakerActuallyWorks(SerializerType serializerType)
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var options = CreateFusionCacheOptions();
			options.DistributedCacheCircuitBreakerDuration = circuitBreakerDuration;
			using var fusionCache = new FusionCache(options, memoryCache);
			fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			fusionCache.Set<int>("foo", 1, options => options.SetDurationSec(60).SetFailSafe(true));
			chaosDistributedCache.SetAlwaysThrow();
			fusionCache.Set<int>("foo", 2, options => options.SetDurationSec(60).SetFailSafe(true));
			chaosDistributedCache.SetNeverThrow();
			fusionCache.Set<int>("foo", 3, options => options.SetDurationSec(60).SetFailSafe(true));
			Thread.Sleep(circuitBreakerDuration.PlusALittleBit());
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var res = fusionCache.GetOrDefault<int>("foo", -1);

			Assert.Equal(1, res);
		}

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public async Task HandlesFlexibleSimpleTypeConversionsAsync(SerializerType serializerType)
		//{
		//	using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
		//	{
		//		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		//		using (var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType)))
		//		{
		//			int? initialValue = 42;
		//			await fusionCache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
		//			memoryCache.Remove("foo");
		//			var newValue = await fusionCache.GetOrDefaultAsync<int>("foo");
		//			Assert.Equal(initialValue, newValue);
		//		}
		//	}
		//}

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public void HandlesFlexibleSimpleTypeConversions(SerializerType serializerType)
		//{
		//	using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
		//	{
		//		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		//		using (var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType)))
		//		{
		//			int? initialValue = 42;
		//			fusionCache.Set("foo", initialValue, TimeSpan.FromHours(24));
		//			memoryCache.Remove("foo");
		//			var newValue = fusionCache.GetOrDefault<int>("foo");
		//			Assert.Equal(initialValue, newValue);
		//		}
		//	}
		//}

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public async Task HandlesFlexibleComplexTypeConversionsAsync(SerializerType serializerType)
		//{
		//	using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
		//	{
		//		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		//		using (var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType)))
		//		{
		//			var initialValue = (object)ComplexType.CreateSample();
		//			await fusionCache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
		//			memoryCache.Remove("foo");
		//			var newValue = await fusionCache.GetOrDefaultAsync<ComplexType>("foo");
		//			Assert.Equal(initialValue, newValue);
		//		}
		//	}
		//}

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public void HandlesFlexibleComplexTypeConversions(SerializerType serializerType)
		//{
		//	using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
		//	{
		//		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		//		using (var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType)))
		//		{
		//			var initialValue = (object)ComplexType.CreateSample();
		//			fusionCache.Set("foo", initialValue, TimeSpan.FromHours(24));
		//			memoryCache.Remove("foo");
		//			var newValue = fusionCache.GetOrDefault<ComplexType>("foo");
		//			Assert.Equal(initialValue, newValue);
		//		}
		//	}
		//}

		private static void _DistributedCacheWireVersionModifierWorks(SerializerType serializerType, CacheKeyModifierMode modifierMode)
		{
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var options = CreateFusionCacheOptions();
			options.DistributedCacheKeyModifierMode = modifierMode;
			using var fusionCache = new FusionCache(options, memoryCache).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			var cacheKey = "foo";
			var preProcessedCacheKey = TestsUtils.MaybePreProcessCacheKey(cacheKey, options.CacheKeyPrefix);
			string distributedCacheKey;
			switch (modifierMode)
			{
				case CacheKeyModifierMode.Prefix:
					distributedCacheKey = $"{FusionCacheOptions.DistributedCacheWireFormatVersion}{FusionCacheOptions.DistributedCacheWireFormatSeparator}{preProcessedCacheKey}";
					break;
				case CacheKeyModifierMode.Suffix:
					distributedCacheKey = $"{preProcessedCacheKey}{FusionCacheOptions.DistributedCacheWireFormatSeparator}{FusionCacheOptions.DistributedCacheWireFormatVersion}";
					break;
				default:
					distributedCacheKey = preProcessedCacheKey;
					break;
			}
			var value = "sloths";
			fusionCache.Set(cacheKey, value, new FusionCacheEntryOptions(TimeSpan.FromHours(24)) { AllowBackgroundDistributedCacheOperations = false });
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
		public async Task ReThrowsDistributedCacheErrorsAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysThrow();
			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			await Assert.ThrowsAsync<ChaosException>(async () =>
			{
				await fusionCache.SetAsync<int>("foo", 42);
			});

			await Assert.ThrowsAsync<ChaosException>(async () =>
			{
				_ = await fusionCache.TryGetAsync<int>("bar");
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void ReThrowsDistributedCacheErrors(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysThrow();
			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			Assert.Throws<ChaosException>(() =>
			{
				fusionCache.Set<int>("foo", 42);
			});

			Assert.Throws<ChaosException>(() =>
			{
				_ = fusionCache.TryGet<int>("bar");
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task ReThrowsSerializationErrorsAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));

			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowSerializationExceptions = true;

			fusionCache.SetupDistributedCache(distributedCache, serializer);

			serializer.SetAlwaysThrow();
			await Assert.ThrowsAsync<ChaosException>(async () =>
			{
				await fusionCache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));
			});

			serializer.SetNeverThrow();
			await fusionCache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			Thread.Sleep(TimeSpan.FromSeconds(1));

			serializer.SetAlwaysThrow();
			await Assert.ThrowsAsync<ChaosException>(async () =>
			{
				_ = await fusionCache.TryGetAsync<int>("foo");
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void ReThrowsSerializationErrors(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));

			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowSerializationExceptions = true;

			fusionCache.SetupDistributedCache(distributedCache, serializer);

			serializer.SetAlwaysThrow();
			Assert.Throws<ChaosException>(() =>
			{
				fusionCache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));
			});

			serializer.SetNeverThrow();
			fusionCache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			Thread.Sleep(TimeSpan.FromSeconds(1));

			serializer.SetAlwaysThrow();
			Assert.Throws<ChaosException>(() =>
			{
				_ = fusionCache.TryGet<int>("foo");
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task DoesNotReThrowsSerializationErrorsAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));

			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowSerializationExceptions = false;

			fusionCache.SetupDistributedCache(distributedCache, serializer);

			serializer.SetAlwaysThrow();
			await fusionCache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			serializer.SetNeverThrow();
			await fusionCache.SetAsync<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			Thread.Sleep(TimeSpan.FromSeconds(1));

			serializer.SetAlwaysThrow();
			var res = await fusionCache.TryGetAsync<int>("foo");

			Assert.False(res.HasValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void DoesNotReThrowsSerializationErrors(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var serializer = new ChaosSerializer(TestsUtils.GetSerializer(serializerType));

			using var fusionCache = new FusionCache(CreateFusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowSerializationExceptions = false;

			fusionCache.SetupDistributedCache(distributedCache, serializer);

			serializer.SetAlwaysThrow();
			fusionCache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			serializer.SetNeverThrow();
			fusionCache.Set<string>("foo", "sloths, sloths everywhere", x => x.SetDuration(TimeSpan.FromMilliseconds(100)).SetDistributedCacheDuration(TimeSpan.FromSeconds(10)));

			Thread.Sleep(TimeSpan.FromSeconds(1));

			serializer.SetAlwaysThrow();
			var res = fusionCache.TryGet<int>("foo");

			Assert.False(res.HasValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task SpecificDistributedCacheDurationWorksAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
			await Task.Delay(TimeSpan.FromSeconds(2));
			var value = await fusionCache.GetOrDefaultAsync<int>("foo");
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void SpecificDistributedCacheDurationWorks(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.Set<int>("foo", 21, opt => opt.SetFailSafe(false).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
			Thread.Sleep(TimeSpan.FromSeconds(2));
			var value = fusionCache.GetOrDefault<int>("foo");
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task SpecificDistributedCacheDurationWithFailSafeWorksAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
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
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.Set<int>("foo", 21, opt => opt.SetFailSafe(true).SetDuration(TimeSpan.FromSeconds(1)).SetDistributedCacheDuration(TimeSpan.FromMinutes(1)));
			Thread.Sleep(TimeSpan.FromSeconds(2));
			var value = fusionCache.GetOrDefault<int>("foo");
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task DistributedCacheFailSafeMaxDurationWorksAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)));
			await Task.Delay(TimeSpan.FromSeconds(2));
			var value = await fusionCache.GetOrDefaultAsync<int>("foo", opt => opt.SetFailSafe(true));
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void DistributedCacheFailSafeMaxDurationWorks(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.Set<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(2)).SetDistributedCacheFailSafeOptions(TimeSpan.FromMinutes(10)));
			Thread.Sleep(TimeSpan.FromSeconds(2));
			var value = fusionCache.GetOrDefault<int>("foo", opt => opt.SetFailSafe(true));
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task DistributedCacheFailSafeMaxDurationNormalizationOccursAsync(SerializerType serializerType)
		{
			var duration = TimeSpan.FromSeconds(5);
			var maxDuration = TimeSpan.FromSeconds(1);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration));
			await Task.Delay(maxDuration.PlusALittleBit());
			var value = await fusionCache.GetOrDefaultAsync<int>("foo", opt => opt.SetFailSafe(true));
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void DistributedCacheFailSafeMaxDurationNormalizationOccurs(SerializerType serializerType)
		{
			var duration = TimeSpan.FromSeconds(5);
			var maxDuration = TimeSpan.FromSeconds(1);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			fusionCache.Set<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration).SetDistributedCacheFailSafeOptions(maxDuration));
			Thread.Sleep(maxDuration.PlusALittleBit());
			var value = fusionCache.GetOrDefault<int>("foo", opt => opt.SetFailSafe(true));
			Assert.Equal(21, value);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task MemoryExpirationAlignedWithDistributedAsync(SerializerType serializerType)
		{
			var firstDuration = TimeSpan.FromSeconds(4);
			var secondDuration = TimeSpan.FromSeconds(10);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions())
				.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
			;
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions())
				.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
			;

			await fusionCache1.SetAsync<int>("foo", 21, opt => opt.SetDuration(firstDuration));
			await Task.Delay(firstDuration / 2);
			var v1 = await fusionCache2.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(secondDuration));
			await Task.Delay(firstDuration + TimeSpan.FromSeconds(1));
			var v2 = await fusionCache2.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(secondDuration));

			Assert.Equal(21, v1);
			Assert.Equal(42, v2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void MemoryExpirationAlignedWithDistributed(SerializerType serializerType)
		{
			var firstDuration = TimeSpan.FromSeconds(4);
			var secondDuration = TimeSpan.FromSeconds(10);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions())
				.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
			;
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions())
				.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType))
			;

			fusionCache1.Set<int>("foo", 21, opt => opt.SetDuration(firstDuration));
			Thread.Sleep(firstDuration / 2);
			var v1 = fusionCache2.GetOrDefault<int>("foo", 42, opt => opt.SetDuration(secondDuration));
			Thread.Sleep(firstDuration + TimeSpan.FromSeconds(1));
			var v2 = fusionCache2.GetOrDefault<int>("foo", 42, opt => opt.SetDuration(secondDuration));

			Assert.Equal(21, v1);
			Assert.Equal(42, v2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanSkipDistributedCacheAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			var v1 = await fusionCache1.GetOrSetAsync<int>("foo", 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true));
			var v2 = await fusionCache2.GetOrSetAsync<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

			Assert.Equal(1, v1);
			Assert.Equal(2, v2);

			var v3 = await fusionCache1.GetOrSetAsync<int>("bar", 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			var v4 = await fusionCache2.GetOrSetAsync<int>("bar", 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true));

			Assert.Equal(3, v3);
			Assert.Equal(4, v4);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanSkipDistributedCache(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			var v1 = fusionCache1.GetOrSet<int>("foo", 1, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true).SetSkipDistributedCache(true, true));
			var v2 = fusionCache2.GetOrSet<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

			Assert.Equal(1, v1);
			Assert.Equal(2, v2);

			var v3 = fusionCache1.GetOrSet<int>("bar", 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			var v4 = fusionCache2.GetOrSet<int>("bar", 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCache(true, true));

			Assert.Equal(3, v3);
			Assert.Equal(4, v4);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanSkipDistributedReadWhenStaleAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			var v1 = await fusionCache1.GetOrSetAsync<int>("foo", 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			var v2 = await fusionCache2.GetOrSetAsync<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));

			Assert.Equal(1, v1);
			Assert.Equal(1, v2);

			await Task.Delay(TimeSpan.FromSeconds(2).PlusALittleBit());

			v1 = await fusionCache1.GetOrSetAsync<int>("foo", 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			v2 = await fusionCache2.GetOrSetAsync<int>("foo", 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

			Assert.Equal(3, v1);
			Assert.Equal(4, v2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanSkipDistributedReadWhenStale(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var fusionCache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var fusionCache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			var v1 = fusionCache1.GetOrSet<int>("foo", 1, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			var v2 = fusionCache2.GetOrSet<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));

			Assert.Equal(1, v1);
			Assert.Equal(1, v2);

			Thread.Sleep(TimeSpan.FromSeconds(2).PlusALittleBit());

			v1 = fusionCache1.GetOrSet<int>("foo", 3, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true));
			v2 = fusionCache2.GetOrSet<int>("foo", 4, opt => opt.SetDuration(TimeSpan.FromSeconds(2)).SetFailSafe(true).SetSkipDistributedCacheReadWhenStale(true));

			Assert.Equal(3, v1);
			Assert.Equal(4, v2);
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

			var duration = TimeSpan.FromSeconds(1);
			var endpoint = new FakeHttpEndpoint(1);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			// TOT REQ + 1 / FULL RESP + 1
			var v1 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// CACHED -> NO INCR
			var v2 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// LET THE CACHE EXPIRE
			await Task.Delay(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
			var v3 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// LET THE CACHE EXPIRE
			await Task.Delay(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
			var v4 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// SET VALUE -> CHANGE LAST MODIFIED
			endpoint.SetValue(42);

			// LET THE CACHE EXPIRE
			await Task.Delay(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
			var v5 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

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

			var duration = TimeSpan.FromSeconds(1);
			var endpoint = new FakeHttpEndpoint(1);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			// TOT REQ + 1 / FULL RESP + 1
			var v1 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// CACHED -> NO INCR
			var v2 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// LET THE CACHE EXPIRE
			Thread.Sleep(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
			var v3 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// LET THE CACHE EXPIRE
			Thread.Sleep(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
			var v4 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

			// SET VALUE -> CHANGE LAST MODIFIED
			endpoint.SetValue(42);

			// LET THE CACHE EXPIRE
			Thread.Sleep(duration.PlusALittleBit());

			// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
			var v5 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

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
			var duration = TimeSpan.FromSeconds(2);
			var eagerRefreshThreshold = 0.2f;

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			cache.DefaultEntryOptions.Duration = duration;
			cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

			// EXECUTE FACTORY
			var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

			// USE CACHED VALUE
			var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
			var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
			await Task.Delay(eagerDuration);

			// EAGER REFRESH KICKS IN
			var v3 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
			await Task.Delay(TimeSpan.FromMilliseconds(50));

			// GET THE REFRESHED VALUE
			var v4 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR EXPIRATION
			await Task.Delay(duration.PlusALittleBit());

			// EXECUTE FACTORY AGAIN
			var v5 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

			// USE CACHED VALUE
			var v6 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

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
			var duration = TimeSpan.FromSeconds(2);
			var eagerRefreshThreshold = 0.2f;

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			cache.DefaultEntryOptions.Duration = duration;
			cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

			// EXECUTE FACTORY
			var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			// USE CACHED VALUE
			var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
			var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
			Thread.Sleep(eagerDuration);

			// EAGER REFRESH KICKS IN
			var v3 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
			Thread.Sleep(TimeSpan.FromMilliseconds(50));

			// GET THE REFRESHED VALUE
			var v4 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			// WAIT FOR EXPIRATION
			Thread.Sleep(duration.PlusALittleBit());

			// EXECUTE FACTORY AGAIN
			var v5 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			// USE CACHED VALUE
			var v6 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

			Assert.Equal(v1, v2);
			Assert.Equal(v2, v3);
			Assert.True(v4 > v3);
			Assert.True(v5 > v4);
			Assert.Equal(v5, v6);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanSkipMemoryCacheAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var cache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			cache1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cache2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

			// SET ON CACHE 1 AND ON DISTRIBUTED CACHE
			var v1 = await cache1.GetOrSetAsync<int>("foo", async _ => 10);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
			var v2 = await cache2.GetOrSetAsync<int>("foo", async _ => 20);

			// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
			await cache1.SetAsync<int>("foo", 30, opt => opt.SetSkipMemoryCache());

			// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
			var v3 = await cache1.GetOrSetAsync<int>("foo", async _ => 40);

			// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
			var v4 = await cache2.GetOrSetAsync<int>("foo", async _ => 50);

			// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
			var v5 = await cache2.GetOrSetAsync<int>("foo", async _ => 60, opt => opt.SetSkipMemoryCache());

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
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache1 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			using var cache2 = new FusionCache(CreateFusionCacheOptions()).SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

			cache1.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cache2.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

			// SET ON CACHE 1 AND ON DISTRIBUTED CACHE
			var v1 = cache1.GetOrSet<int>("foo", _ => 10);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE 2
			var v2 = cache2.GetOrSet<int>("foo", _ => 20);

			// SET ON DISTRIBUTED CACHE BUT SKIP CACHE 1
			cache1.Set<int>("foo", 30, opt => opt.SetSkipMemoryCache());

			// GET FROM CACHE 1 (10) AND DON'T CALL THE FACTORY
			var v3 = cache1.GetOrSet<int>("foo", _ => 40);

			// GET FROM CACHE 2 (10) AND DON'T CALL THE FACTORY
			var v4 = cache2.GetOrSet<int>("foo", _ => 50);

			// SKIP CACHE 2, GET FROM DISTRIBUTED CACHE (30)
			var v5 = cache2.GetOrSet<int>("foo", _ => 60, opt => opt.SetSkipMemoryCache());

			Assert.Equal(10, v1);
			Assert.Equal(10, v2);
			Assert.Equal(10, v3);
			Assert.Equal(10, v4);
			Assert.Equal(30, v5);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanHandleIssuesWithBothDistributedCacheAndBackplaneAsync(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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
			await Task.Delay(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA3 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB3 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70);

			Assert.Equal(60, vA3);
			Assert.Equal(60, vB3);

			// GET FROM CACHE A (MEMORY CACHE)
			var vA4 = await cacheA.GetOrSetAsync<int>("foo", async _ => 120);

			// GET FROM CACHE A (MEMORY CACHE)
			var vB4 = await cacheB.GetOrSetAsync<int>("foo", async _ => 130);

			Assert.Equal(60, vA4);
			Assert.Equal(60, vB4);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanHandleIssuesWithBothDistributedCacheAndBackplane(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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

			// GIVE IT SOME TIME
			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// RE-ENABLE DISTRIBUTED CACHE AND BACKPLANE (SEND AUTO-RECOVERY NOTIFICATIONS)
			chaosDistributedCache.SetNeverThrow();
			chaosBackplaneA.SetNeverThrow();
			chaosBackplaneB.SetNeverThrow();

			// GIVE IT SOME TIME
			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA3 = cacheA.GetOrSet<int>("foo", _ => 60);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB3 = cacheB.GetOrSet<int>("foo", _ => 70);

			Assert.Equal(60, vA3);
			Assert.Equal(60, vB3);

			// GET FROM CACHE A (MEMORY CACHE)
			var vA4 = cacheA.GetOrSet<int>("foo", _ => 120);

			// GET FROM CACHE A (MEMORY CACHE)
			var vB4 = cacheB.GetOrSet<int>("foo", _ => 130);

			Assert.Equal(60, vA4);
			Assert.Equal(60, vB4);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanHandleReconnectedBackplaneWithoutReconnectedDistributedCacheAsync(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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
			await Task.Delay(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA3 = await cacheA.GetOrDefaultAsync<int>("foo");

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB3 = await cacheB.GetOrDefaultAsync<int>("foo");

			Assert.Equal(10, vA3);
			Assert.Equal(30, vB3);

			// RE-ENABLE DISTRIBUTED CACHE
			chaosDistributedCache.SetNeverThrow();

			// GIVE IT SOME TIME TO RETRY AUTOMATICALLY
			await Task.Delay(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA4 = await cacheA.GetOrSetAsync<int>("foo", async _ => 60);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB4 = await cacheB.GetOrSetAsync<int>("foo", async _ => 70);

			Assert.Equal(60, vA4);
			Assert.Equal(60, vB4);

			// GET FROM CACHE A (MEMORY CACHE)
			var vA5 = await cacheA.GetOrSetAsync<int>("foo", async _ => 120);

			// GET FROM CACHE A (MEMORY CACHE)
			var vB5 = await cacheB.GetOrSetAsync<int>("foo", async _ => 130);

			Assert.Equal(60, vA5);
			Assert.Equal(60, vB5);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanHandleReconnectedBackplaneWithoutReconnectedDistributedCache(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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
			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA3 = cacheA.GetOrDefault<int>("foo");

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB3 = cacheB.GetOrDefault<int>("foo");

			Assert.Equal(10, vA3);
			Assert.Equal(30, vB3);

			// RE-ENABLE DISTRIBUTED CACHE
			chaosDistributedCache.SetNeverThrow();

			// GIVE IT SOME TIME TO RETRY AUTOMATICALLY
			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// SET ON CACHE A AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vA4 = cacheA.GetOrSet<int>("foo", _ => 60);

			// GET FROM DISTRIBUTED CACHE AND SET IT ON CACHE B
			var vB4 = cacheB.GetOrSet<int>("foo", _ => 70);

			Assert.Equal(60, vA4);
			Assert.Equal(60, vB4);

			// GET FROM CACHE A (MEMORY CACHE)
			var vA5 = cacheA.GetOrSet<int>("foo", _ => 120);

			// GET FROM CACHE A (MEMORY CACHE)
			var vB5 = cacheB.GetOrSet<int>("foo", _ => 130);

			Assert.Equal(60, vA5);
			Assert.Equal(60, vB5);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanHandleDistributedCacheErrorsWithBackplaneRetryAsync(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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

			// SET ON CACHE B
			var vB1 = await cacheB.GetOrSetAsync<int>("foo", async _ => 40);

			Assert.Equal(10, vA1);
			Assert.Equal(30, vB1);

			// RE-ENABLE DISTRIBUTED CACHE
			chaosDistributedCache.SetNeverThrow();

			// GIVE IT SOME TIME
			await Task.Delay(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// GET FROM CACHE A (MEMORY CACHE)
			var vA2 = await cacheA.GetOrSetAsync<int>("foo", async _ => 50);

			// SET ON CACHE B AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vB2 = await cacheB.GetOrSetAsync<int>("foo", async _ => 60);

			Assert.Equal(50, vA2);
			Assert.Equal(50, vB2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanHandleDistributedCacheErrorsWithBackplaneRetry(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, logger: CreateLogger<ChaosDistributedCache>());

			// SETUP CACHE A
			var backplaneA = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneA = new ChaosBackplane(backplaneA, logger: CreateLogger<ChaosBackplane>());
			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

			cacheA.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			cacheA.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(chaosBackplaneA);

			// SETUP CACHE B
			var backplaneB = new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId });
			var chaosBackplaneB = new ChaosBackplane(backplaneB, logger: CreateLogger<ChaosBackplane>());
			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());

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

			// SET ON CACHE B
			var vB1 = cacheB.GetOrSet<int>("foo", _ => 40);

			Assert.Equal(10, vA1);
			Assert.Equal(30, vB1);

			// RE-ENABLE DISTRIBUTED CACHE
			chaosDistributedCache.SetNeverThrow();

			// GIVE IT SOME TIME
			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryDelay.PlusALittleBit());

			// GET FROM CACHE A (MEMORY CACHE)
			var vA2 = cacheA.GetOrSet<int>("foo", _ => 50);

			// SET ON CACHE B AND ON DISTRIBUTED CACHE + NOTIFY ON BACKPLANE
			var vB2 = cacheB.GetOrSet<int>("foo", _ => 60);

			Assert.Equal(50, vA2);
			Assert.Equal(50, vB2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanExecuteBackgroundDistributedCacheOperationsAsync(SerializerType serializerType)
		{
			var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
			var eo = new FusionCacheEntryOptions().SetDurationSec(10);
			eo.AllowBackgroundDistributedCacheOperations = true;

			var logger = CreateLogger<FusionCache>();
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateLogger<ChaosDistributedCache>());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache, logger);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			await fusionCache.SetAsync<int>("foo", 21, eo);
			await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
			var sw = Stopwatch.StartNew();
			// SHOULD RETURN IMMEDIATELY
			await fusionCache.SetAsync<int>("foo", 42, eo);
			sw.Stop();
			logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
			await Task.Delay(TimeSpan.FromMilliseconds(200));
			chaosDistributedCache.SetNeverDelay();
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var foo1 = await fusionCache.GetOrDefaultAsync<int>("foo", -1, eo);
			await Task.Delay(simulatedDelayMs.PlusALittleBit());
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var foo2 = await fusionCache.GetOrDefaultAsync<int>("foo", -1, eo);

			Assert.True(sw.Elapsed < simulatedDelayMs);
			Assert.Equal(21, foo1);
			Assert.Equal(42, foo2);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanExecuteBackgroundDistributedCacheOperations(SerializerType serializerType)
		{
			var simulatedDelayMs = TimeSpan.FromMilliseconds(2_000);
			var eo = new FusionCacheEntryOptions().SetDurationSec(10);
			eo.AllowBackgroundDistributedCacheOperations = true;

			var logger = CreateLogger<FusionCache>();
			using var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache, CreateLogger<ChaosDistributedCache>());
			using var fusionCache = new FusionCache(CreateFusionCacheOptions(), memoryCache, logger);
			fusionCache.SetupDistributedCache(chaosDistributedCache, TestsUtils.GetSerializer(serializerType));

			fusionCache.Set<int>("foo", 21, eo);
			Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());
			chaosDistributedCache.SetAlwaysDelayExactly(simulatedDelayMs);
			var sw = Stopwatch.StartNew();
			// SHOULD RETURN IMMEDIATELY
			fusionCache.Set<int>("foo", 42, eo);
			sw.Stop();
			logger.Log(LogLevel.Information, "ELAPSED: {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
			Thread.Sleep(TimeSpan.FromMilliseconds(200));
			chaosDistributedCache.SetNeverDelay();
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var foo1 = fusionCache.GetOrDefault<int>("foo", -1, eo);
			Thread.Sleep(simulatedDelayMs.PlusALittleBit());
			memoryCache.Remove(TestsUtils.MaybePreProcessCacheKey("foo", _cacheKeyPrefix));
			var foo2 = fusionCache.GetOrDefault<int>("foo", -1, eo);

			Assert.True(sw.Elapsed < simulatedDelayMs);
			Assert.Equal(21, foo1);
			Assert.Equal(42, foo2);
		}
	}
}
