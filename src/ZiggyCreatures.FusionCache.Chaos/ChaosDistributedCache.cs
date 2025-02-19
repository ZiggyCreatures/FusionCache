using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IDistributedCache"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosDistributedCache
	: AbstractChaosComponent
	, IDistributedCache
{
	private readonly IDistributedCache _innerCache;

	/// <summary>
	/// Initializes a new instance of the ChaosDistributedCache class.
	/// </summary>
	/// <param name="innerCache">The actual <see cref="IDistributedCache"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosDistributedCache(IDistributedCache innerCache, ILogger<ChaosDistributedCache>? logger = null)
		: base(logger)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
	}

	/// <inheritdoc/>
	public byte[]? Get(string key)
	{
		MaybeChaos();
		return _innerCache.Get(key);
	}

	/// <inheritdoc/>
	public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		return await _innerCache.GetAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Refresh(string key)
	{
		MaybeChaos();
		_innerCache.Refresh(key);
	}

	/// <inheritdoc/>
	public async Task RefreshAsync(string key, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		await _innerCache.RefreshAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		MaybeChaos();
		_innerCache.Remove(key);
	}

	/// <inheritdoc/>
	public async Task RemoveAsync(string key, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		await _innerCache.RemoveAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		MaybeChaos();
		_innerCache.Set(key, value, options);
	}

	/// <inheritdoc/>
	public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		await _innerCache.SetAsync(key, value, options, token).ConfigureAwait(false);
	}
}
