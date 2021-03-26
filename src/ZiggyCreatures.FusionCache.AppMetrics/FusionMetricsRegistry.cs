using App.Metrics;
using App.Metrics.Counter;

namespace ZiggyCreatures.FusionCache.AppMetrics
{
    /// <summary>
    /// Define FusionCache metrics
    /// </summary>
    public class FusionMetricsRegistry
    {
        // In time series database the MetricsOptions.DefaultContextLabel will be prefixed to the MeasurementName
        private static string MeasurementName = "cache-events";

        /// <summary>
        /// Cache hit counter
        /// </summary>
        public static CounterOptions CacheHitCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "HIT"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache miss counter.  When a cache is written to local cache
        /// </summary>
        public static CounterOptions CacheMissCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "MISS"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache stale hit counter.  Cache failed to complete within soft timeout period. 
        /// </summary>
        public static CounterOptions CacheStaleHitCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "STALE_HIT"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache refresh in background.
        /// </summary>
        public static CounterOptions CacheBackgroundRefreshed => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "STALE_REFRESH"),
            ResetOnReporting = true
        };


        /// <summary>
        /// Cache expired counter
        /// </summary>
        public static CounterOptions CacheExpireCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "EXPIRE"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache eviction from capacity limit
        /// </summary>
        public static CounterOptions CacheCapacityCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "CAPACITY"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache item removed counter
        /// </summary>
        public static CounterOptions CacheRemoveCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "REMOVE"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache item replaced.  Happens from either user code or a background refresh.
        /// </summary>
        public static CounterOptions CacheReplaceCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "REPLACE"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache item evicted for unknown reason.
        /// </summary>
        public static CounterOptions CacheEvictCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "EVICT"),
            ResetOnReporting = true
        };

        /// <summary>
        /// Cache item count.  Tracked by add and remove counters. 
        /// </summary>
        public static CounterOptions CacheItemCounter => new CounterOptions
        {
            Name = MeasurementName,
            Tags = new MetricTags("cacheEvent", "ITEM_COUNT"),
            ResetOnReporting = false,
        };
    }
}
