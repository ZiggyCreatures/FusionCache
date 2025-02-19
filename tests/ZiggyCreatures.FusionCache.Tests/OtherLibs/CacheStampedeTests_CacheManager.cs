using CacheManager.Core;
using Xunit;

namespace FusionCacheTests.OtherLibs;

// REMOVE THE abstract MODIFIER TO RUN THESE TESTS
public abstract class CacheStampedeTests_CacheManager
{
	private static readonly TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(500);

	[Theory]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1_000)]
	public void OnlyOneFactoryGetsCalledEvenInHighConcurrency(int accessorsCount)
	{
		using (var cache = CacheFactory.Build<int>(p => p.WithMicrosoftMemoryCacheHandle()))
		{
			var factoryCallsCount = 0;

			Parallel.For(0, accessorsCount, _ =>
			{
				cache.GetOrAdd(
					"foo",
					key =>
					{
						Interlocked.Increment(ref factoryCallsCount);
						Thread.Sleep(FactoryDuration);
						return new CacheItem<int>(
							key,
							42,
							ExpirationMode.Absolute,
							TimeSpan.FromSeconds(10)
						);
					}
				);
			});

			Assert.Equal(1, factoryCallsCount);
		}
	}
}
