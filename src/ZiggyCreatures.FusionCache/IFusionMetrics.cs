using System;

namespace ZiggyCreatures.Caching.Fusion
{
    /// <summary>
    /// Represents a provider instance.
    /// </summary>
    public interface IFusionMetrics 
    {
        /// <summary>
        /// Cache item hit counter.
        /// </summary>
        public void CacheHit();

        /// <summary>
        /// Cache item miss counter.  When a cache item is written to local cache
        /// </summary>
        public void CacheMiss();

        /// <summary>
        /// Cache item stale hit counter.  Cache item failed to complete within soft timeout period. 
        /// </summary>
        public void CacheStaleHit();

        /// <summary>
        /// Cache item refresh in background.
        /// </summary>
        public void CacheBackgroundRefresh();

        /// <summary>
        /// Cache item expired
        /// </summary>
        public void CacheExpired();

        /// <summary>
        /// Cache item removed due to capacity
        /// </summary>
        public void CacheCapacityExpired();

        /// <summary>
        /// Cache item explicitly removed by user code
        /// </summary>
        public void CacheRemoved();

        /// <summary>
        /// Cache item removed by user code or due to a background refresh
        /// </summary>
        public void CacheReplaced();
        
        /// <summary>
        /// Cache item removed for unknown reason.
        /// </summary>
        public void CacheEvicted();
        
    }
}
