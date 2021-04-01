<div align="center">

![FusionCache logo](../../docs/logo-256x256.png)

</div>

# FusionCache.EventCounters

<div align="center">

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=flat&logo=twitter)](https://twitter.com/intent/tweet?hashtags=fusioncache,caching,cache,dotnet,oss,csharp&text=🚀+FusionCache:+a+new+cache+with+an+optional+2nd+layer+and+some+advanced+features&url=https%3A%2F%2Fgithub.com%2Fjodydonetti%2FZiggyCreatures.FusionCache&via=jodydonetti)

</div>

### FusionCache.EventCounters is a plugin to capture caching metrics using [FusionCache](https://github.com/jodydonetti/ZiggyCreatures.FusionCache).

Metrics are missing from open-source resiliency projects in the .NET ecosystem where in equivalent Java libraries, metrics tend to be common.  FusionCache is a feature rich caching library addressing resiliency needs of today’s enterprise implementations.  [EventCounters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters) is a lightweight .NET Core API library that works in .NET Core.  Joining these two excellent libraries together you can easily be caching and writing metrics to your favorite timeseries database or use the dotnet-counters tool to monitor from the console.

Metrics plugins are created by implementing the IFusionMetrics interface from [FusionCache](https://github.com/jodydonetti/ZiggyCreatures.FusionCache).


The following IFusionMetrics are implemented along with Tag names.  Tags are the typical in time series databases and are indexed making them friendly to searching and grouping over time.  

### CacheName

"cacheName" is a Tag.  Set this when creating a AppMetricsProvider.

Example usage where domainsCache is the name of the cache:

```csharp
var domainMetrics = FusionCacheEventSource.Instance("domainsCache");
```

## Incrementing Polling Counters for Hits and Misses

The following counters are all IncrementingPollingCounters which tracks based on a time interval. EventListeners will get a value based on the difference between the current invocation and the last invocation. Read [EventCounter API overview](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters#eventcounter-api-overview) to get a better understanding of the various EventCounter implementations.

### CacheHit()

The cache tag is "HIT".  Every cache hit will increment a counter.

### CacheMiss()

The cache tag is "MISS".  Every cache miss will increment a counter.

### CacheStaleHit()

The cache tag is "STALE_HIT".  When [FailSafe](https://github.com/jodydonetti/ZiggyCreatures.FusionCache/blob/main/docs/Timeouts.md) is enabled and a request times out due to a "soft timeout" and a stale cache item exists then increment a counter.  Note this will not trigger the CacheMiss() counter.  

### CacheBackgroundRefresh()

The cache tag is "STALE_REFRESH".  When [FailSafe](https://github.com/jodydonetti/ZiggyCreatures.FusionCache/blob/main/docs/Timeouts.md) is enabled and a request times out due to a "soft timeout" the request will continue for the length of a "hard timeout".  If the request finds data it will call this CacheBackgroundRefresh() and increment a counter.  Note it would be normal for this counter and CacheStaleHit() to track with each other.

## Incrementing Polling Counter for Evictions

Eviction counters are wired into the ICacheEntries with the PostEvictionDelegate.  

### CacheExpired

The cache tag is "EXPIRE".  When the EvictionReason is Expired increment a counter.

### CacheCapacityExpired()

The cache tag is "CAPACITY".  When the EvictionReason is Capacity increment a counter.

### CacheRemoved()

The cache tag is "REMOVE".  When the EvictionReason is Replaced increment a counter.

### CacheEvicted()

The cache tag is "EVICT".  When the EvictionReason is non of the previous options increment a counter.

## Polling Counters

The following counters are all PollingCounters which track the a shared accumulator between the following two counters.

### CacheCountIncrement()

The cach tag is "ITEM_COUNT". Every CacheMiss() call will also increment the item count counter.

### CacheCountDecrement()

The cach tag is "ITEM_COUNT". Every Eviction will also decrement the item count counter.


## Usage

```csharp
    var domainMetrics = FusionCacheEventSource.Instance("domainCache");

    services.AddFusionCache(options =>
        {
            options.DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromSeconds(5),
                Priority = CacheItemPriority.High
            }
                .SetFailSafe(true, TimeSpan.FromHours(2))
                .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
        },
        metrics: domainMetrics);
```

### Reporting

dotnet-counters can listen to the metrics above.
Example command line for a example.exe application

```cmd
dotnet-counters monitor -n example --counters domainCache
```

Example output would look like the following
[domainCache]
    Cache Background Refresh (Count / 1 sec)           0
    Cache Capacity Eviction (Count / 1 sec)            0
    Cache Evicted (Count / 1 sec)                      0
    Cache Expired Eviction (Count / 1 sec)             5
    Cache Hits (Count / 1 sec)                       221
    Cache Misses (Count / 1 sec)                       0
    Cache Removed (Count / 1 sec)                      0
    Cache Replaced (Count / 1 sec)                     5
    Cache Size                                     1,157
    Cache Stale Hit (Count / 1 sec)                    5

To make reporting seemless implent a [EventListener](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters#sample-code)  and run it as a HostedService.

```csharep
Needs work.  I have an implementation to write to InfluxDb but it is not shareable yet.  Might work of a Prometheus sample.        
```
