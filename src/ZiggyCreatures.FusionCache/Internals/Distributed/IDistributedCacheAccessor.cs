using Microsoft.Extensions.Caching.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal interface IDistributedCacheAccessor
{
	IDistributedCache DistributedCache { get; }
		
	IFusionCacheSerializer Serializer { get; }

	bool IsCurrentlyUsable(string? operationId, string? key);

	(FusionCacheDistributedEntry<TValue>? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, TimeSpan? timeout, CancellationToken token);
	
	ValueTask<(FusionCacheDistributedEntry<TValue>? entry, bool isValid)> TryGetEntryAsync<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, TimeSpan? timeout, CancellationToken token);

	bool SetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token);
	
	ValueTask<bool> SetEntryAsync<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token);

	bool RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, bool isBackground, CancellationToken token);
	
	ValueTask<bool> RemoveEntryAsync(string operationId, string key, FusionCacheEntryOptions options, bool isBackground, CancellationToken token);
}
