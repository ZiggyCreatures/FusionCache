using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.FusionCaching;
using ZiggyCreatures.FusionCaching.Chaos;
using ZiggyCreatures.FusionCaching.Serialization.NewtonsoftJson;

namespace FusionCaching.Tests
{
	public class MultiLevelTests
	{

		[Fact]
		public async Task ReturnsDataFromDistributedCacheIfNoDataInMemoryCacheAsync()
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, new FusionCacheNewtonsoftJsonSerializer()))
				{
					var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions().SetDurationSec(10));
					memoryCache.Remove("foo");
					var newValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(21), new FusionCacheEntryOptions().SetDurationSec(10));
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Fact]
		public void ReturnsDataFromDistributedCacheIfNoDataInMemoryCache()
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(distributedCache, new FusionCacheNewtonsoftJsonSerializer()))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;

					var initialValue = fusionCache.GetOrSet<int>("foo", _ => 42, options => options.SetDurationSec(10));
					memoryCache.Remove("foo");
					var newValue = fusionCache.GetOrSet<int>("foo", _ => 21, options => options.SetDurationSec(10));
					Assert.Equal(initialValue, newValue);
				}
			}
		}

		[Fact]
		public async Task HandlesDistributedCacheFailuresAsync()
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using (var fusionCache = new FusionCache(new FusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer()))
			{
				var initialValue = await fusionCache.GetOrSetAsync<int>("foo", _ => Task.FromResult(42), new FusionCacheEntryOptions() { Duration = TimeSpan.FromSeconds(1), IsFailSafeEnabled = true });
				await Task.Delay(1_500);
				chaosDistributedCache.SetAlwaysThrow();
				var newValue = await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Generic error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }); ;
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public async Task HandlesDistributedCacheRemovalInTheMiddleOfAnOperationAsync()
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
			using (var fusionCache = new FusionCache(new FusionCacheOptions()).SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer()))
			{
				var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				await Task.Delay(500);
				fusionCache.RemoveDistributedCache();
				var value = await task;
				Assert.Equal(42, value);
			}
		}

		[Fact]
		public async Task HandlesDistributedCacheFailuresInTheMiddleOfAnOperationAsync()
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				var chaosDistributedCache = new ChaosDistributedCache(distributedCache);
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache).SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer()))
				{
					var task = fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 42; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
					await Task.Delay(500);
					chaosDistributedCache.SetAlwaysThrow();
					var value = await task;
					chaosDistributedCache.SetNeverThrow();

					// END RESULT IS WHAT EXPECTED
					Assert.Equal(42, value);

					// MEMORY CACHE HAS BEEN UPDATED
					Assert.Equal(42, memoryCache.Get<FusionCacheEntry<int>>("foo").Value);

					// DISTRIBUTED CACHE HAS -NOT- BEEN UPDATED
					Assert.Null(distributedCache.GetString("foo"));
				}
			}
		}

		[Fact]
		public async Task AppliesDistributedCacheHardTimeoutAsync()
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());
					await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
					memoryCache.Remove("foo");
					await Assert.ThrowsAsync<Exception>(async () =>
					{
						var res = await fusionCache.GetOrSetAsync<int>("foo", async ct => throw new Exception("Banana"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					});
				}
			}
		}

		[Fact]
		public void AppliesDistributedCacheHardTimeout()
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());
					fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					Thread.Sleep(TimeSpan.FromSeconds(1));
					memoryCache.Remove("foo");
					Assert.Throws<Exception>(() =>
					{
						_ = fusionCache.GetOrSet<int>("foo", ct => throw new Exception("Banana"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					});
				}
			}
		}

		[Fact]
		public async Task AppliesDistributedCacheSoftTimeoutAsync()
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());
					await fusionCache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
					var sw = Stopwatch.StartNew();
					var res = await fusionCache.GetOrSetAsync<int>("foo", async ct => throw new Exception("Banana"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					sw.Stop();

					Assert.Equal(42, res);
					Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
					Assert.True(sw.ElapsedMilliseconds < simulatedDelayMs, "Distributed cache soft timeout not applied");
				}
			}
		}

		[Fact]
		public void AppliesDistributedCacheSoftTimeout()
		{
			var simulatedDelayMs = 5_000;
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			chaosDistributedCache.SetAlwaysDelayExactly(TimeSpan.FromMilliseconds(simulatedDelayMs));
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions(), memoryCache))
				{
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());
					fusionCache.Set<int>("foo", 42, new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true));
					Thread.Sleep(TimeSpan.FromSeconds(1));
					var sw = Stopwatch.StartNew();
					var res = fusionCache.GetOrSet<int>("foo", ct => throw new Exception("Banana"), new FusionCacheEntryOptions().SetDurationSec(1).SetFailSafe(true).SetDistributedCacheTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1_000)));
					sw.Stop();

					Assert.Equal(42, res);
					Assert.True(sw.ElapsedMilliseconds >= 100, "Distributed cache soft timeout not applied");
					Assert.True(sw.ElapsedMilliseconds < simulatedDelayMs, "Distributed cache soft timeout not applied");
				}
			}
		}

		[Fact]
		public async Task DistributedCacheCircuitBreakerActuallyWorksAsync()
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions() { DistributedCacheCircuitBreakerDuration = circuitBreakerDuration }, memoryCache))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());

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

		[Fact]
		public void DistributedCacheCircuitBreakerActuallyWorks()
		{
			var circuitBreakerDuration = TimeSpan.FromSeconds(2);
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			var chaosDistributedCache = new ChaosDistributedCache(distributedCache);

			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				using (var fusionCache = new FusionCache(new FusionCacheOptions() { DistributedCacheCircuitBreakerDuration = circuitBreakerDuration }, memoryCache))
				{
					fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
					fusionCache.SetupDistributedCache(chaosDistributedCache, new FusionCacheNewtonsoftJsonSerializer());

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

	}
}
