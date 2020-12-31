using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.FusionCaching;

namespace FusionCaching.Tests
{

	public static class ExtMethods
	{
		public static FusionCacheEntryOptions SetFactoryTimeoutsMs(this FusionCacheEntryOptions options, int? softTimeoutMs = null, int? hardTimeoutMs = null, bool? keepTimedOutFactoryResult = null)
		{
			if (softTimeoutMs is object)
				options.FactorySoftTimeout = TimeSpan.FromMilliseconds(softTimeoutMs.Value);
			if (hardTimeoutMs is object)
				options.FactoryHardTimeout = TimeSpan.FromMilliseconds(hardTimeoutMs.Value);
			if (keepTimedOutFactoryResult is object)
				options.AllowTimedOutFactoryBackgroundCompletion = keepTimedOutFactoryResult.Value;
			return options;
		}
	}

	public class SingleLevelTests
	{

		[Theory]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1_000)]
		public async Task OnlyOneFactoryGetsCalledEvenInHighConcurrencyAsync(int accessorsCount)
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var factoryCallsCount = 0;

				var tasks = new ConcurrentBag<Task>();
				Parallel.For(0, accessorsCount, _ =>
				{
					var task = cache.GetOrSetAsync<int>(
						"foo",
						async _ =>
						{
							Interlocked.Increment(ref factoryCallsCount);
							return 42;
						},
						new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
					);
					tasks.Add(task);
				});

				await Task.WhenAll(tasks);

				Assert.Equal(1, factoryCallsCount);
			}
		}

		[Theory]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1_000)]
		public void OnlyOneFactoryGetsCalledEvenInHighConcurrency(int accessorsCount)
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var factoryCallsCount = 0;

				var tasks = new ConcurrentBag<Task>();
				Parallel.For(0, accessorsCount, _ =>
				{
					cache.GetOrSet<int>(
						"foo",
						_ =>
						{
							Interlocked.Increment(ref factoryCallsCount);
							return 42;
						},
						new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
					);
				});

				Assert.Equal(1, factoryCallsCount);
			}
		}

		[Fact]
		public async Task ReturnsStaleDataWhenFactoryFailsWithFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				await Task.Delay(1_100);
				var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Random error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public void ReturnsStaleDataWhenFactoryFailsWithFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Thread.Sleep(1_100);
				var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Random error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public async Task ThrowsWhenFactoryThrowsWithoutFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				await Task.Delay(1_100);
				await Assert.ThrowsAnyAsync<Exception>(async () =>
				{
					var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Random error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
				});
			}
		}

		[Fact]
		public void ThrowsWhenFactoryThrowsWithoutFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Thread.Sleep(1_100);
				Assert.ThrowsAny<Exception>(() =>
				{
					var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Random error"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
				});
			}
		}

		[Fact]
		public async Task ThrowsOnFactoryHardTimeoutWithoutStaleDataAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
				{
					var value = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
				});
			}
		}

		[Fact]
		public void ThrowsOnFactoryHardTimeoutWithoutStaleData()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				Assert.Throws<SyntheticTimeoutException>(() =>
				{
					var value = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
				});
			}
		}

		[Fact]
		public async Task ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				await Task.Delay(1_100);
				var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public void ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Thread.Sleep(1_100);
				var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public async Task DoesNotSoftTimeoutWithoutStaleDataAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
				Assert.Equal(21, initialValue);
			}
		}

		[Fact]
		public void DoesNotSoftTimeoutWithoutStaleData()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
				Assert.Equal(21, initialValue);
			}
		}

		[Fact]
		public async Task DoesHardTimeoutEvenWithoutStaleDataAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				await Assert.ThrowsAnyAsync<Exception>(async () =>
				{
					var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
				});
			}
		}

		[Fact]
		public void DoesHardTimeoutEvenWithoutStaleData()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				Assert.ThrowsAny<Exception>(() =>
				{
					var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
				});
			}
		}

		[Fact]
		public async Task ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				await Task.Delay(1_100);
				var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
				Assert.Equal(42, newValue);
			}
		}

		[Fact]
		public void ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Thread.Sleep(1_100);
				var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
				Assert.Equal(42, newValue);
			}
		}

		[Fact]
		public async Task SetOverwritesAnExistingValueAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				var newValue = 21;
				cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				var actualValue = await cache.GetOrDefaultAsync<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				Assert.Equal(newValue, actualValue);
			}
		}

		[Fact]
		public void SetOverwritesAnExistingValue()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				var newValue = 21;
				cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				var actualValue = cache.GetOrDefault<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				Assert.Equal(newValue, actualValue);
			}
		}

		[Fact]
		public async Task GetOrSetDoesNotOverwriteANonExpiredValueAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public void GetOrSetDoesNotOverwriteANonExpiredValue()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public async Task DoesNotReturnStaleDataIfFactorySucceedsAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				await Task.Delay(1_500);
				var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Assert.NotEqual(initialValue, newValue);
			}
		}

		[Fact]
		public void DoesNotReturnStaleDataIfFactorySucceeds()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Thread.Sleep(1_500);
				var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Assert.NotEqual(initialValue, newValue);
			}
		}

		[Fact]
		public async Task GetOrDefaultDoesReturnStaleDataWithFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				await Task.Delay(1_500);
				var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public void GetOrDefaultDoesReturnStaleDataWithFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Thread.Sleep(1_500);
				var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				Assert.Equal(initialValue, newValue);
			}
		}

		[Fact]
		public async Task GetOrDefaultDoesNotReturnStaleDataWithoutFailSafeAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
				await Task.Delay(1_500);
				var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
				Assert.NotEqual(initialValue, newValue);
			}
		}

		[Fact]
		public void GetOrDefaultDoesNotReturnStaleWithoutFailSafe()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var initialValue = 42;
				cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Thread.Sleep(1_500);
				var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
				Assert.NotEqual(initialValue, newValue);
			}
		}

		[Fact]
		public async Task FactoryTimedOutButSuccessfulDoesUpdateCachedValueAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
				var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
				await Task.Delay(1_500);
				var middleValue = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
				var interstitialValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				await Task.Delay(3_000);
				var finalValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

				Assert.Equal(42, initialValue);
				Assert.Equal(42, middleValue);
				Assert.Equal(42, interstitialValue);
				Assert.Equal(21, finalValue);
			}
		}

		[Fact]
		public void FactoryTimedOutButSuccessfulDoesUpdateCachedValue()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
				var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
				Thread.Sleep(1_500);
				var middleValue = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
				var interstitialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Thread.Sleep(3_000);
				var finalValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

				Assert.Equal(42, initialValue);
				Assert.Equal(42, middleValue);
				Assert.Equal(42, interstitialValue);
				Assert.Equal(21, finalValue);
			}
		}

		[Fact]
		public async Task TryGetReturnsCorrectlyAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var res1 = await cache.TryGetAsync<int>("foo");
				await cache.SetAsync<int>("foo", 42);
				var res2 = await cache.TryGetAsync<int>("foo");
				Assert.False(res1.Success);
				Assert.Equal(default(int), res1.Value);
				Assert.True(res2.Success);
				Assert.Equal(42, res2.Value);
			}
		}

		[Fact]
		public void TryGetReturnsCorrectly()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				var res1 = cache.TryGet<int>("foo");
				cache.Set<int>("foo", 42);
				var res2 = cache.TryGet<int>("foo");
				Assert.False(res1.Success);
				Assert.Equal(default(int), res1.Value);
				Assert.True(res2.Success);
				Assert.Equal(42, res2.Value);
			}
		}

		[Fact]
		public async Task CancelingAnOperationActuallyCancelsItAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				int res = -1;
				var sw = Stopwatch.StartNew();
				var outerCancelDelayMs = 500;
				var factoryDelayMs = 2_000;
				await Assert.ThrowsAsync<OperationCanceledException>(async () =>
				{
					var cts = new CancellationTokenSource(outerCancelDelayMs);
					await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
				});
				sw.Stop();

				Assert.Equal(-1, res);
				Assert.True(sw.ElapsedMilliseconds > outerCancelDelayMs);
				Assert.True(sw.ElapsedMilliseconds < factoryDelayMs);
			}
		}

		[Fact]
		public void CancelingAnOperationActuallyCancelsIt()
		{
			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				int res = -1;
				var sw = Stopwatch.StartNew();
				var outerCancelDelayMs = 500;
				var factoryDelayMs = 2_000;
				Assert.Throws<OperationCanceledException>(() =>
				{
					var cts = new CancellationTokenSource(outerCancelDelayMs);
					cache.GetOrSet<int>("foo", ct => { Thread.Sleep(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
				});
				sw.Stop();

				Assert.Equal(-1, res);
				Assert.True(sw.ElapsedMilliseconds > outerCancelDelayMs);
				Assert.True(sw.ElapsedMilliseconds < factoryDelayMs);
			}
		}

	}
}