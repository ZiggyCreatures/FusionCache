using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider;

internal sealed class FusionCacheProvider
	: IFusionCacheProvider
{
	private readonly FrozenDictionary<string, LazyNamedCache?> _caches;

	public FusionCacheProvider(IEnumerable<IFusionCache> directCaches, IEnumerable<LazyNamedCache> lazyNamedCaches)
	{
		List<KeyValuePair<string, LazyNamedCache?>> caches = [];
		foreach (var group in lazyNamedCaches.GroupBy(g => g.CacheName))
		{
			if (group.Count() == 1)
			{
				// ONLY 1 CACHE -> ADD IT
				caches.Add(new(group.Key, group.First()));
			}
			else
			{
				// MORE THAN 1 CACHE -> ADD NULL
				// NOTE: THIS WILL SIGNAL THAT THERE WERE MULTIPLE ONES AND, SINCE
				// THEY WILL NOT BE ACCESSIBLE ANYWAY, WILL SAVE SOME MEMORY
				caches.Add(new(group.Key, null));
			}
		}

		var defaultCache = directCaches.LastOrDefault();
		if (defaultCache is not null)
		{
			caches.Add(new(defaultCache.CacheName, new LazyNamedCache(defaultCache.CacheName, defaultCache)));
		}

		_caches = caches.ToFrozenDictionary();
	}

	public IFusionCache? GetCacheOrNull(string cacheName)
	{
		if (_caches.TryGetValue(cacheName, out var item) == false)
			return null;

		if (item is null)
			throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({cacheName})");

		return item.Cache;
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
