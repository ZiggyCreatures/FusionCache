namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to cache entries' hits (eg: with a cache key and a stale flag).
/// </summary>
public class FusionCacheEntryHitEventArgs : FusionCacheEntryEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEntryHitEventArgs" /> class.
	/// </summary>
	/// <param name="key">The cache key related to the event.</param>
	/// <param name="isStale">A flag that indicates if the cache hit was for a fresh or stale entry.</param>
	public FusionCacheEntryHitEventArgs(string key, bool isStale)
		: base(key)
	{
		IsStale = isStale;
	}

	/// <summary>
	/// A flag that indicates if the cache hit was for a fresh or stale entry.
	/// </summary>
	public bool IsStale { get; }
}
