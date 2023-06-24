using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider
{
	internal class FusionCacheProvider
		: IFusionCacheProvider
	{
		private readonly IFusionCache? _defaultCache;
		private readonly NamedCacheWrapper[] _namedCacheWrappers;

		public FusionCacheProvider(IEnumerable<IFusionCache> defaultCaches, IEnumerable<NamedCacheWrapper> namedCaches)
		{
			_defaultCache = defaultCaches.LastOrDefault();
			_namedCacheWrappers = namedCaches.ToArray();
		}

		public IFusionCache? GetCacheOrNull(string cacheName)
		{
			if (cacheName == FusionCacheOptions.DefaultCacheName)
				return _defaultCache;

			var matchingWrappers = _namedCacheWrappers.Where(x => x.CacheName == cacheName).ToArray();

			if (matchingWrappers.Length == 1)
				return matchingWrappers[0].Cache;

			if (matchingWrappers.Length > 1)
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
				: $"No cache has been registered with name ({cacheName})"
			);
		}
	}
}
