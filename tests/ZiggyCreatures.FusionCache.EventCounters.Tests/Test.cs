using Microsoft.Extensions.Caching.Memory;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.FusionCache.EventCounters;

namespace ZiggyCreatures.Caching.Fusion.EventCounters.Tests
{
    
    public class Test
    {
        [Fact]
        public void SetCacheName()
        {
            using (var cache = new MemoryCache(new MemoryCacheOptions()))
            {
                var eventSource = FusionCacheEventSource.Instance("testCacheName", cache);
                Assert.Equal("testCacheName", eventSource.CacheName);
            }
        }
    }
}
