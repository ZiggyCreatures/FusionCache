namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// The provider to work with multiple named FusionCache instances, kinda like Microsoft's HTTP named clients (see https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests#named-clients)
	/// </summary>
	public interface IFusionCacheProvider
	{
		/// <summary>
		/// Returns the FusionCache instance with the corresponding name.
		/// </summary>
		/// <param name="cacheName">The name of the cache: it must match the one provided during registration.</param>
		/// <returns>The FusionCache instance corresponding to the cache name specified.</returns>
		IFusionCache GetCache(string cacheName);
	}
}
