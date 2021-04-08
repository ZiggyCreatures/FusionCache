using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.FusionCache.EventCounters;

namespace ZiggyCreatures.FusionCaching.EventCounters.Benchmarks
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
        private TestEventListener Listener;
        
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

        }
        
        [Benchmark(Baseline = true)]
        public async Task FusionCache()
        {
            // Creates a MemoryCache in FusionCache
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
        public async Task FusionCacheWithEventCounters()
        {
            // create memory cache externall so it can be shared with FusionCacheEventSource metrics provider so it can reporting on cache item count.
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var metrics = new FusionCacheEventSource("fusionCache", memoryCache);
            
            using (var cache = new Caching.Fusion.FusionCache(
                new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) }, 
                memoryCache,  
                metrics: metrics))
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
        public async Task FusionCacheWithEventCountersAndListener()
        {
            // create memory cache externall so it can be shared with FusionCacheEventSource metrics provider so it can reporting on cache item count.
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var metrics = new FusionCacheEventSource("fusionCache", memoryCache);
            const long AllKeywords = -1;

            using (Listener = new TestEventListener()) // enable some external listener
            {
                Listener.EnableEvents(metrics, EventLevel.Verbose, (EventKeywords)AllKeywords,
                    new Dictionary<string, string>
                    {
                        ["EventCounterIntervalSec"] = "5"
                    });

                using (var cache = new Caching.Fusion.FusionCache(
                    new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) },
                    memoryCache, 
                    metrics: metrics))
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
}
