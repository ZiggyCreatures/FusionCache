using System.Collections.Generic;

namespace ZiggyCreatures.Caching.Fusion;

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

	/// <summary>
	/// Returns the FusionCache instance with the corresponding name, or <see langword="null"/> if none found.
	/// </summary>
	/// <param name="cacheName">The name of the cache: it must match the one provided during registration.</param>
	/// <returns>The FusionCache instance corresponding to the cache name specified.</returns>
	IFusionCache? GetCacheOrNull(string cacheName);

	/// <summary>
	/// The collection of all available FusionCache names.
	/// </summary>
	IReadOnlyCollection<string> CacheNames { get; }
}
