using System;
using System.Collections.Generic;
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
	public class SequentialComparisonBenchmarkSync
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

		[Params(200)]
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
					foreach (var key in Keys)
					{
						cache.GetOrSet<SamplePayload>(
						   key,
						   ct =>
						   {
							   return new SamplePayload();
						   }
					   );
					}
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
					foreach (var key in Keys)
					{
						cache.GetOrAdd(
							key,
							_ =>
							{
								return new CacheItem<SamplePayload>(
									key,
									new SamplePayload(),
									global::CacheManager.Core.ExpirationMode.Absolute,
									CacheDuration
								);
							}
						);
					}
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
				foreach (var key in Keys)
				{
					cache.Get<SamplePayload>(
						key,
						() =>
						{
							return new SamplePayload();
						},
						CacheDuration
					);
				}
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
					foreach (var key in Keys)
					{
						appcache.GetOrAdd<SamplePayload>(
						   key,
						   () =>
						   {
							   return new SamplePayload();
						   }
					   );
					}
				}

				// CLEANUP
				cache.Compact(1);
			}
		}

	}
}
