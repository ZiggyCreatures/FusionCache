using System.Collections.Concurrent;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

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
}
