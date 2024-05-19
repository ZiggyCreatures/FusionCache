using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IMemoryCache"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosMemoryCache
	: AbstractChaosComponent
	, IMemoryCache
{
	private readonly IMemoryCache _innerCache;

	/// <summary>
	/// Initializes a new instance of the ChaosMemoryCache class.
	/// </summary>
	/// <param name="innerCache">The actual <see cref="IMemoryCache"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosMemoryCache(IMemoryCache innerCache, ILogger<ChaosMemoryCache>? logger = null)
		: base(logger)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
	}

	/// <inheritdoc/>
	public ICacheEntry CreateEntry(object key)
	{
		MaybeChaos();
		return _innerCache.CreateEntry(key);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public void Remove(object key)
	{
		MaybeChaos();
		_innerCache.Remove(key);
	}

	/// <inheritdoc/>
	public bool TryGetValue(object key, out object? value)
	{
		MaybeChaos();
		return _innerCache.TryGetValue(key, out value);
	}
}
