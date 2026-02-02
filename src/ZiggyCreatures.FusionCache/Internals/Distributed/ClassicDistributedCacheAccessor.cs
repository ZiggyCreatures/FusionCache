using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal sealed class ClassicDistributedCacheAccessor(IDistributedCache distributedCache, IFusionCacheSerializer serializer, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
	: DistributedCacheAccessor<byte[]>(options, logger, events)
{
	private readonly IDistributedCache _cache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
	private readonly IFusionCacheSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

	public override IDistributedCache DistributedCache => _cache;
	public override IFusionCacheSerializer Serializer => _serializer;

	protected override byte[]? Serialize<T>(FusionCacheDistributedEntry<T> obj)
	{
		return _serializer.Serialize(obj);
	}

	protected override ValueTask<byte[]?> SerializeAsync<T>(FusionCacheDistributedEntry<T> obj, CancellationToken ct)
	{
		return _serializer.SerializeAsync(obj, ct)!;
	}

	protected override T? Deserialize<T>(byte[] data) where T : default
	{
		return _serializer.Deserialize<T>(data);
	}

	protected override ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken ct) where T : default
	{
		return _serializer.DeserializeAsync<T>(data, ct);
	}

	protected override byte[]? GetCacheEntry(string key)
	{
		return _cache.Get(key);
	}

	protected override Task<byte[]?> GetCacheEntryAsync(string key, CancellationToken ct)
	{
		return _cache.GetAsync(key, ct);
	}

	protected override void SetCacheEntry(string key, byte[] data, DistributedCacheEntryOptions distributedOptions)
	{
		_cache.Set(key, data, distributedOptions);
	}

	protected override Task SetCacheEntryAsync(string key, byte[] data, DistributedCacheEntryOptions distributedOptions, CancellationToken ct)
	{
		return _cache.SetAsync(key, data, distributedOptions, ct);
	}

	protected override void RemoveCacheEntry(string key)
	{
		_cache.Remove(key);
	}

	protected override Task RemoveCacheEntryAsync(string key, CancellationToken ct)
	{
		return _cache.RemoveAsync(key, ct);
	}
}
