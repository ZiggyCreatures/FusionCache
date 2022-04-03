using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	/// <summary>
	/// Represents an generic entry in a <see cref="FusionCache"/>, which can be either a <see cref="FusionCacheMemoryEntry"/> or a <see cref="FusionCacheDistributedEntry{TValue}"/>.
	/// </summary>
	public interface IFusionCacheEntry
	{
		/// <summary>
		/// Get the value inside the entry.
		/// </summary>
		/// <typeparam name="TValue">The typeof the value.</typeparam>
		/// <returns>The value.</returns>
		TValue? GetValue<TValue>();

		/// <summary>
		/// Set the value inside the entry.
		/// </summary>
		/// <typeparam name="TValue">The typeof the value.</typeparam>
		/// <param name="value">The value.</param>
		void SetValue<TValue>(TValue? value);

		/// <summary>
		/// Metadata about the cache entry.
		/// </summary>
		FusionCacheEntryMetadata? Metadata { get; }
	}
}
