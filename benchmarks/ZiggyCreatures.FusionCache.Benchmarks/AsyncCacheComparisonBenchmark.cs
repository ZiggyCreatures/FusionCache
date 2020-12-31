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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZiggyCreatures.FusionCaching.Benchmarks
{
	[MemoryDiagnoser]
	[Config(typeof(Config))]
	public class AsyncCacheComparisonBenchmark
	{

		public static Dictionary<string, List<string>> ExcessiveFactoryCalls = new Dictionary<string, List<string>>();

		public static void UpdateExcessiveFactoryCallsStatistics(string name, int expectedFactoryCallsCount, int actualFactoryCallsCount)
		{
			if (expectedFactoryCallsCount == actualFactoryCallsCount)
				return;

			if (ExcessiveFactoryCalls.ContainsKey(name) == false)
				ExcessiveFactoryCalls[name] = new List<string>();

			ExcessiveFactoryCalls[name].Add($"EXPECTED: {expectedFactoryCallsCount} - ACTUAL: {actualFactoryCallsCount}");
		}

		public static string GetCallerName([CallerMemberName] string name = "")
		{
			return name;
		}

		private class Config : ManualConfig
		{
			public Config()
			{
				AddColumn(
					StatisticColumn.P90
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

		private int FactoryCallsCount;

		private IServiceProvider ServiceProvider;

		[GlobalSetup]
		public void Setup()
		{
			// SETUP KEYS
			Keys = new List<string>();
			var foo = DateTime.UtcNow.Ticks;
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
		public async Task LazyCacheCache()
		{
			using (var cache = new MemoryCache(new MemoryCacheOptions()))
			{
				var appcache = new CachingService(new MemoryCacheProvider(cache));

				appcache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = (int)(CacheDuration.TotalSeconds) };

				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();
					FactoryCallsCount = 0;

					// FULL PARALLEL
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
						{
							var t = appcache.GetOrAddAsync<long>(
								key,
								async () =>
								{
									Interlocked.Increment(ref FactoryCallsCount);
									await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
									return DateTimeOffset.UtcNow.Ticks;
								}
							);
							tasks.Add(t);
						});
					});

					await Task.WhenAll(tasks).ConfigureAwait(false);

					UpdateExcessiveFactoryCallsStatistics(GetCallerName(), KeysCount, FactoryCallsCount);
				}

				cache.Compact(1);
			}
		}

		//[Benchmark]
		public async Task CacheTowerCache()
		{
			var cacheSettings = new CacheSettings(CacheDuration);
			await using (var cache = new CacheStack<int>(() => 0, new[] { new MemoryCacheLayer() }, new[] { new AutoCleanupExtension(TimeSpan.FromMinutes(5)) }))
			{
				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();
					FactoryCallsCount = 0;

					// FULL PARALLEL
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
						{
							var t = cache.GetOrSetAsync<long>(
								key,
								async _ =>
								{
									Interlocked.Increment(ref FactoryCallsCount);
									await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
									return DateTimeOffset.UtcNow.Ticks;
								},
								cacheSettings
							).AsTask();
							tasks.Add(t);
						});
					});

					await Task.WhenAll(tasks).ConfigureAwait(false);

					UpdateExcessiveFactoryCallsStatistics(GetCallerName(), KeysCount, FactoryCallsCount);
				}

				await cache.CleanupAsync().ConfigureAwait(false);
			}
		}

		[Benchmark]
		public async Task CacheManagerCache()
		{
			using (var cache = CacheFactory.Build<long>(p => p.WithMicrosoftMemoryCacheHandle()))
			{
				for (int i = 0; i < Rounds; i++)
				{
					//var tasks = new ConcurrentBag<Task>();
					FactoryCallsCount = 0;

					// FULL PARALLEL
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
						{
							cache.GetOrAdd(
								key,
								_ =>
								{
									Interlocked.Increment(ref FactoryCallsCount);
									Thread.Sleep(FactoryDurationMs);
									return new CacheItem<long>(
										key,
										DateTimeOffset.UtcNow.Ticks,
										CacheManager.Core.ExpirationMode.Absolute,
										CacheDuration
									);
								}
							);

							//var t = Task.Run(() =>
							//{
							//	cache.GetOrAdd(
							//		key,
							//		_ =>
							//		{
							//			Interlocked.Increment(ref FactoryCallsCount);
							//			Thread.Sleep(FactoryDurationMs);
							//			return new CacheItem<long>(
							//				key,
							//				DateTimeOffset.UtcNow.Ticks,
							//				CacheManager.Core.ExpirationMode.Absolute,
							//				CacheDuration
							//			);
							//		}
							//	);
							//});
							//tasks.Add(t);
						});
					});

					//await Task.WhenAll(tasks).ConfigureAwait(false);

					UpdateExcessiveFactoryCallsStatistics(GetCallerName(), KeysCount, FactoryCallsCount);
				}
			}
		}

		[Benchmark]
		public async Task EasyCachingCache()
		{
			var factory = ServiceProvider.GetRequiredService<IEasyCachingProviderFactory>();
			var cache = factory.GetCachingProvider("default");

			for (int i = 0; i < Rounds; i++)
			{
				var tasks = new ConcurrentBag<Task>();
				FactoryCallsCount = 0;

				// FULL PARALLEL
				Parallel.ForEach(Keys, key =>
				{
					Parallel.For(0, Accessors, _ =>
					{
						var t = cache.GetAsync<long>(
							key,
							async () =>
							{
								Interlocked.Increment(ref FactoryCallsCount);
								await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
								return DateTimeOffset.UtcNow.Ticks;
							},
							CacheDuration
						);
						tasks.Add(t);
					});
				});

				await Task.WhenAll(tasks).ConfigureAwait(false);

				UpdateExcessiveFactoryCallsStatistics(GetCallerName(), KeysCount, FactoryCallsCount);
			}

			await cache.FlushAsync();
		}

		[Benchmark]
		public async Task FusionCacheCache()
		{
			using (var cache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }))
			{
				for (int i = 0; i < Rounds; i++)
				{
					var tasks = new ConcurrentBag<Task>();
					FactoryCallsCount = 0;

					// FULL PARALLEL
					Parallel.ForEach(Keys, key =>
					{
						Parallel.For(0, Accessors, _ =>
						{
							var t = cache.GetOrSetAsync<long>(
								key,
								async ct =>
								{
									Interlocked.Increment(ref FactoryCallsCount);
									await Task.Delay(FactoryDurationMs).ConfigureAwait(false);
									return DateTimeOffset.UtcNow.Ticks;
								}
							);
							tasks.Add(t);
						});
					});

					await Task.WhenAll(tasks).ConfigureAwait(false);

					UpdateExcessiveFactoryCallsStatistics(GetCallerName(), KeysCount, FactoryCallsCount);
				}

				// NO NEED TO CLEANUP, AUTOMATICALLY DONE WHEN DISPOSING
			}
		}

	}
}