using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests
{
	public class MultiLevelTests
	{
		public enum SerializerType
		{
			NewtonsoftJson = 0,
			SystemTextJson = 1
		}

		private IFusionCacheSerializer GetSerializer(SerializerType serializerType)
		{
			switch (serializerType)
			{
				case SerializerType.NewtonsoftJson:
					return new FusionCacheNewtonsoftJsonSerializer();
				case SerializerType.SystemTextJson:
					return new FusionCacheSystemTextJsonSerializer();
			}

			throw new ArgumentException("Invalid serializer specified", nameof(serializerType));
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task ReturnsDataFromDistributedCacheIfNoDataInMemoryCacheAsync(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions().SetDurationSec(10));
					memoryCache.Remove("foo");
					var newValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(21), new FusionCacheEntryOptions().SetDurationSec(10));
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void ReturnsDataFromDistributedCacheIfNoDataInMemoryCache(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;

					var initialValue = fusionCache.GetOrSet<int>("foo", _ => 42, options => options.SetDurationSec(10));
					memoryCache.Remove("foo");
					var newValue = fusionCache.GetOrSet<int>("foo", _ => 21, options => options.SetDurationSec(10));
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task HandlesDistributedCacheFailuresAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using (var fusionCache = new FusionCache(new FusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType)))
			{
				var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
				await Task.Delay(1_500);
				chaosDistributedCache.SetAlwaysThrow();
				var newValue = await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }); ;
				Assert.Equal(initialValue, newValue);
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using (var fusionCache = new FusionCache(new FusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType)))
			{
				var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				await Task.Delay(500);
				fusionCache.RemoveDistributedCache();
				var value = await task;
				Assert.Equal(42, value);
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task HandlesDistributedCacheFailuresInTheMiddleOfAnOperationAsync(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType)))
				{
					var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
					await Task.Delay(500);
					chaosDistributedCache.SetAlwaysThrow();
					var value = await task;
					chaosDistributedCache.SetNeverThrow();

					// END RESULT IS WHAT EXPECTED
					Assert.Equal(42, value);

					// MEMORY CACHE HAS BEEN UPDATED
					Assert.Equal(42, memoryCache.Get<IFusionCacheEntry>("foo").GetValue<int>());

					// DISTRIBUTED CACHE HAS -NOT- BEEN UPDATED
					Assert.Null(distributedCache.GetString("foo"));
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task AppliesDistributedCacheHardTimeoutAsync(SerializerType serializerType)
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));
					await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
					memoryCache.Remove("foo");
					await Assert.ThrowsAsync<Exception>(async () =>
					{
						var res = await fusionCache.GetOrSetAsync<int>("foo", async ct => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					});
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void AppliesDistributedCacheHardTimeout(SerializerType serializerType)
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));
					fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					Thread.Sleep(TimeSpan.FromSeconds(1));
					memoryCache.Remove("foo");
					Assert.Throws<Exception>(() =>
					{
						_ = fusionCache.GetOrSet<int>("foo", ct => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					});
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task AppliesDistributedCacheSoftTimeoutAsync(SerializerType serializerType)
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));
					await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
					var sw = Stopwatch.StartNew();
					var res = await fusionCache.GetOrSetAsync<int>("foo", async ct => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					sw.Stop();

					Assert.Equal(42, res);
					Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
					Assert.True(sw.ElapsedMilliseconds < simulatedDelayMs, "Distributed cache soft timeout not applied");
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void AppliesDistributedCacheSoftTimeout(SerializerType serializerType)
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));
					fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					Thread.Sleep(TimeSpan.FromSeconds(1));
					var sw = Stopwatch.StartNew();
					var res = fusionCache.GetOrSet<int>("foo", ct => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					sw.Stop();

					Assert.Equal(42, res);
					Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
					Assert.True(sw.ElapsedMilliseconds < simulatedDelayMs, "Distributed cache soft timeout not applied");
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task DistributedCacheCircuitBreakerActuallyWorksAsync(SerializerType serializerType)
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions() { DistributedCacheCircuitBreakerDuration = circuitBreakerDuration }, memoryCache))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));

					await fusionCache.SetAsync<int>("foo", 1, options => options.SetDurationSec(60).SetFailSafe(true));
					chaosDistributedCache.SetAlwaysThrow();
					await fusionCache.SetAsync<int>("foo", 2, options => options.SetDurationSec(60).SetFailSafe(true));
					chaosDistributedCache.SetNeverThrow();
					await fusionCache.SetAsync<int>("foo", 3, options => options.SetDurationSec(60).SetFailSafe(true));
					await Task.Delay(circuitBreakerDuration).ConfigureAwait(false);
					memoryCache.Remove("foo");
					var res = await fusionCache.GetOrDefaultAsync<int>("foo", -1);

					Assert.Equal(1, res);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void DistributedCacheCircuitBreakerActuallyWorks(SerializerType serializerType)
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions() { DistributedCacheCircuitBreakerDuration = circuitBreakerDuration }, memoryCache))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
					fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));

					fusionCache.Set<int>("foo", 1, options => options.SetDurationSec(60).SetFailSafe(true));
					chaosDistributedCache.SetAlwaysThrow();
					fusionCache.Set<int>("foo", 2, options => options.SetDurationSec(60).SetFailSafe(true));
					chaosDistributedCache.SetNeverThrow();
					fusionCache.Set<int>("foo", 3, options => options.SetDurationSec(60).SetFailSafe(true));
					Thread.Sleep(circuitBreakerDuration);
					memoryCache.Remove("foo");
					var res = fusionCache.GetOrDefault<int>("foo", -1);

					Assert.Equal(1, res);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task HandlesFlexibleSimpleTypeConversionsAsync(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var initialValue = (object)42;
					await fusionCache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
					memoryCache.Remove("foo");
					var newValue = await fusionCache.GetOrDefaultAsync<int>("foo");
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void HandlesFlexibleSimpleTypeConversions(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var initialValue = (object)42;
					fusionCache.Set("foo", initialValue, TimeSpan.FromHours(24));
					memoryCache.Remove("foo");
					var newValue = fusionCache.GetOrDefault<int>("foo");
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task HandlesFlexibleComplexTypeConversionsAsync(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var initialValue = (object)SampleComplexObject.CreateRandom();
					await fusionCache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
					memoryCache.Remove("foo");
					var newValue = await fusionCache.GetOrDefaultAsync<SampleComplexObject>("foo");
					Assert.NotNull(newValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void HandlesFlexibleComplexTypeConversions(SerializerType serializerType)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var initialValue = (object)SampleComplexObject.CreateRandom();
					fusionCache.Set("foo", initialValue, TimeSpan.FromHours(24));
					memoryCache.Remove("foo");
					var newValue = fusionCache.GetOrDefault<SampleComplexObject>("foo");
					Assert.NotNull(newValue);
				}
			}
		}

		private void _DistributedCacheWireVersionModifierWorks(SerializerType serializerType, CacheKeyModifierMode modifierMode)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions() { DistributedCacheKeyModifierMode = modifierMode }, memoryCache).SetupDistributedCache(distributedCache, GetSerializer(serializerType)))
				{
					var cacheKey = "foo";
					string distributedCacheKey;
					switch (modifierMode)
					{
						case CacheKeyModifierMode.Prefix:
							distributedCacheKey = $"v1:{cacheKey}";
							break;
						case CacheKeyModifierMode.Suffix:
							distributedCacheKey = $"{cacheKey}:v1";
							break;
						default:
							distributedCacheKey = cacheKey;
							break;
					}
					var value = "sloths";
					fusionCache.Set(cacheKey, value, new FusionCacheEntryOptions(TimeSpan.FromHours(24)) { AllowBackgroundDistributedCacheOperations = false });
					var nullValue = distributedCache.Get("foo42");
					var distributedValue = distributedCache.Get(distributedCacheKey);
					Assert.Null(nullValue);
					Assert.NotNull(distributedValue);
				}
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void DistributedCacheWireVersionPrefixModeWorks(SerializerType serializerType)
		{
			_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.Prefix);
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void DistributedCacheWireVersionSuffixModeWorks(SerializerType serializerType)
		{
			_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.Suffix);
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void DistributedCacheWireVersionNoneModeWorks(SerializerType serializerType)
		{
			_DistributedCacheWireVersionModifierWorks(serializerType, CacheKeyModifierMode.None);
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task ReThrowsDistributedCacheErrorsAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysThrow();
			using var fusionCache = new FusionCache(new FusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

			fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));

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
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void ReThrowsDistributedCacheErrors(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysThrow();
			using var fusionCache = new FusionCache(new FusionCacheOptions());
			fusionCache.DefaultEntryOptions.ReThrowDistributedCacheExceptions = true;

			fusionCache.SetupDistributedCache(chaosDistributedCache, GetSerializer(serializerType));

			Assert.Throws<ChaosException>(() =>
			{
				fusionCache.Set<int>("foo", 42);
			});

			Assert.Throws<ChaosException>(() =>
			{
				_ = fusionCache.TryGet<int>("bar");
			});
		}
	}
}
