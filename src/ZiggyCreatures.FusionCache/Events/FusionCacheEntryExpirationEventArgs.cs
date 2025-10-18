using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to cache entries' expirations.
/// </summary>
public class FusionCacheEntryExpirationEventArgs : FusionCacheEntryEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEntryExpirationEventArgs" /> class.
	/// </summary>
	/// <param name="key">The cache key related to the event.</param>
	/// <param name="metadata">The metadata related to the cache entry expired (if any).</param>
	public FusionCacheEntryExpirationEventArgs(string key, FusionCacheEntryMetadata? metadata)
		: base(key)
	{
		Metadata = metadata;
	}

	/// <summary>
	/// The metadata related to the cache entry expired (if any).
	/// </summary>
	public FusionCacheEntryMetadata? Metadata { get; }
}
