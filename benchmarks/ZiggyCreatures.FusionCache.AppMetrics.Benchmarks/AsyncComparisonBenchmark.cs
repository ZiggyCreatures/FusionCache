using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.FusionCache.AppMetrics;

namespace ZiggyCreatures.FusionCaching.AppMetrics.Benchmarks
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
        private IMetricsRoot AppMetrics;

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

            AppMetrics = new MetricsBuilder()
                .Configuration.Configure(
                    options =>
                    {
                        options.DefaultContextLabel = "appMetrics_BenchMarkDotNet";
                        options.Enabled = true;
                        options.ReportingEnabled = true;
                    })
                .Build();
        }
        
        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Metrics")]
        public async Task FusionCache()
        {
            using (var cache = new Caching.Fusion.FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }))
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
        [BenchmarkCategory("Metrics")]
        public async Task FusionCacheWithAppMetrics()
        {
            var metrics = new AppMetricsProvider(AppMetrics, "FusionCache");
            using (var cache = new Caching.Fusion.FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }, metrics: metrics))
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
    }
}
