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
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.Log(LogLevel.Warning, "FUSION: multiple FusionCache registrations detected with cache name {CacheName}. This should be avoided as it will lead to surprises.", group.Key);

				items.Add(new(group.Key, null));
			}
		}

		var directCachesCount = directCaches.Count();
		if (directCachesCount > 0)
		{
			var nonDefaultDirectCacheNames = directCaches.Where(x => x.CacheName != FusionCacheOptions.DefaultCacheName).Select(x => x.CacheName).Distinct().ToArray();
			if (nonDefaultDirectCacheNames.Length > 0)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.Log(LogLevel.Warning, "FUSION: one or more direct IFusionCache registrations have been detected with a cache name which is not the default one: {CacheNames}.", string.Join(", ", nonDefaultDirectCacheNames));
			}

			if (directCachesCount > 1)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.Log(LogLevel.Warning, "FUSION: multiple direct IFusionCache registrations have been detected. The last one will be used, as per Microsoft standard DI implementation, but it should be avoided when possible to prevent bad surprises.");
			}

			// THE LAST ONE REGISTERED WILL BE THE ONE USED, FOLLOWING
			// THE STANDARD BEHAVIOR OF MICROSOFT'S DI CONTAINER.
			var directCache = directCaches.Last();

			if (directCache.CacheName != FusionCacheOptions.DefaultCacheName)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.Log(LogLevel.Warning, "FUSION: the direct IFusionCache registration that will be used has a cache name ({CacheName}) that is different from the default one. This should be avoided when possible to prevent bad surprises.", directCache.CacheName);
			}

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
