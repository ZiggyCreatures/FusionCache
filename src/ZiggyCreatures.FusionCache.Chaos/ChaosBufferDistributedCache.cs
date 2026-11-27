using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IBufferDistributedCache"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// <br/><br/>
/// Use this instead of <see cref="ChaosDistributedCache"/> when the inner cache implements <see cref="IBufferDistributedCache"/>, so that FusionCache keeps using the buffered distributed cache path (which a plain <see cref="ChaosDistributedCache"/> would hide).
/// </summary>
public class ChaosBufferDistributedCache
	: ChaosDistributedCache
	, IBufferDistributedCache
{
	private readonly IBufferDistributedCache _innerCache;

	/// <summary>
	/// Initializes a new instance of the ChaosBufferDistributedCache class.
	/// </summary>
	/// <param name="innerCache">The actual <see cref="IBufferDistributedCache"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosBufferDistributedCache(IBufferDistributedCache innerCache, ILogger<ChaosBufferDistributedCache>? logger = null)
		: base(innerCache, logger)
	{
		_innerCache = innerCache;
	}

	/// <inheritdoc/>
	public bool TryGet(string key, IBufferWriter<byte> destination)
	{
		MaybeChaos();
		return _innerCache.TryGet(key, destination);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		return await _innerCache.TryGetAsync(key, destination, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
	{
		MaybeChaos();
		_innerCache.Set(key, value, options);
	}

	/// <inheritdoc/>
	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		await _innerCache.SetAsync(key, value, options, token).ConfigureAwait(false);
	}
}
