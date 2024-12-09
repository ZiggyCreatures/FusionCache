using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using CacheTower;
using CacheTower.Extensions;
using CacheTower.Providers.Memory;
using EasyCaching.Core;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class ParallelComparisonBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(
				StatisticColumn.P95
			);
		}
	}

	[Params(100)]
	public int FactoryDurationMs;

	[Params(10, 1000)]
	public int Accessors;

	[Params(100)]
	public int KeysCount;

	[Params(1, 10)]
	public int Rounds;

	private List<string> Keys = null!;
	private TimeSpan CacheDuration = TimeSpan.FromDays(10);
	private IServiceProvider ServiceProvider = null!;

	private FusionCache _FusionCache = null!;
	private FusionCache _FusionCacheNoTagging = null!;
	private FusionCache _FusionCacheProbabilistic = null!;
	private CacheStack _CacheTower = null!;
	private IEasyCachingProvider _EasyCaching = null!;
	private CachingService _LazyCache = null!;
	private HybridCache _HybridCache = null!;
	private FusionCache _FusionCacheForHybrid = null!;
	private HybridCache _FusionHybridCache = null!;

	[GlobalSetup]
	public void Setup()
	{
		// SETUP KEYS
		Keys = [];
		for (int i = 0; i < KeysCount; i++)
		{
			var key = Guid.NewGuid().ToString("N") + "-" + i.ToString();
			Keys.Add(key);
		}

		// SETUP DI
		var services = new ServiceCollection();
		services.AddEasyCaching(options => { options.UseInMemory("default"); });
#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
		services.AddHybridCache();
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
		ServiceProvider = services.BuildServiceProvider();

		// SETUP CACHES
		_FusionCache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) });
		_FusionCacheNoTagging = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration), DisableTagging = true });
		_FusionCacheProbabilistic = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }, memoryLocker: new ProbabilisticMemoryLocker());
		_CacheTower = new CacheStack(null, new CacheStackOptions([new MemoryCacheLayer()]) { Extensions = [new AutoCleanupExtension(TimeSpan.FromMinutes(5))] });
		_EasyCaching = ServiceProvider.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider("default");
		_LazyCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())));
		_LazyCache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = (int)(CacheDuration.TotalSeconds) };
		_HybridCache = ServiceProvider.GetRequiredService<HybridCache>();
		_FusionCacheForHybrid = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) });
		_FusionHybridCache = new FusionHybridCache(_FusionCacheForHybrid);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_FusionCache.Dispose();
		_FusionCacheNoTagging.Dispose();
		_FusionCacheProbabilistic.Dispose();
		_CacheTower.DisposeAsync().AsTask().Wait();
		_FusionCacheForHybrid.Dispose();
	}

	[Benchmark(Baseline = true)]
	public async Task FusionCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _FusionCache.GetOrSetAsync<SamplePayload>(
						key,
						async (ctx, ct) =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						},
						default,
						null,
						null,
						default
					);
					tasks.Add(t.AsTask());
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task FusionCache_NoTagging()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _FusionCache.GetOrSetAsync<SamplePayload>(
						key,
						async (ctx, ct) =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						},
						default,
						null,
						null,
						default
					);
					tasks.Add(t.AsTask());
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	//[Benchmark]
	//public async Task FusionCacheProbabilistic()
	//{
	//	for (int i = 0; i < Rounds; i++)
	//	{
	//		var tasks = new ConcurrentBag<Task>();

	//		Parallel.ForEach(Keys, key =>
	//		{
	//			Parallel.For(0, Accessors, _ =>
	//			{
	//				var t = _FusionCacheProbabilistic.GetOrSetAsync<SamplePayload>(
	//				   key,
	//				   async ct =>
	//				   {
	//					   await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
	//					   return new SamplePayload();
	//				   }
	//			   );
	//				tasks.Add(t.AsTask());
	//			});
	//		});

	//		await Task.WhenAll(tasks).ConfigureAwait(false);
	//	}
	//}

	// NOTE: EXCLUDED BECAUSE IT DOES NOT SUPPORT CACHE STAMPEDE PROTECTION, SO IT WOULD NOT BE COMPARABLE
	// [Benchmark]
	//public void CacheManager()
	//{
	//	using var cache = CacheFactory.Build<SamplePayload>(p => p.WithMicrosoftMemoryCacheHandle());

	//	for (int i = 0; i < Rounds; i++)
	//	{
	//		Parallel.ForEach(Keys, key =>
	//		{
	//			Parallel.For(0, Accessors, _ =>
	//			{
	//				cache.GetOrAdd(
	//					key,
	//					_ =>
	//					{
	//						Thread.Sleep(FactoryDurationMs);
	//						return new CacheItem<SamplePayload>(
	//							key,
	//							new SamplePayload(),
	//							global::CacheManager.Core.ExpirationMode.Absolute,
	//							CacheDuration
	//						);
	//					}
	//				);
	//			});
	//		});
	//	}

	//	// CLEANUP
	//	cache.Clear();
	//}

	[Benchmark]
	public async Task CacheTower()
	{
		var cacheSettings = new CacheSettings(CacheDuration, CacheDuration);

		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _CacheTower.GetOrSetAsync<SamplePayload>(
						key,
						async (old) =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						},
						cacheSettings
					);
					tasks.Add(t.AsTask());
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task EasyCaching()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _EasyCaching.GetAsync<SamplePayload>(
						key,
						async () =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						},
						CacheDuration
					);
					tasks.Add(t);
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task LazyCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _LazyCache.GetOrAddAsync<SamplePayload>(
						key,
						async () =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						}
					);
					tasks.Add(t);
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task HybridCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _HybridCache.GetOrCreateAsync<SamplePayload>(
						key,
						async _ =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						}
					);
					tasks.Add(t.AsTask());
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task FusionHybridCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			var tasks = new ConcurrentBag<Task>();

			Parallel.ForEach(Keys, key =>
			{
				Parallel.For(0, Accessors, _ =>
				{
					var t = _FusionHybridCache.GetOrCreateAsync<SamplePayload>(
						key,
						async _ =>
						{
							await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
							return new SamplePayload();
						}
					);
					tasks.Add(t.AsTask());
				});
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}
}
