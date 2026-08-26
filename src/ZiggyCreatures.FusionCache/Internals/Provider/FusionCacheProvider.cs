using System.Collections.Frozen;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider;

internal sealed class FusionCacheProvider
	: IFusionCacheProvider
{
	private readonly FrozenDictionary<string, LazyNamedCache?> _caches;
	private readonly ILogger<FusionCache>? _logger;

	public FusionCacheProvider(IEnumerable<IFusionCache> directCaches, IEnumerable<LazyNamedCache> lazyNamedCaches, ILogger<FusionCache>? logger = null)
	{
		_logger = logger;

		List<KeyValuePair<string, LazyNamedCache?>> items = [];
		foreach (var group in lazyNamedCaches.GroupBy(g => g.CacheName))
		{
			if (group.Count() == 1)
			{
				// ONLY 1 CACHE -> ADD IT
				items.Add(new(group.Key, group.First()));
			}
			else
			{
				// MORE THAN 1 CACHE -> ADD NULL
				// NOTE: THIS WILL SIGNAL THAT THERE WERE MULTIPLE ONES AND, SINCE
				// THEY WILL NOT BE ACCESSIBLE ANYWAY, WILL SAVE SOME MEMORY

				items.Add(new(group.Key, null));
			}
		}

		var directCachesCount = directCaches.Count();
		if (directCachesCount > 0)
		{
			// THE LAST ONE REGISTERED WILL BE THE ONE USED, FOLLOWING
			// THE STANDARD BEHAVIOR OF MICROSOFT'S DI CONTAINER.
			var directCache = directCaches.Last();
			items.Add(new(directCache.CacheName, new LazyNamedCache(directCache.CacheName, directCache)));
		}

		_caches = items.ToFrozenDictionary();
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
