using Microsoft.Extensions.Caching.Distributed;

namespace FusionCacheTests.Stuff;

internal class LimitedCharsDistributedCache
	: IDistributedCache
{
	private readonly IDistributedCache _innerCache;
	private readonly Func<string, bool> _keyValidator;

	public LimitedCharsDistributedCache(IDistributedCache innerCache, Func<string, bool> keyValidator)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
		_keyValidator = keyValidator;
	}

	private void ValidateKey(string key)
	{
		if (_keyValidator(key) == false)
			throw new ArgumentException("The specified key is invalid.", nameof(key));
	}

	/// <inheritdoc/>
	public byte[]? Get(string key)
	{
		ValidateKey(key);
		return _innerCache.Get(key);
	}

	/// <inheritdoc/>
	public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
	{
		ValidateKey(key);
		return await _innerCache.GetAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Refresh(string key)
	{
		ValidateKey(key);
		_innerCache.Refresh(key);
	}

	/// <inheritdoc/>
	public async Task RefreshAsync(string key, CancellationToken token = default)
	{
		ValidateKey(key);
		await _innerCache.RefreshAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		ValidateKey(key);
		_innerCache.Remove(key);
	}

	/// <inheritdoc/>
	public async Task RemoveAsync(string key, CancellationToken token = default)
	{
		ValidateKey(key);
		await _innerCache.RemoveAsync(key, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		ValidateKey(key);
		_innerCache.Set(key, value, options);
	}

	/// <inheritdoc/>
	public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		ValidateKey(key);
		await _innerCache.SetAsync(key, value, options, token).ConfigureAwait(false);
	}
}
