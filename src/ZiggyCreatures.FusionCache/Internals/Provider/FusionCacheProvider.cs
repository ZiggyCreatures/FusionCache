using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider;

internal sealed class FusionCacheProvider
	: IFusionCacheProvider
{
	private readonly IFusionCache? _defaultCache;
	private readonly LazyNamedCache[] _lazyNamedCaches;

	public FusionCacheProvider(IEnumerable<IFusionCache> defaultCaches, IEnumerable<LazyNamedCache> lazyNamedCaches)
	{
		_defaultCache = defaultCaches.LastOrDefault();
		_lazyNamedCaches = lazyNamedCaches.ToArray();
	}

	public IFusionCache? GetCacheOrNull(string cacheName)
	{
		if (cacheName == FusionCacheOptions.DefaultCacheName)
			return _defaultCache;

		var matchingLazyNamedCaches = _lazyNamedCaches.Where(x => x.CacheName == cacheName).ToArray();

		if (matchingLazyNamedCaches.Length == 1)
			return matchingLazyNamedCaches[0].Cache;

		if (matchingLazyNamedCaches.Length > 1)
			throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({cacheName})");

		return null;
	}

	public IFusionCache GetCache(string cacheName)
	{
		var maybeCache = GetCacheOrNull(cacheName);

		if (maybeCache is not null)
			return maybeCache;

		throw new InvalidOperationException(
			cacheName == FusionCacheOptions.DefaultCacheName
			? "No default cache has been registered"
			: $"No cache has been registered with name ({cacheName}): make sure you registered it with the AddFusionCache(\"{cacheName}\") method."
		);
	}
}
