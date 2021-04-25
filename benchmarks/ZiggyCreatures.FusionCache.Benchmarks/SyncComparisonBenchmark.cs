using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using CacheManager.Core;
using EasyCaching.Core;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	[MemoryDiagnoser]
	[Config(typeof(Config))]
	public class SyncComparisonBenchmark
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

		[Params(20)]
		public int FactoryDurationMs;

		[Params(10, 100)]
		public int Accessors;

		[Params(100)]
		public int KeysCount;

		[Params(1, 50)]
		public int Rounds;

		private List<string> Keys;
		private TimeSpan CacheDuration = TimeSpan.FromDays(10);
		private IServiceProvider ServiceProvider;

		[GlobalSetup]
		public void Setup()
		{
			// SETUP KEYS
			Keys = new List<string>();
			for (int i = 0; i < KeysCount; i++)
			{
				var key = Guid.NewGuid().ToString("N") + "-" + i.ToString();
				Keys.Add(key);
			}

			// SETUP DI
			var services = new ServiceCollection();
			services.AddEasyCaching(options => { options.UseInMemory("default"); });
			ServiceProvider = services.BuildServiceProvider();
		}

		[Benchmark(Baseline = true)]
		public void FusionCache()
		{
			using (var cache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }))
			{
				for (int i = 0; i < Rounds; i++)
				{
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
					   {
					   cache.GetOrSet<SamplePayload>(
						  key,
						  ct =>
						  {
							   Thread.Sleep(FactoryDurationMs);
							   return new SamplePayload();
						   }
					  );
				   });
					});
				}

				// NO NEED TO CLEANUP, AUTOMATICALLY DONE WHEN DISPOSING
			}
		}

		[Benchmark]
		public void CacheManager()
		{
			using (var cache = CacheFactory.Build<SamplePayload>(p => p.WithMicrosoftMemoryCacheHandle()))
			{
				for (int i = 0; i < Rounds; i++)
				{
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
					   {
					   cache.GetOrAdd(
						  key,
						  _ =>
						  {
							   Thread.Sleep(FactoryDurationMs);
							   return new CacheItem<SamplePayload>(
								  key,
								  new SamplePayload(),
								  global::CacheManager.Core.ExpirationMode.Absolute,
								  CacheDuration
							  );
						   }
					  );

				   });
					});
				}

				// CLEANUP
				cache.Clear();
			}
		}

		[Benchmark]
		public void EasyCaching()
		{
			var factory = ServiceProvider.GetRequiredService<IEasyCachingProviderFactory>();
			var cache = factory.GetCachingProvider("default");

			for (int i = 0; i < Rounds; i++)
			{
				var tasks = new ConcurrentBag<Task>();

				Parallel.ForEach(Keys, key =>
				{
					Parallel.For(0, Accessors, _ =>
					{
					cache.Get<SamplePayload>(
						key,
						() =>
						{
							Thread.Sleep(FactoryDurationMs);
							return new SamplePayload();
						},
						CacheDuration
					);
				});
				});
			}

			// CLEANUP
			cache.Flush();
		}

		[Benchmark]
		public void LazyCache()
		{
			using (var cache = new MemoryCache(new MemoryCacheOptions()))
			{
				var appcache = new CachingService(new MemoryCacheProvider(cache));

				appcache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = (int)(CacheDuration.TotalSeconds) };

				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();

					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
					   {
					   appcache.GetOrAdd<SamplePayload>(
						  key,
						  () =>
						  {
							   Thread.Sleep(FactoryDurationMs);
							   return new SamplePayload();
						   }
					  );
				   });
					});
				}

				// CLEANUP
				cache.Compact(1);
			}
		}

	}
}