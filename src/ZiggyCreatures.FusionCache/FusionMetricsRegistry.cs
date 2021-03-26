// using App.Metrics;
// using App.Metrics.Counter;
//
// namespace ZiggyCreatures.Caching.Fusion
// {
//     /// <summary>
//     /// Define FusionCache metrics
//     /// </summary>
//     public class FusionMetricsRegistry
//     {
//         // In time series database the MetricsOptions.DefaultContextLabel will be prefixed to the MeasurementName
//         private static string MeasurementName = "cache-events";
//
//         /// <summary>
//         /// Cache hit counter
//         /// </summary>
//         public static CounterOptions CacheHitCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "HIT"),
//             ResetOnReporting = true
//         };
//
//         /// <summary>
//         /// Cache miss counter.  When a cache is written to local cache
//         /// </summary>
//         public static CounterOptions CacheMissCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "MISS"),
//             ResetOnReporting = true
//         };
//
//         /// <summary>
//         /// Cache stale hit counter.  Cache failed to complete within soft timeout period. 
//         /// </summary>
//         public static CounterOptions CacheStaleHitCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "STALE_HIT"),
//             ResetOnReporting = true
//         };
//
//         /// <summary>
//         /// Cache refresh in background.
//         /// </summary>
//         public static CounterOptions CacheBackGroundRefreshed => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "STALE_REFRESH"),
//             ResetOnReporting = true
//         };
//
//         
//         public static CounterOptions CacheExpireCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "EXPIRE"),
//             ResetOnReporting = true
//         };
//
//         public static CounterOptions CacheCapacityCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "CAPACITY"),
//             ResetOnReporting = true
//         };
//
//         public static CounterOptions CacheRemoveCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "REMOVE"),
//             ResetOnReporting = true
//         };
//
//         public static CounterOptions CacheReplaceCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "REPLACE"),
//             ResetOnReporting = true
//         };
//         
//         public static CounterOptions CacheEvictCounter => new CounterOptions
//         {
//             Name = MeasurementName,
//             Tags = new MetricTags("cacheEvent", "EVICT"),
//             ResetOnReporting = true
//         };
//         
//         public static CounterOptions CacheSizeCounter => new CounterOptions
//         {
//             Name = "cache-size",
//             ResetOnReporting = false,
//         };
//     }
// }
