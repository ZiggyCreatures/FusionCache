using App.Metrics;
using ZiggyCreatures.Caching.Fusion;

namespace ZiggyCreatures.FusionCache.AppMetrics
{
    /// <summary>
    /// AppMetrics implementation of IFusionMetrics provider
    /// </summary>
    public class AppMetricsProvider : IFusionMetrics
    {
        private IMetrics _metrics;
        private MetricTags _cacheNameMetricTag;
        private string _cacheName;
        
        /// <summary>
        /// Instantiate AppMetricsProvider
        /// </summary>
        /// <param name="metrics">App.Metrics IMetric instance</param>
        /// <param name="cacheName">Used to capture metrics tagged by cacheName</param>
        public AppMetricsProvider(IMetrics metrics, string cacheName)
        {
            _metrics = metrics;
            _cacheName = cacheName;
            _cacheNameMetricTag = new MetricTags("cacheName", cacheName);
        }

        /// <inheritdoc/>
        public string CacheName
        {
            get
            {
                return _cacheName;
            }
        }

        /// <inheritdoc/>
        public void CacheHit()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheHitCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheMiss()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheMissCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheStaleHit()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheStaleHitCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheBackgroundRefresh()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheBackgroundRefreshed, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheExpired()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheExpireCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheCapacityExpired()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheCapacityCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheRemoved()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheRemoveCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheReplaced()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheReplaceCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheEvicted()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheEvictCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheCountIncrement()
        {
            _metrics.Measure.Counter.Increment(FusionMetricsRegistry.CacheItemCounter, _cacheNameMetricTag);
        }

        /// <inheritdoc/>
        public void CacheCountDecrement()
        {
            _metrics.Measure.Counter.Decrement(FusionMetricsRegistry.CacheItemCounter, _cacheNameMetricTag);
        }
    }
}
