using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FusionCacheTests.OtherLibs;

// REMOVE THE abstract MODIFIER TO RUN THESE TESTS
public abstract class CacheStampedeTests_HybridCache
{
	private static readonly TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(500);

	[Theory]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1_000)]
	public async Task OnlyOneFactoryGetsCalledEvenInHighConcurrencyAsync(int accessorsCount)
	{
		var services = new ServiceCollection();

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
		services.AddHybridCache();
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

		var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<HybridCache>();

		var factoryCallsCount = 0;

		var entryOptions = new HybridCacheEntryOptions
		{
			Expiration = TimeSpan.FromMinutes(5)
		};

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, _ =>
		{
			var task = cache.GetOrCreateAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				entryOptions
			);
			tasks.Add(task.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}
}
