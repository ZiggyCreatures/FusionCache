namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// Common interface to provide <see cref="FusionCacheEntryOptions"/> based on a key.
	/// </summary>
	public interface IKeyedFusionCacheEntryOptionsProvider
	{
		/// <summary>
		/// Gets the <see cref="FusionCacheEntryOptions"/> for the specified <paramref name="key"/>.
		/// </summary>
		/// <param name="key">The actual cache key.</param>
		/// <returns><see cref="FusionCacheEntryOptions"/> for the specified <paramref name="key"/> or null if no match is found.</returns>
		FusionCacheEntryOptions? GetEntryOptions(string key);
	}
}
