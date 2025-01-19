using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Hybrid;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace FusionCacheTests;

public class CacheStampedeTests
{
	private static readonly TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(500);

	// FUSIONCACHE

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionAsync(MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));

		var factoryCallsCount = 0;

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, _ =>
		{
			var task = cache.GetOrSetAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			);
			tasks.Add(task.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public void FusionSync(MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));

		var factoryCallsCount = 0;

		Parallel.For(0, accessorsCount, _ =>
		{
			cache.GetOrSet<int>(
				"foo",
				_ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					Thread.Sleep(FactoryDuration);
					return 42;
				},
				new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			);
		});

		Assert.Equal(1, factoryCallsCount);
	}

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionMixedAsync(MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));

		var factoryCallsCount = 0;

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, idx =>
		{
			if (idx % 2 == 0)
			{
				var task = cache.GetOrSetAsync<int>(
				   "foo",
				   async _ =>
				   {
					   Interlocked.Increment(ref factoryCallsCount);
					   await Task.Delay(FactoryDuration);
					   return 42;
				   },
				   new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			   );
				tasks.Add(task.AsTask());
			}
			else
			{
				cache.GetOrSet<int>(
				   "foo",
				   _ =>
				   {
					   Interlocked.Increment(ref factoryCallsCount);
					   Thread.Sleep(FactoryDuration);
					   return 42;
				   },
				   new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			   );
			}
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}

	// HYBRIDCACHE ADAPTER (ASYNC-ONLY)

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionHybridAsync(MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var fusionCache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		var hybridCache = new FusionHybridCache(fusionCache);

		var factoryCallsCount = 0;

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, _ =>
		{
			var task = hybridCache.GetOrCreateAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) }
			);
			tasks.Add(task.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}

	// FUSIONCACHE + HYBRIDCACHE ADAPTER AT THE SAME TIME (ASYNC-ONLY)

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionAndFusionHybridAsync(MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var fusionCache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		var hybridCache = new FusionHybridCache(fusionCache);

		var factoryCallsCount = 0;

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, _ =>
		{
			var task1 = fusionCache.GetOrSetAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			);
			tasks.Add(task1.AsTask());
			var task2 = hybridCache.GetOrCreateAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) }
			);
			tasks.Add(task2.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}
}
