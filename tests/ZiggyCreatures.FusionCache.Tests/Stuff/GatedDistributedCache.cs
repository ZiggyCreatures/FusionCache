using Microsoft.Extensions.Caching.Distributed;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IDistributedCache"/> wrapper that, for the keys matching a filter, first reads from the inner cache and then waits for a gate to be opened before returning.
/// <br/>
/// This allows to deterministically hold a read AFTER the value has been read, to simulate other things happening while the read is still in flight.
/// </summary>
internal class GatedDistributedCache
	: IDistributedCache
{
	private readonly IDistributedCache _innerCache;
	private readonly Func<string, bool> _keyFilter;
	private readonly TaskCompletionSource<bool> _readReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private int _isEnabled;

	public GatedDistributedCache(IDistributedCache innerCache, Func<string, bool> keyFilter)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
		_keyFilter = keyFilter ?? throw new ArgumentNullException(nameof(keyFilter));
	}

	public Task ReadReached => _readReached.Task;

	public bool WaitForReadReached(TimeSpan timeout)
	{
		return ((IAsyncResult)_readReached.Task).AsyncWaitHandle.WaitOne(timeout);
	}

	public void Enable()
	{
		Interlocked.Exchange(ref _isEnabled, 1);
	}

	public void OpenGate()
	{
		_gate.TrySetResult(true);
	}

	private bool ShouldGate(string key)
	{
		return _keyFilter(key) && Interlocked.CompareExchange(ref _isEnabled, 0, 1) == 1;
	}

	public byte[]? Get(string key)
	{
		var shouldGate = ShouldGate(key);
		var res = _innerCache.Get(key);
		if (shouldGate)
		{
			_readReached.TrySetResult(true);
			_gate.Task.GetAwaiter().GetResult();
		}
		return res;
	}

	public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
	{
		var shouldGate = ShouldGate(key);
		var res = await _innerCache.GetAsync(key, token).ConfigureAwait(false);
		if (shouldGate)
		{
			_readReached.TrySetResult(true);
			await _gate.Task.ConfigureAwait(false);
		}
		return res;
	}

	public void Refresh(string key)
	{
		_innerCache.Refresh(key);
	}

	public Task RefreshAsync(string key, CancellationToken token = default)
	{
		return _innerCache.RefreshAsync(key, token);
	}

	public void Remove(string key)
	{
		_innerCache.Remove(key);
	}

	public Task RemoveAsync(string key, CancellationToken token = default)
	{
		return _innerCache.RemoveAsync(key, token);
	}

	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		_innerCache.Set(key, value, options);
	}

	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		return _innerCache.SetAsync(key, value, options, token);
	}
}
