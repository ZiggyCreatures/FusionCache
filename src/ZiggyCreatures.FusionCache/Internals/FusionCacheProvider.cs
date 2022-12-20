using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion.Internals
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
			try
			{
				var cache = _caches.Single(x => x.CacheName == cacheName);

				if (cache is null)
					throw new ArgumentException($"No FusionCache registration has been found with the provided name ({cacheName})", nameof(cacheName));

				return cache;
			}
			catch (InvalidOperationException exc)
			{
				throw new ArgumentException($"No FusionCache registration has been found with the provided name ({cacheName})", nameof(cacheName), exc);
			}
		}
	}
}
