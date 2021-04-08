using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZiggyCreatures.Caching.Fusion.Tests
{
    public class TestMetrics : IFusionMetrics
    {
        public int CacheHitCounter = 0;
        public int CacheMissCounter = 0;
        public int CachStaleHitCounter = 0;
        public int CacheBackgroundRefreshCounter = 0;
        public int CacheExpiredCounter = 0;
        public int CacheCapacityExpiredCounter = 0;
        public int CacheRemovedCounter = 0;
        public int CacheReplacedCounter = 0;
        public int CacheEvictedCounter = 0;

        public int TotalCounters
        {
            get
            {
                return 
                      CacheHitCounter
                    + CacheMissCounter
                    + CachStaleHitCounter
                    + CacheBackgroundRefreshCounter
                    + CacheExpiredCounter
                    + CacheCapacityExpiredCounter
                    + CacheRemovedCounter
                    + CacheReplacedCounter
                    + CacheEvictedCounter;
            }
        }


        public void CacheHit()
        {
            Interlocked.Increment(ref CacheHitCounter);
        }

        public void CacheMiss()
        {
            Interlocked.Increment(ref CacheMissCounter);
        }

        public void CacheStaleHit()
        {
            Interlocked.Increment(ref CachStaleHitCounter);
        }

        public void CacheBackgroundRefresh()
        {
            Interlocked.Increment(ref CacheBackgroundRefreshCounter);
        }

        public void CacheExpired()
        {
            Interlocked.Increment(ref CacheExpiredCounter);
        }

        public void CacheCapacityExpired()
        {
            Interlocked.Increment(ref CacheCapacityExpiredCounter);
        }

        public void CacheRemoved()
        {
            Interlocked.Increment(ref CacheRemovedCounter);
        }

        public void CacheReplaced()
        {
            Interlocked.Increment(ref CacheReplacedCounter);
        }

        public void CacheEvicted()
        {
            Interlocked.Increment(ref CacheEvictedCounter);
        }
    }
    
    public class SingleLevelMetricTests
    {
        [Fact]
        public async Task ReturnsStaleDataWhenFactoryFailsWithFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                await Task.Delay(1_100);
                var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(initialValue, newValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void ReturnsStaleDataWhenFactoryFailsWithFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(1, testMetrics.CacheMissCounter); 
                Thread.Sleep(1_100);
                var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(initialValue, newValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task ThrowsWhenFactoryThrowsWithoutFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                await Task.Delay(1_100);
                await Assert.ThrowsAnyAsync<Exception>(async () =>
                {
                    var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
                });
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void ThrowsWhenFactoryThrowsWithoutFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Thread.Sleep(1_100);
                Assert.ThrowsAny<Exception>(() =>
                {
                    var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
                });
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task ThrowsOnFactoryHardTimeoutWithoutStaleDataAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
                {
                    var value = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
                });
                Assert.Equal(0, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }


        [Fact]
        public void ThrowsOnFactoryHardTimeoutWithoutStaleData()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                Assert.Throws<SyntheticTimeoutException>(() =>
                {
                    var value = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
                });
                Assert.Equal(0, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                await Task.Delay(1_100);
                var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
                Assert.Equal(initialValue, newValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Thread.Sleep(1_100);
                var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
                Assert.Equal(initialValue, newValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task DoesNotSoftTimeoutWithoutStaleDataAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
                Assert.Equal(21, initialValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void DoesNotSoftTimeoutWithoutStaleData()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
                Assert.Equal(21, initialValue);
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task DoesHardTimeoutEvenWithoutStaleDataAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                await Assert.ThrowsAnyAsync<Exception>(async () =>
                {
                    var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
                });
                Assert.Equal(0, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void DoesHardTimeoutEvenWithoutStaleData()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                Assert.ThrowsAny<Exception>(() =>
                {
                    var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
                });
                Assert.Equal(0, testMetrics.CacheMissCounter);
                Assert.Equal(0, testMetrics.CacheHitCounter);
                Assert.Equal(0, testMetrics.CachStaleHitCounter);
                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(0, testMetrics.TotalCounters);
                await Task.Delay(1_100);
                var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
                Assert.Equal(42, newValue);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(0, testMetrics.TotalCounters);
                Thread.Sleep(1_100);
                var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
                Assert.Equal(42, newValue);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
            }
        }


        [Fact]
        public async Task SetOverwritesAnExistingValueAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                var newValue = 21;
                cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                var actualValue = await cache.GetOrDefaultAsync<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                Assert.Equal(newValue, actualValue);
            }
        }

        [Fact]
        public void SetOverwritesAnExistingValue()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                var newValue = 21;
                cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                var actualValue = cache.GetOrDefault<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                Assert.Equal(newValue, actualValue);
            }
        }

        [Fact]
        public async Task GetOrSetDoesNotOverwriteANonExpiredValueAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                Assert.Equal(initialValue, newValue);
            }
        }

        [Fact]
        public void GetOrSetDoesNotOverwriteANonExpiredValue()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                Assert.Equal(initialValue, newValue);
            }
        }

        [Fact]
        public async Task DoesNotReturnStaleDataIfFactorySucceedsAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(1, testMetrics.CacheMissCounter);
                await Task.Delay(1_500); // Wait causes logically expired cache 
                var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(2, testMetrics.CacheMissCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                Assert.NotEqual(initialValue, newValue);
            }
        }

        [Fact]
        public void DoesNotReturnStaleDataIfFactorySucceeds()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Thread.Sleep(1_500);  // Wait causes logically expired cache 
                var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(2, testMetrics.CacheMissCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                Assert.NotEqual(initialValue, newValue);
            }
        }

        [Fact]
        public async Task GetOrDefaultDoesReturnStaleDataWithFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                await Task.Delay(1_500);
                var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                Assert.Equal(initialValue, newValue);
            }
        }

        [Fact]
        public void GetOrDefaultDoesReturnStaleDataWithFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Thread.Sleep(1_500);
                var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                Assert.Equal(initialValue, newValue);
            }
        }

        [Fact]
        public async Task GetOrDefaultDoesNotReturnStaleDataWithoutFailSafeAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
                await Task.Delay(1_500);
                var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
                Assert.Equal(0, testMetrics.TotalCounters); // do we need a default counter?
                Assert.NotEqual(initialValue, newValue);
            }
        }

        [Fact]
        public void GetOrDefaultDoesNotReturnStaleWithoutFailSafe()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = 42;
                cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Thread.Sleep(1_500);
                var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
                Assert.Equal(0, testMetrics.TotalCounters); // do we need a default counter?
                Assert.NotEqual(initialValue, newValue);
            }
        }

        [Fact]
        public async Task FactoryTimedOutButSuccessfulDoesUpdateCachedValueAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
                var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                await Task.Delay(1_500);
                var middleValue = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
                Assert.Equal(2, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
                var interstitialValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(3, testMetrics.CacheHitCounter);
                Assert.Equal(4, testMetrics.TotalCounters);
                await Task.Delay(3_000);
                var finalValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(4, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CacheBackgroundRefreshCounter);
                Assert.Equal(6, testMetrics.TotalCounters);

                Assert.Equal(42, initialValue);
                Assert.Equal(42, middleValue);
                Assert.Equal(42, interstitialValue);
                Assert.Equal(21, finalValue);
            }
        }

        [Fact]
        public void FactoryTimedOutButSuccessfulDoesUpdateCachedValue()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
                var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Thread.Sleep(1_500);
                var middleValue = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
                Assert.Equal(2, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CachStaleHitCounter);
                Assert.Equal(3, testMetrics.TotalCounters);
                var interstitialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(3, testMetrics.CacheHitCounter);
                Assert.Equal(4, testMetrics.TotalCounters);
                Thread.Sleep(3_000);
                var finalValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
                Assert.Equal(4, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.CacheBackgroundRefreshCounter);
                Assert.Equal(6, testMetrics.TotalCounters);
                
                Assert.Equal(42, initialValue);
                Assert.Equal(42, middleValue);
                Assert.Equal(42, interstitialValue);
                Assert.Equal(21, finalValue);
            }
        }

        [Fact]
        public async Task TryGetReturnsCorrectlyAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var res1 = await cache.TryGetAsync<int>("foo");
                await cache.SetAsync<int>("foo", 42);
                var res2 = await cache.TryGetAsync<int>("foo");
                Assert.False(res1.HasValue);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    var foo = res1.Value;
                });
                Assert.True(res2.HasValue);
                Assert.Equal(42, res2.Value);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void TryGetReturnsCorrectly()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var res1 = cache.TryGet<int>("foo");
                cache.Set<int>("foo", 42);
                var res2 = cache.TryGet<int>("foo");
                Assert.False(res1.HasValue);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    var foo = res1.Value;
                });
                Assert.True(res2.HasValue);
                Assert.Equal(42, res2.Value);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task CancelingAnOperationActuallyCancelsItAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                int res = -1;
                var sw = Stopwatch.StartNew();
                var outerCancelDelayMs = 500;
                var factoryDelayMs = 2_000;
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    var cts = new CancellationTokenSource(outerCancelDelayMs);
                    res = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
                });
                sw.Stop();

                Assert.Equal(-1, res);
                // TODO: MAYBE DON'T RELY ON ELAPSED TIME
                Assert.True(sw.ElapsedMilliseconds > outerCancelDelayMs, "Elapsed is lower or equal than outer cancel");
                Assert.True(sw.ElapsedMilliseconds < factoryDelayMs, "Elapsed is greater or equal than factory delay");

                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void CancelingAnOperationActuallyCancelsIt()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                int res = -1;
                var sw = Stopwatch.StartNew();
                var outerCancelDelayMs = 500;
                var factoryDelayMs = 2_000;
                Assert.ThrowsAny<OperationCanceledException>(() =>
                {
                    var cts = new CancellationTokenSource(outerCancelDelayMs);
                    res = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
                });
                sw.Stop();

                Assert.Equal(-1, res);
                // TODO: MAYBE DON'T RELY ON ELAPSED TIME
                Assert.True(sw.ElapsedMilliseconds > outerCancelDelayMs, "Elapsed is lower or equal than outer cancel");
                Assert.True(sw.ElapsedMilliseconds < factoryDelayMs, "Elapsed is greater or equal than factory delay");

                Assert.Equal(0, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task HandlesFlexibleSimpleTypeConversionsAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = (object)42;
                await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
                var newValue = await cache.GetOrDefaultAsync<int>("foo");
                Assert.Equal(initialValue, newValue);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void HandlesFlexibleSimpleTypeConversions()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = (object)42;
                cache.Set("foo", initialValue, TimeSpan.FromHours(24));
                var newValue = cache.GetOrDefault<int>("foo");
                Assert.Equal(initialValue, newValue);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task HandlesFlexibleComplexTypeConversionsAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = (object)SampleComplexObject.CreateRandom();
                await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
                var newValue = await cache.GetOrDefaultAsync<SampleComplexObject>("foo");
                Assert.NotNull(newValue);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void HandlesFlexibleComplexTypeConversions()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var initialValue = (object)SampleComplexObject.CreateRandom();
                cache.Set("foo", initialValue, TimeSpan.FromHours(24));
                var newValue = cache.GetOrDefault<SampleComplexObject>("foo");
                Assert.NotNull(newValue);

                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task GetOrDefaultDoesNotSetAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var foo = await cache.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(0, testMetrics.TotalCounters); // Again wondering if there should be a DEFAULT counter metric
                var bar = await cache.GetOrDefaultAsync<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(0, testMetrics.TotalCounters);
                var baz = await cache.TryGetAsync<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(42, foo);
                Assert.Equal(21, bar);
                Assert.False(baz.HasValue);
            }
        }

        [Fact]
        public void GetOrDefaultDoesNotSet()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var foo = cache.GetOrDefault<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(0, testMetrics.TotalCounters); // Again wondering if there should be a DEFAULT counter metric
                var bar = cache.GetOrDefault<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(0, testMetrics.TotalCounters); // Again wondering if there should be a DEFAULT counter metric
                var baz = cache.TryGet<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)));
                Assert.Equal(42, foo);
                Assert.Equal(21, bar);
                Assert.False(baz.HasValue);
            }
        }

        [Fact]
        public async Task GetOrSetWithDefaultValueWorksAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var foo = 42;
                await cache.GetOrSetAsync<int>("foo", foo, TimeSpan.FromHours(24));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                var bar = await cache.GetOrDefaultAsync<int>("foo", 21);
                Assert.Equal(foo, bar);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public void GetOrSetWithDefaultValueWorks()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var foo = 42;
                cache.GetOrSet<int>("foo", foo, TimeSpan.FromHours(24));
                Assert.Equal(1, testMetrics.CacheMissCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                var bar = cache.GetOrDefault<int>("foo", 21);
                Assert.Equal(foo, bar);
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
            }
        }

        [Fact]
        public async Task ThrottleDurationWorksCorrectlyAsync()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var duration = TimeSpan.FromSeconds(1);
                var throttleDuration = TimeSpan.FromSeconds(3);

                // SET THE VALUE (WITH FAIL-SAFE ENABLED)
                await cache.SetAsync("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration));
                // LET IT EXPIRE
                await Task.Delay(duration).ConfigureAwait(false);
                // CHECK EXPIRED (WITHOUT FAIL-SAFE)
                var nope = await cache.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));
                Assert.Equal(0, testMetrics.TotalCounters);
                // ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
                var throttled1 = await cache.GetOrDefaultAsync("foo", 1, opt => opt.SetFailSafe(true, throttleDuration: throttleDuration));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                // WAIT A LITTLE BIT (LESS THAN THE DURATION)
                await Task.Delay(100).ConfigureAwait(false);
                // GET THE THROTTLED (NON EXPIRED) VALUE
                var throttled2 = await cache.GetOrDefaultAsync("foo", 2, opt => opt.SetFailSafe(false));
                Assert.Equal(2, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                // LET THE THROTTLE DURATION PASS
                await Task.Delay(throttleDuration).ConfigureAwait(false);
                // FALLBACK TO THE DEFAULT VALUE
                var default3 = await cache.GetOrDefaultAsync("foo", 3, opt => opt.SetFailSafe(false));
                Assert.Equal(2, testMetrics.TotalCounters);
                
                Assert.False(nope.HasValue);
                Assert.Equal(42, throttled1);
                Assert.Equal(42, throttled2);
                Assert.Equal(3, default3);
            }
        }

        [Fact]
        public void ThrottleDurationWorksCorrectly()
        {
            var testMetrics = new TestMetrics();
            using (var cache = new FusionCache(new FusionCacheOptions(), metrics: testMetrics))
            {
                var duration = TimeSpan.FromSeconds(1);
                var throttleDuration = TimeSpan.FromSeconds(3);

                // SET THE VALUE (WITH FAIL-SAFE ENABLED)
                cache.Set("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration));
                // LET IT EXPIRE
                Thread.Sleep(duration);
                // CHECK EXPIRED (WITHOUT FAIL-SAFE)
                var nope = cache.TryGet<int>("foo", opt => opt.SetFailSafe(false));
                // ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
                var throttled1 = cache.GetOrDefault("foo", 1, opt => opt.SetFailSafe(true, throttleDuration: throttleDuration));
                Assert.Equal(1, testMetrics.CacheHitCounter);
                Assert.Equal(1, testMetrics.TotalCounters);
                // WAIT A LITTLE BIT (LESS THAN THE DURATION)
                Thread.Sleep(100);
                // GET THE THROTTLED (NON EXPIRED) VALUE
                var throttled2 = cache.GetOrDefault("foo", 2, opt => opt.SetFailSafe(false));
                Assert.Equal(2, testMetrics.CacheHitCounter);
                Assert.Equal(2, testMetrics.TotalCounters);
                // LET THE THROTTLE DURATION PASS
                Thread.Sleep(throttleDuration);
                // FALLBACK TO THE DEFAULT VALUE
                var default3 = cache.GetOrDefault("foo", 3, opt => opt.SetFailSafe(false));
                Assert.Equal(2, testMetrics.TotalCounters);
                
                Assert.False(nope.HasValue);
                Assert.Equal(42, throttled1);
                Assert.Equal(42, throttled2);
                Assert.Equal(3, default3);
            }
        }

    }
}
