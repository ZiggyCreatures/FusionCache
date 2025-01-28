namespace ZiggyCreatures.Caching.Fusion.NullObjects
{
	/// <summary>
	/// An implementation of <see cref="IKeyedFusionCacheEntryOptionsProvider"/> that implements the null object pattern, meaning that it does nothing, always returning default value of null.
	/// </summary>
	public class NullKeyedEntryOptionsProvider : IKeyedFusionCacheEntryOptionsProvider
	{
		/// <inheritdoc/>
		public FusionCacheEntryOptions? GetEntryOptions(string key)
		{
			return null;
		}
	}
}
