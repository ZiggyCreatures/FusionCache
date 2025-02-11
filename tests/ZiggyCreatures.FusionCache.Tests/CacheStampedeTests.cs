using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
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
	public async Task FusionAsync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			cache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			cache.DefaultEntryOptions.EnableAutoClone = true;
		}

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
	public void FusionSync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			cache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			cache.DefaultEntryOptions.EnableAutoClone = true;
		}

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
	public async Task FusionMixedAsync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var cache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			cache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			cache.DefaultEntryOptions.EnableAutoClone = true;
		}

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
	public async Task FusionHybridAsync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var fusionCache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			fusionCache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			fusionCache.DefaultEntryOptions.EnableAutoClone = true;
		}
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
				new HybridCacheEntryOptions { LocalCacheExpiration = TimeSpan.FromSeconds(10) }
			);
			tasks.Add(task.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}

	// FUSIONCACHE + HYBRIDCACHE ADAPTER AT THE SAME TIME (ASYNC-ONLY)

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionAndFusionHybridAsync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var fusionCache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			fusionCache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			fusionCache.DefaultEntryOptions.EnableAutoClone = true;
		}
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
				new HybridCacheEntryOptions { LocalCacheExpiration = TimeSpan.FromSeconds(10) }
			);
			tasks.Add(task2.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}

	// FUSIONCACHE + HYBRIDCACHE ADAPTER, ASYNC + ASYNC ALL AT THE SAME TIME

	[Theory]
	[ClassData(typeof(CacheStampedeClassData))]
	public async Task FusionAndFusionHybridAsyncAndSync(SerializerType? serializerType, MemoryLockerType memoryLockerType, int accessorsCount)
	{
		using var fusionCache = new FusionCache(new FusionCacheOptions(), memoryLocker: TestsUtils.GetMemoryLocker(memoryLockerType));
		if (serializerType is not null)
		{
			fusionCache.SetupDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TestsUtils.GetSerializer(serializerType.Value));
			fusionCache.DefaultEntryOptions.EnableAutoClone = true;
		}
		var hybridCache = new FusionHybridCache(fusionCache);

		var factoryCallsCount = 0;

		var tasks = new ConcurrentBag<Task>();
		Parallel.For(0, accessorsCount, _ =>
		{
			fusionCache.GetOrSet<int>(
				"foo",
				_ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					Thread.Sleep(FactoryDuration);
					return 42;
				},
				new FusionCacheEntryOptions(TimeSpan.FromSeconds(10))
			);

			var task = hybridCache.GetOrCreateAsync<int>(
				"foo",
				async _ =>
				{
					Interlocked.Increment(ref factoryCallsCount);
					await Task.Delay(FactoryDuration);
					return 42;
				},
				new HybridCacheEntryOptions { LocalCacheExpiration = TimeSpan.FromSeconds(10) }
			);
			tasks.Add(task.AsTask());
		});

		await Task.WhenAll(tasks);

		Assert.Equal(1, factoryCallsCount);
	}
}
