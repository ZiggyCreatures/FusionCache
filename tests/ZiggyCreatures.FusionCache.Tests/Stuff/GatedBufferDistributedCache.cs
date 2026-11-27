using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IBufferDistributedCache"/> wrapper whose buffered reads for the keys matching a filter, once enabled, wait for a gate to be opened before writing the payload into the caller's buffer writer: it ignores cancellation while waiting, like a real network call already in flight would.
/// <br/><br/>
/// This allows to deterministically simulate a read that outlives the caller (eg: because of a timeout) and then writes into the buffer.
/// </summary>
internal sealed class GatedBufferDistributedCache
	: IBufferDistributedCache
{
	private readonly IDistributedCache _innerCache;
	private readonly Func<string, bool> _keyFilter;
	private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource<bool> _readCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private volatile bool _isEnabled;

	public GatedBufferDistributedCache(IDistributedCache innerCache, Func<string, bool> keyFilter)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
		_keyFilter = keyFilter ?? throw new ArgumentNullException(nameof(keyFilter));
	}

	/// <summary>
	/// Completes when a gated read has written into the caller's buffer writer and returned.
	/// </summary>
	public Task ReadCompleted => _readCompleted.Task;

	public void Enable() => _isEnabled = true;

	public void OpenGate() => _gate.TrySetResult(true);

	public bool TryGet(string key, IBufferWriter<byte> destination)
	{
		var shouldGate = _isEnabled && _keyFilter(key);
		var data = _innerCache.Get(key);
		if (shouldGate)
			_gate.Task.GetAwaiter().GetResult();

		try
		{
			return Write(data, destination);
		}
		finally
		{
			if (shouldGate)
				_readCompleted.TrySetResult(true);
		}
	}

	public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		var shouldGate = _isEnabled && _keyFilter(key);
		var data = await _innerCache.GetAsync(key, CancellationToken.None).ConfigureAwait(false);
		if (shouldGate)
			await _gate.Task.ConfigureAwait(false);

		try
		{
			return Write(data, destination);
		}
		finally
		{
			if (shouldGate)
				_readCompleted.TrySetResult(true);
		}
	}

	private static bool Write(byte[]? data, IBufferWriter<byte> destination)
	{
		if (data is null)
			return false;

		destination.Write(data);
		return true;
	}

	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options) => _innerCache.Set(key, value.ToArray(), options);

	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default) => await _innerCache.SetAsync(key, value.ToArray(), options, token).ConfigureAwait(false);

	public byte[]? Get(string key) => _innerCache.Get(key);

	public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _innerCache.GetAsync(key, token);

	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _innerCache.Set(key, value, options);

	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => _innerCache.SetAsync(key, value, options, token);

	public void Refresh(string key) => _innerCache.Refresh(key);

	public Task RefreshAsync(string key, CancellationToken token = default) => _innerCache.RefreshAsync(key, token);

	public void Remove(string key) => _innerCache.Remove(key);

	public Task RemoveAsync(string key, CancellationToken token = default) => _innerCache.RemoveAsync(key, token);
}
