using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.AspNetCore.OutputCaching;

/// <summary>
/// Options for configuring OutputCache with FusionCache.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class FusionOutputCacheOptions
{
	/// <summary>
	/// The name of the cache to use for output caching: if left to <see langword="null"/>, the default cache (with a name equal to <see cref="FusionCacheOptions.DefaultCacheName"/>) will be used.
	/// </summary>
	public string? CacheName { get; set; }

	private string GetDebuggerDisplay()
	{
		return $"OutputCache Name={CacheName}";
	}
}
