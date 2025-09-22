using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to cache entries' set.
/// </summary>
public class FusionCacheEntrySetEventArgs : FusionCacheEntryEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEntrySetEventArgs" /> class.
	/// </summary>
	/// <param name="key">The cache key related to the event.</param>
	/// <param name="metadata">The metadata related to the cache entry set (if any).</param>
	public FusionCacheEntrySetEventArgs(string key, FusionCacheEntryMetadata? metadata)
		: base(key)
	{
		Metadata = metadata;
	}

	/// <summary>
	/// The metadata related to the cache entry set (if any).
	/// </summary>
	public FusionCacheEntryMetadata? Metadata { get; }
}
