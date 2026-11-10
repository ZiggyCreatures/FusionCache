using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IDistributedCache"/> wrapper that keeps track of which methods have been called, and for which keys: it does NOT implement <see cref="IBufferDistributedCache"/>, so it can be used to check that the buffered path is not used when the distributed cache does not support it.
/// <br/><br/>
/// NOTE: counts are per-key because FusionCache also reads/writes its own internal marker entries (eg: for tagging and clearing), which would otherwise show up in the totals.
/// </summary>
internal class CallTrackingDistributedCache
	: IDistributedCache
{
	private readonly IDistributedCache _innerCache;
	private readonly List<string> _classicGets = [];
	private readonly List<string> _classicSets = [];
	private readonly List<string> _bufferGets = [];
	private readonly List<string> _bufferSets = [];

	public CallTrackingDistributedCache(IDistributedCache innerCache)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
	}

	public int ClassicGetCount(string key) => CountFor(_classicGets, key);
	public int ClassicSetCount(string key) => CountFor(_classicSets, key);
	public int BufferGetCount(string key) => CountFor(_bufferGets, key);
	public int BufferSetCount(string key) => CountFor(_bufferSets, key);

	private static int CountFor(List<string> keys, string key)
	{
		lock (keys)
		{
			// THE L2 KEY IS THE CACHE KEY PLUS A PREFIX AND/OR A WIRE FORMAT TOKEN, SO MATCH ON A SUBSTRING
			return keys.Count(x => x.Contains(key, StringComparison.Ordinal));
		}
	}

	private static void Track(List<string> keys, string key)
	{
		lock (keys)
		{
			keys.Add(key);
		}
	}

	/// <inheritdoc/>
	public byte[]? Get(string key)
	{
		Track(_classicGets, key);
		return _innerCache.Get(key);
	}

	/// <inheritdoc/>
	public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
	{
		Track(_classicGets, key);
		return await _innerCache.GetAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Refresh(string key)
	{
		_innerCache.Refresh(key);
	}

	/// <inheritdoc/>
	public async Task RefreshAsync(string key, CancellationToken token = default)
	{
		await _innerCache.RefreshAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		_innerCache.Remove(key);
	}

	/// <inheritdoc/>
	public async Task RemoveAsync(string key, CancellationToken token = default)
	{
		await _innerCache.RemoveAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		Track(_classicSets, key);
		_innerCache.Set(key, value, options);
	}

	/// <inheritdoc/>
	public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		Track(_classicSets, key);
		await _innerCache.SetAsync(key, value, options, token).ConfigureAwait(false);
	}

	protected void TrackBufferGet(string key)
	{
		Track(_bufferGets, key);
	}

	protected void TrackBufferSet(string key)
	{
		Track(_bufferSets, key);
	}
}

/// <summary>
/// A <see cref="CallTrackingDistributedCache"/> that also implements <see cref="IBufferDistributedCache"/>, mimicking what the Redis implementation does with the buffer APIs: on a get the payload is copied into the caller's buffer writer, and on a set the sequence is expected to be contiguous (a multi-segment one would have to be linearized first).
/// </summary>
internal sealed class BufferCallTrackingDistributedCache
	: CallTrackingDistributedCache, IBufferDistributedCache
{
	private readonly IDistributedCache _innerCache;

	public BufferCallTrackingDistributedCache(IDistributedCache innerCache)
		: base(innerCache)
	{
		_innerCache = innerCache;
	}

	/// <summary>
	/// Whether every sequence received via the buffered set methods has been a single-segment one: since consumers like Redis need contiguous memory, a multi-segment sequence would mean an extra copy on their side.
	/// </summary>
	public bool AllSetSequencesWereSingleSegment { get; private set; } = true;

	/// <inheritdoc/>
	public bool TryGet(string key, IBufferWriter<byte> destination)
	{
		TrackBufferGet(key);

		var data = _innerCache.Get(key);
		if (data is null)
			return false;

		destination.Write(data);
		return true;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		TrackBufferGet(key);

		var data = await _innerCache.GetAsync(key, token).ConfigureAwait(false);
		if (data is null)
			return false;

		destination.Write(data);
		return true;
	}

	/// <inheritdoc/>
	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
	{
		TrackBufferSet(key);
		TrackSetSequence(value);

		_innerCache.Set(key, value.ToArray(), options);
	}

	/// <inheritdoc/>
	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		TrackBufferSet(key);
		TrackSetSequence(value);

		await _innerCache.SetAsync(key, value.ToArray(), options, token).ConfigureAwait(false);
	}

	private void TrackSetSequence(in ReadOnlySequence<byte> value)
	{
		if (value.IsSingleSegment == false)
			AllSetSequencesWereSingleSegment = false;
	}
}
