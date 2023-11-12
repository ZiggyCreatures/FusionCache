using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IDistributedCache"/> that implements the null object pattern, meaning that it does nothing. Consider this a kind of a pass-through implementation.
/// </summary>
public class NullDistributedCache
	: IDistributedCache
{
	/// <inheritdoc/>
	public byte[] Get(string key)
	{
		return null!;
	}

	/// <inheritdoc/>
	public Task<byte[]> GetAsync(string key, CancellationToken token = default)
	{
		return Task.FromResult<byte[]>(null!);
	}

	/// <inheritdoc/>
	public void Refresh(string key)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public Task RefreshAsync(string key, CancellationToken token = default)
	{
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public Task RemoveAsync(string key, CancellationToken token = default)
	{
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		return Task.CompletedTask;
	}
}
