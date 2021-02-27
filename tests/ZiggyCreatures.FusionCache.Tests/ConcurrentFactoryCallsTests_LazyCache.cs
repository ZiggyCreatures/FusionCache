using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZiggyCreatures.Caching.Fusion.Tests
{

	// REMOVE THE abstract MODIFIER TO RUN THESE TESTS
	public abstract class ConcurrentFactoryCallsTests_LazyCache
	{

		static TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(500);

		[Theory]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1_000)]
		public async Task OnlyOneFactoryGetsCalledEvenInHighConcurrencyAsync(int accessorsCount)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var cache = new CachingService(new MemoryCacheProvider(memoryCache));
				cache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = 10 };

				var factoryCallsCount = 0;

				var tasks = new ConcurrentBag<Task>();
				Parallel.For(0, accessorsCount, _ =>
				{
					var task = cache.GetOrAddAsync<int>(
						"foo",
						async _ =>
						{
							Interlocked.Increment(ref factoryCallsCount);
							await Task.Delay(FactoryDuration).ConfigureAwait(false);
							return 42;
						}
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
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var cache = new CachingService(new MemoryCacheProvider(memoryCache));
				cache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = 10 };

				var factoryCallsCount = 0;

				Parallel.For(0, accessorsCount, _ =>
				{
					cache.GetOrAdd<int>(
						"foo",
						_ =>
						{
							Interlocked.Increment(ref factoryCallsCount);
							Thread.Sleep(FactoryDuration);
							return 42;
						}
					);
				});

				Assert.Equal(1, factoryCallsCount);
			}
		}

		[Theory]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1_000)]
		public async Task OnlyOneFactoryGetsCalledEvenInMixedHighConcurrencyAsync(int accessorsCount)
		{
			using (var memoryCache = new MemoryCache(new MemoryCacheOptions()))
			{
				var cache = new CachingService(new MemoryCacheProvider(memoryCache));
				cache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = 10 };

				var factoryCallsCount = 0;

				var tasks = new ConcurrentBag<Task>();
				Parallel.For(0, accessorsCount, idx =>
				{
					if (idx % 2 == 0)
					{
						var task = cache.GetOrAddAsync<int>(
							"foo",
							async _ =>
							{
								Interlocked.Increment(ref factoryCallsCount);
								await Task.Delay(FactoryDuration).ConfigureAwait(false);
								return 42;
							}
						);
						tasks.Add(task);
					}
					else
					{
						cache.GetOrAdd<int>(
							"foo",
							_ =>
							{
								Interlocked.Increment(ref factoryCallsCount);
								Thread.Sleep(FactoryDuration);
								return 42;
							}
						);
					}
				});

				await Task.WhenAll(tasks);

				Assert.Equal(1, factoryCallsCount);
			}
		}

	}

}