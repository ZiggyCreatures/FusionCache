using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider
{
	internal class FusionCacheProvider
		: IFusionCacheProvider
	{
		private readonly IFusionCache? _defaultCache;
		private readonly IFusionCache[] _namedCaches;

		public FusionCacheProvider(IEnumerable<IFusionCache> defaultCaches, IEnumerable<NamedCacheWrapper> namedCaches)
		{
			_defaultCache = defaultCaches.LastOrDefault();
			_namedCaches = namedCaches.Select(x => x.Cache).ToArray();
		}

		public IFusionCache GetCache(string cacheName)
		{
			if (cacheName == FusionCacheOptions.DefaultCacheName)
				return _defaultCache ?? throw new InvalidOperationException("No default cache has been registered");

			var matchingCaches = _namedCaches.Where(x => x.CacheName == cacheName).ToArray();

			if (matchingCaches.Length == 1)
				return matchingCaches[0];

			if (matchingCaches.Length > 1)
				throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({cacheName})");

			throw new ArgumentException($"No FusionCache registration has been found with the provided name ({cacheName})", nameof(cacheName));
		}

		public IFusionCache? GetCacheOrNull(string cacheName)
		{
			if (cacheName == FusionCacheOptions.DefaultCacheName)
				return _defaultCache;

			var matchingCaches = _namedCaches.Where(x => x.CacheName == cacheName).ToArray();

			if (matchingCaches.Length == 1)
				return matchingCaches[0];

			if (matchingCaches.Length > 1)
				throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({cacheName})");

			return null;
		}
	}
}
