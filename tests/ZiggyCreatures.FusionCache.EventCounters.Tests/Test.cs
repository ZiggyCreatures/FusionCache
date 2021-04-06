using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.FusionCache.EventCounters;

namespace ZiggyCreatures.Caching.Fusion.EventCounters.Tests
{

    public class Test
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public Test(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void SetCacheName()
        {
            using (var cache = new MemoryCache(new MemoryCacheOptions()))
            {
                using (var eventSource = new FusionCacheEventSource("testCacheName", cache))
                {
                    Assert.Equal("testCacheName", eventSource.Name);
                }
            }
        }

        [Fact]
        public void LoadPluginDirectly()
        {
            var scanner = new MetricsAssemblyScanner();
            using (var cache = new MemoryCache(new MemoryCacheOptions()))
            {
                var metrics = scanner.GetPlugin("testCacheName", cache);
                Assert.Equal("testCacheName", ((EventSource)metrics).Name);
                ((IDisposable)metrics).Dispose();
            }
        }

        [Fact]
        public void LoadPluginWithItemCountCheck()
        {
            using (var listener = new TestEventListener())
            {
                using (var cache = new FusionCache(
                new FusionCacheOptions(),
                new MemoryCache(new MemoryCacheOptions()),
                cacheName: "testCacheName"))
                {
                    var eventSource = EventSource.GetSources().Single(es => es.Name == "testCacheName");
                    const long AllKeywords = -1;
                    listener.EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)AllKeywords, new Dictionary<string, string>
                    {
                        ["EventCounterIntervalSec"] = "1"
                    });

                    for (int i = 0; i < 100; i++)
                    {
                        cache.GetOrSet($"A-Key-{i}", (cts) => $"A-Value{i}");
                    }

                    // Let EventListener poll for data
                    System.Threading.Thread.Sleep(2500);
                    
                    var messages = listener.Messages.ToList();
                    AssertItemCount(messages);
                    AssertCacheNameInjected(messages);
                }
            }
        }

        private static void AssertItemCount(List<EventWrittenEventArgs> messages)
        {
            long itemCount = 0;
            System.Threading.Thread.Sleep(3000);


            foreach (var eventData in messages)
            {
                bool itemCountFlag = false;
                for (int i = 0; i < eventData.Payload.Count; ++i)
                {
                    if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                    {
                        if (eventPayload.TryGetValue("Name", out object nameValue))
                        {
                            if (nameValue.ToString() == "ITEM_COUNT")
                            {
                                itemCountFlag = true;
                            }
                        }

                        if (itemCountFlag)
                        {
                            if (eventPayload.TryGetValue("Mean", out object meanValue))
                            {
                                itemCount = Convert.ToInt64(meanValue);
                            }
                        }
                    }
                }
            }

            Assert.Equal(100, itemCount);
        }
        

        private static void AssertCacheNameInjected(List<EventWrittenEventArgs> messages)
        {
            string cacheName = null;
           

            foreach (var eventData in messages)
            {
                bool itemCountFlag = false;
                for (int i = 0; i < eventData.Payload.Count; ++i)
                {
                    if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                    {
                        if (eventPayload.TryGetValue("Metadata", out object metaDataValue))
                        {
                            var metaDataString = Convert.ToString(metaDataValue);
                            var metaData = metaDataString
                                .Split(',')
                                .Select(item => item.Split(':'))
                                .ToDictionary(s => s[0], s => s[1]);

                            cacheName = metaData[FusionCacheEventSource.Tags.CacheName];
                        }
                    }
                }
            }

            Assert.Equal("testCacheName", cacheName);
        }
    }
}
