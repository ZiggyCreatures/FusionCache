using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CacheTower;
using CacheTower.Extensions;
using CacheTower.Providers.Memory;
using Xunit;

namespace ZiggyCreatures.Caching.Fusion.Tests
{

	// REMOVE THE abstract MODIFIER TO RUN THESE TESTS
	public abstract class ConcurrentFactoryCallsTests_CacheTower
	{

		static TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(500);

		[Theory]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1_000)]
		public async Task OnlyOneFactoryGetsCalledEvenInHighConcurrencyAsync(int accessorsCount)
		{
			await using (var cache = new CacheStack(new[] { new MemoryCacheLayer() }, new[] { new AutoCleanupExtension(TimeSpan.FromMinutes(5)) }))
			{
				var cacheSettings = new CacheSettings(TimeSpan.FromSeconds(10));

				var factoryCallsCount = 0;

				var tasks = new ConcurrentBag<Task>();
				Parallel.For(0, accessorsCount, _ =>
				{
					var task = cache.GetOrSetAsync<int>(
						"foo",
						async old =>
						{
						Interlocked.Increment(ref factoryCallsCount);
						await Task.Delay(FactoryDuration).ConfigureAwait(false);
						return 42;
					},
						cacheSettings
					);
					tasks.Add(task.AsTask());
				});

				await Task.WhenAll(tasks);

				Assert.Equal(1, factoryCallsCount);
			}
		}

	}

}