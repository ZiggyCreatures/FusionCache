using System;
using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider
{
	[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
	internal sealed class LazyNamedCache : IDisposable
	{
		private string GetDebuggerDisplay()
		{
			return $"CACHE: {CacheName} - INSTANTIATED: {_cache is not null}";
		}

		public LazyNamedCache(string name, Func<IFusionCache> cacheFactory)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			if (cacheFactory is null)
				throw new ArgumentNullException(nameof(cacheFactory));

			CacheName = name;
			_cacheFactory = cacheFactory;
		}

		public LazyNamedCache(string name, IFusionCache cache)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			if (cache is null)
				throw new ArgumentNullException(nameof(cache));

			CacheName = name;
			_cache = cache;
		}

		private readonly object _mutex = new object();
		private IFusionCache? _cache;
		private readonly Func<IFusionCache>? _cacheFactory;

		public string CacheName { get; }

		public IFusionCache Cache
		{
			get
			{
				if (_cache is not null)
					return _cache;

				if (_cacheFactory is not null)
				{
					lock (_mutex)
					{
						return _cache = _cacheFactory();
					}
				}

				throw new InvalidOperationException("This should not be possible");
			}
		}

		public void Dispose()
		{
			_cache?.Dispose();
		}
	}
}
