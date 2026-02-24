using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal sealed class BufferDistributedCacheAccessor(IBufferDistributedCache distributedCache, IBufferFusionCacheSerializer serializer, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
	: DistributedCacheAccessor<ArrayPoolBufferWriter>(options, logger, events)
{
	private readonly IBufferDistributedCache _cache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
	private readonly IBufferFusionCacheSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

	public override IDistributedCache DistributedCache => _cache;
	public override IFusionCacheSerializer Serializer => _serializer;

	protected override ArrayPoolBufferWriter Serialize<T>(FusionCacheDistributedEntry<T> obj)
	{
		var writer = new ArrayPoolBufferWriter();
		_serializer.Serialize(obj, writer);
		return writer;
	}

	protected override async ValueTask<ArrayPoolBufferWriter?> SerializeAsync<T>(FusionCacheDistributedEntry<T> obj, CancellationToken ct)
	{
		var writer = new ArrayPoolBufferWriter();
		await _serializer.SerializeAsync(obj, writer, ct).ConfigureAwait(false);
		return writer;
	}

	protected override T? Deserialize<T>(ArrayPoolBufferWriter data) where T : default
	{
		var buffer = data.GetWrittenBuffer();
		var deserialized = _serializer.Deserialize<T>(in buffer);

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		data.Dispose();

		return deserialized;
	}

	protected override async ValueTask<T?> DeserializeAsync<T>(ArrayPoolBufferWriter data, CancellationToken ct) where T : default
	{
		var buffer = data.GetWrittenBuffer();
		var deserialized = await _serializer.DeserializeAsync<T>(buffer, ct).ConfigureAwait(false);

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		data.Dispose();

		return deserialized;
	}

	protected override ArrayPoolBufferWriter? GetCacheEntry(string key)
	{
		var writer = new ArrayPoolBufferWriter();

		if (_cache.TryGet(key, writer))
		{
			return writer;
		}

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		writer.Dispose();
		return null;
	}

	protected override async Task<ArrayPoolBufferWriter?> GetCacheEntryAsync(string key, CancellationToken ct)
	{
		var writer = new ArrayPoolBufferWriter();

		if (await _cache.TryGetAsync(key, writer, ct))
		{
			return writer;
		}

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		writer.Dispose();
		return null;
	}

	protected override void SetCacheEntry(string key, ArrayPoolBufferWriter data, DistributedCacheEntryOptions distributedOptions)
	{
		var buffer = data.GetWrittenBuffer();
		
		_cache.Set(key, buffer, distributedOptions);

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		data.Dispose();
	}

	protected override async Task SetCacheEntryAsync(string key, ArrayPoolBufferWriter data, DistributedCacheEntryOptions distributedOptions, CancellationToken ct)
	{
		var buffer = data.GetWrittenBuffer();

		await _cache.SetAsync(key, buffer, distributedOptions, ct).ConfigureAwait(false);

		// Release the buffer ONLY if the call was successful. In case of exception, the buffer may still be used.
		data.Dispose();
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
