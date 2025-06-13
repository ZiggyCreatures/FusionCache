﻿using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// A set of extension methods that add some commonly used overloads to any instance of a <see cref="IFusionCache"/> instance and other common objects.
/// </summary>
public static partial class FusionCacheExtMethods
{
	#region Dependency Injection

	/// <summary>
	/// Returns the default FusionCache instance, the one with the CacheName equals to <see cref="FusionCacheOptions.DefaultCacheName"/>.
	/// </summary>
	/// <returns>The default FusionCache instance.</returns>
	public static IFusionCache GetDefaultCache(this IFusionCacheProvider cacheProvider)
	{
		return cacheProvider.GetCache(FusionCacheOptions.DefaultCacheName);
	}

	/// <summary>
	/// Returns the default FusionCache instance, the one with the CacheName equals to <see cref="FusionCacheOptions.DefaultCacheName"/>, or <see langword="null"/> if none found.
	/// </summary>
	/// <returns>The default FusionCache instance.</returns>
	public static IFusionCache? GetDefaultCacheOrNull(this IFusionCacheProvider cacheProvider)
	{
		return cacheProvider.GetCacheOrNull(FusionCacheOptions.DefaultCacheName);
	}

	#endregion

	/// <inheritdoc/>
	public static FusionCacheEntryOptions CreateEntryOptions(this IFusionCache cache, string key, Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
	{
		var res = cache.GetOrCreateDefaultEntryOptions(key, true);

		if (duration is not null)
			res.Duration = duration.Value;

		setupAction?.Invoke(res);

		return res;
	}

}
