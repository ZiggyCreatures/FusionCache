using System;
using System.Diagnostics.Tracing;
using System.Threading;
using ZiggyCreatures.Caching.Fusion;

namespace ZiggyCreatures.FusionCache.EventCounters
{
    /// <summary>
    /// Generic FusionCacheEventSource.  
    /// </summary>
    public sealed partial class FusionCacheEventSource : EventSource, IFusionMetrics
    {
        /// <summary>
        /// Consumers access class from Instance.
        /// </summary>
        public static FusionCacheEventSource Instance(string cacheName) => new FusionCacheEventSource(cacheName);

        private long _cacheHits;
        private long _cacheMisses;
        private long _cacheStaleHit;
        private long _cacheBackgroundRefreshed;
        private long _cacheExpiredEvict;
        private long _cacheCapacityEvict;
        private long _cacheRemoved;
        private long _cacheReplaced;
        private long _cacheEvict;
        private long _cacheItemCount;
        
        private IncrementingPollingCounter? _cacheHitPollingCounter;
        private IncrementingPollingCounter? _cacheMissPollingCounter;
        private IncrementingPollingCounter? _cacheStaleHitPollingCounter;
        private IncrementingPollingCounter? _cacheBackgroundRefreshedPollingCounter;
        private IncrementingPollingCounter? _cacheExpiredEvictPollingCounter;
        private IncrementingPollingCounter? _cacheCapacityEvictPollingCounter;
        private IncrementingPollingCounter? _cacheRemovedPollingCounter;
        private IncrementingPollingCounter? _cacheReplacedPollingCounter;
        private IncrementingPollingCounter? _cacheEvictPollingCounter;
        private PollingCounter? _cacheSizePollingCounter;

        private readonly TimeSpan _displayRateTimeScale;

        private FusionCacheEventSource(string cacheName) : base(eventSourceName: cacheName)
        {
            _displayRateTimeScale = TimeSpan.FromSeconds(1);
            CacheName = cacheName;
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            _cacheHitPollingCounter = new IncrementingPollingCounter(
                Tags.CacheHit,
                this,
                () => Volatile.Read(ref _cacheHits))
            {
                DisplayName = "Cache Hits",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheHitPollingCounter.AddMetadata(Tags.CacheName, CacheName);


            _cacheMissPollingCounter = new IncrementingPollingCounter(
                Tags.CacheMiss,
                this,
                () => Volatile.Read(ref _cacheMisses))
            {
                DisplayName = "Cache Misses",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheMissPollingCounter.AddMetadata(Tags.CacheName, CacheName);
            

            _cacheStaleHitPollingCounter = new IncrementingPollingCounter(
                Tags.CacheStaleHit,
                this,
                () => Volatile.Read(ref _cacheStaleHit))
            {
                DisplayName = "Cache Stale Hit",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheStaleHitPollingCounter.AddMetadata(Tags.CacheName, CacheName);


            _cacheBackgroundRefreshedPollingCounter = new IncrementingPollingCounter(
                Tags.CacheBackgroundRefreshed,
                this,
                () => Volatile.Read(ref _cacheBackgroundRefreshed))
            {
                DisplayName = "Cache Background Refresh",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheBackgroundRefreshedPollingCounter.AddMetadata(Tags.CacheName, CacheName);

            
            _cacheExpiredEvictPollingCounter = new IncrementingPollingCounter(
                Tags.CacheExpiredEvict,
                this,
                () => Volatile.Read(ref _cacheExpiredEvict))
            {
                DisplayName = "Cache Expired Eviction",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheExpiredEvictPollingCounter.AddMetadata(Tags.CacheName, CacheName);

            
            _cacheCapacityEvictPollingCounter = new IncrementingPollingCounter(
                Tags.CacheCapacityEvict,
                this,
                () => Volatile.Read(ref _cacheCapacityEvict))
            {
                DisplayName = "Cache Capacity Eviction",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheCapacityEvictPollingCounter.AddMetadata(Tags.CacheName, CacheName);

            
            _cacheRemovedPollingCounter = new IncrementingPollingCounter(
                Tags.CacheRemoved,
                this,
                () => Volatile.Read(ref _cacheRemoved))
            {
                DisplayName = "Cache Removed",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheRemovedPollingCounter.AddMetadata(Tags.CacheName, CacheName);
            

            _cacheReplacedPollingCounter = new IncrementingPollingCounter(
                Tags.CacheReplaced,
                this,
                () => Volatile.Read(ref _cacheReplaced))
            {
                DisplayName = "Cache Replaced",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheReplacedPollingCounter.AddMetadata(Tags.CacheName, CacheName);

            
            _cacheEvictPollingCounter = new IncrementingPollingCounter(
                Tags.CacheEvict,
                this,
                () => Volatile.Read(ref _cacheEvict))
            {
                DisplayName = "Cache Evicted",
                DisplayRateTimeScale = _displayRateTimeScale
            };
            _cacheEvictPollingCounter.AddMetadata(Tags.CacheName, CacheName);

            
            _cacheSizePollingCounter = new PollingCounter(
                Tags.CacheItemCount,
                this,
                () => Volatile.Read(ref _cacheItemCount))
            {
                DisplayName = "Cache Size",
            };
            _cacheSizePollingCounter.AddMetadata(Tags.CacheName, CacheName);
        }

        /// <summary>
        /// Helper class to tag metrics
        /// </summary>
        public static class Tags
        {
            public const string CacheName = "cacheName";
            public const string CacheHit = "HIT";
            public const string CacheMiss = "MISS";
            public const string CacheStaleHit = "STALE_HIT";
            public const string CacheBackgroundRefreshed = "STALE_REFRESH";
            public const string CacheExpiredEvict = "EXPIRE";
            public const string CacheCapacityEvict = "CAPACITY";
            public const string CacheRemoved = "REMOVE";
            public const string CacheReplaced = "REPLACE";
            public const string CacheEvict = "EVICT";
            public const string CacheItemCount = "ITEM_COUNT";
        }

        #region IFusionMetrics
        /// <inheritdoc/>
        public string CacheName { get; }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheHit()
        {
            Interlocked.Increment(ref _cacheHits);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheMiss()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheStaleHit()
        {
            Interlocked.Increment(ref _cacheStaleHit);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheBackgroundRefresh()
        {
            Interlocked.Increment(ref _cacheBackgroundRefreshed);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheExpired()
        {
            Interlocked.Increment(ref _cacheExpiredEvict);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheCapacityExpired()
        {
            Interlocked.Increment(ref _cacheCapacityEvict);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheRemoved()
        {
            Interlocked.Increment(ref _cacheRemoved);
        }
        
        /// <inheritdoc/>
        [NonEvent]
        public void CacheReplaced()
        {
            Interlocked.Increment(ref _cacheReplaced);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheEvicted()
        {
            Interlocked.Increment(ref _cacheEvict);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheCountIncrement()
        {
            Interlocked.Increment(ref _cacheItemCount);
        }

        /// <inheritdoc/>
        [NonEvent]
        public void CacheCountDecrement()
        {
            Interlocked.Decrement(ref _cacheItemCount);
        }
        #endregion
        
    }
}
