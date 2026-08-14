using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion;

public partial class FusionCache
{
	private bool TryAdoptDistributedEntryForEagerRefresh<TValue>(string operationId, string key, IFusionCacheMemoryEntry memoryEntry, FusionCacheDistributedEntry<TValue>? distributedEntry, bool distributedEntryIsValid, FusionCacheEntryOptions options)
	{
		if (distributedEntryIsValid == false || distributedEntry is null)
			return false;

		if (distributedEntry.IsStale() || distributedEntry.ShouldEagerlyRefresh())
			return false;

		if (distributedEntry.Timestamp < memoryEntry.Timestamp)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry more fresh than distributed entry, do not update memory entry", CacheName, InstanceId, operationId, key);

			return false;
		}

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): using distributed entry to eagerly refresh memory entry", CacheName, InstanceId, operationId, key);

		if (_mca.ShouldWrite(options))
		{
			var refreshedMemoryEntry = distributedEntry.AsMemoryEntry<TValue>(options);
			_mca.SetEntry<TValue>(operationId, key, refreshedMemoryEntry, options);
		}

		return true;
	}

	private void NotifyEagerRefresh(string operationId, string key)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): eagerly refreshing", CacheName, InstanceId, operationId, key);

		_events.OnEagerRefresh(operationId, key);
	}
}
