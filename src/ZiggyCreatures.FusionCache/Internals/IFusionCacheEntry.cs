using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// Represents a generic entry in <see cref="FusionCache"/>: it can be either a <see cref="FusionCacheMemoryEntry{TValue}"/> or a <see cref="FusionCacheDistributedEntry{TValue}"/>.
/// </summary>
internal interface IFusionCacheEntry
{
	// NOTE: GetValue<TValue>() AND SetValue<TValue(value) ARE NEEDED TO SUPPORT
	// FLEXIBLE TYPE CONVERSIONS, MEANING THAT WE CAN SET, FOR EXAMPLE, A Person
	// VALUE IN THE CACHE AS object AND THEN GET IT BACK AS A Person AGAIN.
	// THIS WOULDN'T WORK WITHOUT THESE METHODS, BECAUSE CASTING AN INSTANCE OF
	// IFusionCacheEntry<object> TO IFusionCacheEntry<Person> WOULD NOT WORK.

	/// <summary>
	/// Get the value inside the entry.
	/// </summary>
	/// <typeparam name="TValue">The typeof the value.</typeparam>
	/// <returns>The value.</returns>
	TValue GetValue<TValue>();

	/// <summary>
	/// Set the value inside the entry.
	/// </summary>
	/// <typeparam name="TValue">The typeof the value.</typeparam>
	/// <param name="value">The value.</param>
	void SetValue<TValue>(TValue value);


	/// <summary>
	/// The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.
	/// </summary>
	string[]? Tags { get; set; }

	/// <summary>
	/// Metadata about the cache entry.
	/// </summary>
	FusionCacheEntryMetadata? Metadata { get; }

	/// <summary>
	/// The timestamp (in ticks) at which the cached value has been originally created: memory cache entries created from distributed cache entries will have the exact same timestamp.
	/// </summary>
	public long Timestamp { get; }
}
