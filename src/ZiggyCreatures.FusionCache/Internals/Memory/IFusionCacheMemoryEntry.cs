using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory
{
	/// <summary>
	/// Represents a memory entry in <see cref="FusionCache"/>, but as a non-generic interface so it can be used from code that doesn't know the actual type of the value (eg: auto-recovery and other places).
	/// </summary>
	internal interface IFusionCacheMemoryEntry
		: IFusionCacheEntry
	{
		DateTimeOffset PhysicalExpiration { get; set; }
		object? Value { get; set; }

		ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryAsync(string operationId, string cacheKey, FusionCache cache);
		ValueTask<bool> SetDistributedEntryAsync(string operationId, string key, DistributedCacheAccessor dca, FusionCacheEntryOptions options, bool isBackground, CancellationToken token);
	}
}
