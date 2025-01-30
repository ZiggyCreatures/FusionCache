using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OutputCaching;

namespace ZiggyCreatures.Caching.Fusion.AspNetCore.OutputCaching;

internal sealed class FusionOutputCacheStore : IOutputCacheStore
{
	private readonly IFusionCache _cache;

	public FusionOutputCacheStore(IFusionCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache, nameof(cache));

		_cache = cache;
	}

	/// <inheritdoc />
	public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
	{
		await _cache.RemoveByTagAsync(tag, token: cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
	{
		return await _cache.GetOrDefaultAsync<byte[]?>(key, null, token: cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken)
	{
		await _cache.SetAsync(key, value, options => options.SetDuration(validFor).SetSize(value.Length), tags, token: cancellationToken);
	}
}
