using Microsoft.Extensions.Caching.Memory;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to cache entries' evictions.
/// </summary>
public class FusionCacheEntryEvictionEventArgs
	: FusionCacheEntryEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEntryEvictionEventArgs"/> class.
	/// </summary>
	/// <param name="key">The cache key related to the event.</param>
	/// <param name="reason">The reason for the eviction.</param>
	/// <param name="value">The value being evicted from the cache.</param>
	public FusionCacheEntryEvictionEventArgs(string key, EvictionReason reason, object? value)
		: this(key, reason, value, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEntryEvictionEventArgs"/> class.
	/// </summary>
	/// <param name="key">The cache key related to the event.</param>
	/// <param name="reason">The reason for the eviction.</param>
	/// <param name="value">The value being evicted from the cache.</param>
	/// <param name="metadata">The metadata related to the cache entry evicted (if any).</param>
	public FusionCacheEntryEvictionEventArgs(string key, EvictionReason reason, object? value, FusionCacheEntryMetadata? metadata)
		: base(key)
	{
		Reason = reason;
		Value = value;
		Metadata = metadata;
	}

	/// <summary>
	/// The reason for the eviction.
	/// </summary>
	public EvictionReason Reason { get; }

	/// <summary>
	/// The value being evicted from the cache.
	/// </summary>
	public object? Value { get; }

	/// <summary>
	/// The metadata related to the cache entry evicted (if any).
	/// </summary>
	public FusionCacheEntryMetadata? Metadata { get; }
}
