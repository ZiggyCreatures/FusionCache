using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider;

internal sealed class FusionCacheProvider
	: IFusionCacheProvider
{
	private readonly Dictionary<string, Lazy<IFusionCache>> _caches;

	public FusionCacheProvider(IEnumerable<IFusionCache> defaultCaches, IEnumerable<LazyNamedCache> lazyNamedCaches)
	{
		_caches = new Dictionary<string, Lazy<IFusionCache>>();
		foreach (var group in lazyNamedCaches.GroupBy(g => g.CacheName))
		{
			_caches.Add(group.Key, new Lazy<IFusionCache>(() => group.Count() == 1 ? group.First().Cache : throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({group.Key})")));
		}

		var defaultCache = defaultCaches.LastOrDefault();
		if (defaultCache != null)
		{
			_caches.Add(FusionCacheOptions.DefaultCacheName, new Lazy<IFusionCache>(() => defaultCache));
		}
	}

	public IFusionCache? GetCacheOrNull(string cacheName)
	{
		return _caches.TryGetValue(cacheName, out var cache) ? cache.Value : null;
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
