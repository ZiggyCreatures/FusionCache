using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider
{
	internal class FusionCacheProvider
		: IFusionCacheProvider
	{
		private readonly IEnumerable<IFusionCache> _caches;

		public FusionCacheProvider(IEnumerable<IFusionCache> caches)
		{
			_caches = caches;
		}

		public IFusionCache GetCache(string cacheName)
		{
			var matchingCaches = _caches.Where(x => x.CacheName == cacheName).ToArray();

			if (matchingCaches.Length == 1)
				return matchingCaches[0];

			if (matchingCaches.Length > 1)
				throw new InvalidOperationException($"Multiple FusionCache registrations have been found with the provided name ({cacheName})");

			throw new ArgumentException($"No FusionCache registration has been found with the provided name ({cacheName})", nameof(cacheName));
		}
	}
}
