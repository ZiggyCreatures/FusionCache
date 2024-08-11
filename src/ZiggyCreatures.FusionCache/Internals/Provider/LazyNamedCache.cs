using System;
using System.Diagnostics;
using System.Threading;

namespace ZiggyCreatures.Caching.Fusion.Internals.Provider;

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

	private readonly Lock _mutex = new Lock();
	private IFusionCache? _cache;
	private readonly Func<IFusionCache>? _cacheFactory;

	public string CacheName { get; }

	public IFusionCache Cache
	{
		get
		{
			if (_cache is not null)
				return _cache;

			lock (_mutex)
			{
				if (_cache is not null)
					return _cache;

				if (_cacheFactory is null)
					throw new InvalidOperationException("No cache and no cache factory specified: this should not be possible.");

				return _cache = _cacheFactory();
			}
		}
	}

	public void Dispose()
	{
		_cache?.Dispose();
	}
}
