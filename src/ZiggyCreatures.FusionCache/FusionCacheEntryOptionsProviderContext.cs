namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The context passed to the <see cref="FusionCacheOptions.DefaultEntryOptionsProvider"/> to get a default <see cref="FusionCacheEntryOptions"/> based on the cache key.
/// </summary>
public class FusionCacheEntryOptionsProviderContext
{
	private readonly IFusionCache _cache;

	/// <summary>
	/// Creates a new <see cref="FusionCacheEntryOptionsProviderContext"/> instance.
	/// </summary>
	/// <param name="cache">A reference to the related <see cref="IFusionCache"/> instance.</param>
	internal FusionCacheEntryOptionsProviderContext(IFusionCache cache)
	{
		_cache = cache;
	}

	/// <summary>
	/// Duplicates the <see cref="FusionCacheOptions.DefaultEntryOptions"/> to allow subsequent customizations.
	/// </summary>
	/// <returns>A <see cref="FusionCacheEntryOptions"/> instante.</returns>
	public FusionCacheEntryOptions DuplicateDefaultEntryOptions()
	{
		return _cache.DefaultEntryOptions.Duplicate();
	}
}
