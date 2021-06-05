using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using CacheManager.Core;
using CacheTower;
using CacheTower.Extensions;
using CacheTower.Providers.Memory;
using EasyCaching.Core;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	[MemoryDiagnoser]
	[Config(typeof(Config))]
	public class AsyncComparisonBenchmark
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
		public async Task FusionCache()
		{
			using (var cache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }))
			{
				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();

					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
					   {
						   var t = cache.GetOrSetAsync<SamplePayload>(
							  key,
							  async ct =>
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

				// NO NEED TO CLEANUP, AUTOMATICALLY DONE WHEN DISPOSING
			}
		}

		[Benchmark]
		public async Task CacheManager()
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
		public async Task CacheTower()
		{
			await using (var cache = new CacheStack(new[] { new MemoryCacheLayer() }, new[] { new AutoCleanupExtension(TimeSpan.FromMinutes(5)) }))
			{
				var cacheSettings = new CacheSettings(CacheDuration, CacheDuration);

				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();

					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
					   {
						   var t = cache.GetOrSetAsync<SamplePayload>(
							  key,
							  async (old) =>
							  {
								  await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
								  return new SamplePayload();
							  },
							  cacheSettings
						  ).AsTask();
						   tasks.Add(t);
					   });
					});

					await Task.WhenAll(tasks).ConfigureAwait(false);
				}

				// CLEANUP
				await cache.CleanupAsync();
				await cache.FlushAsync();
			}
		}

		[Benchmark]
		public async Task EasyCaching()
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
						var t = cache.GetAsync<SamplePayload>(
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

			// CLEANUP
			await cache.FlushAsync();
		}

		[Benchmark]
		public async Task LazyCache()
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
						   var t = appcache.GetOrAddAsync<SamplePayload>(
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

				// CLEANUP
				cache.Compact(1);
			}
		}

	}
}