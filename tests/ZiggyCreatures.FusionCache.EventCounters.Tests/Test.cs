using Xunit;

namespace ZiggyCreatures.FusionCache.EventCounters.Tests
{
    
    public class Test
    {
        [Fact]
        public void SetCacheName()
        {
            var eventSource = FusionCacheEventSource.Instance("testCacheName");
            Assert.Equal("testCacheName", eventSource.CacheName);
        }
    }
}
