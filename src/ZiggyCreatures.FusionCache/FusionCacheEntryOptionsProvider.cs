namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// A provider to get <see cref="FusionCacheEntryOptions"/> based on a key.
/// <br/><br/>
/// ⚠️ <strong>IMPORTANT:</strong> in your GetEntryOptions() implementation carefully set the canMutate out param to indicate if the returned object can be mutated or not.
/// </summary>
public abstract class FusionCacheEntryOptionsProvider
{
	/// <summary>
	/// Provide entry options based on a key, by either returning a new instance or a reference to an existing one (for improved performance).
	/// <br/><br/>
	/// ⚠️ <strong>IMPORTANT:</strong> carefully set the <paramref name="canMutate"/> out param to indicate if the returned object can be mutated or not.
	/// </summary>
	/// <param name="ctx">The context, containing supporting features.</param>
	/// <param name="key">The cache key.</param>
	/// <param name="canMutate">An out parameter that indicate if the returned object can be mutated.</param>
	/// <returns>The entry options.</returns>
	public abstract FusionCacheEntryOptions? GetEntryOptions(FusionCacheEntryOptionsProviderContext ctx, string key, out bool canMutate);
}
