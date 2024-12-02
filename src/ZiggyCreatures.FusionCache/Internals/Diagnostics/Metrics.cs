using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

internal static class Metrics
{
	public static readonly Meter Meter = new Meter(FusionCacheDiagnostics.MeterName, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly Meter MeterMemoryLevel = new Meter(FusionCacheDiagnostics.MeterNameMemoryLevel, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly Meter MeterDistributedLevel = new Meter(FusionCacheDiagnostics.MeterNameDistributedLevel, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly Meter MeterBackplane = new Meter(FusionCacheDiagnostics.MeterNameBackplane, FusionCacheDiagnostics.FusionCacheVersion);

	// HIGH-LEVEL
	public static readonly Counter<long> CounterSet = Meter.CreateCounter<long>("fusioncache.cache.set");
	public static readonly Counter<long> CounterTryGet = Meter.CreateCounter<long>("fusioncache.cache.try_get");
	public static readonly Counter<long> CounterGetOrDefault = Meter.CreateCounter<long>("fusioncache.cache.get_or_default");
	public static readonly Counter<long> CounterGetOrSet = Meter.CreateCounter<long>("fusioncache.cache.get_or_set");
	public static readonly Counter<long> CounterRemove = Meter.CreateCounter<long>("fusioncache.cache.remove");
	public static readonly Counter<long> CounterExpire = Meter.CreateCounter<long>("fusioncache.cache.expire");
	public static readonly Counter<long> CounterRemoveByTag = Meter.CreateCounter<long>("fusioncache.cache.remove_by_tag");
	public static readonly Counter<long> CounterClear = Meter.CreateCounter<long>("fusioncache.cache.clear");

	public static readonly Counter<long> CounterHit = Meter.CreateCounter<long>("fusioncache.cache.hit");
	public static readonly Counter<long> CounterMiss = Meter.CreateCounter<long>("fusioncache.cache.miss");

	// FACTORY
	public static readonly Counter<long> CounterFactorySyntheticTimeout = Meter.CreateCounter<long>("fusioncache.factory.synthetic_timeout");
	public static readonly Counter<long> CounterFactoryError = Meter.CreateCounter<long>("fusioncache.factory.error");
	public static readonly Counter<long> CounterFactorySuccess = Meter.CreateCounter<long>("fusioncache.factory.success");

	// FAIL-SAFE
	public static readonly Counter<long> CounterFailSafeActivate = Meter.CreateCounter<long>("fusioncache.failsafe_activate");

	// EAGER REFRESH
	public static readonly Counter<long> CounterEagerRefresh = Meter.CreateCounter<long>("fusioncache.eager_refresh");

	// MEMORY
	public static readonly Counter<long> CounterMemorySet = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.set");
	public static readonly Counter<long> CounterMemoryGet = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.get");
	public static readonly Counter<long> CounterMemoryExpire = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.expire");
	public static readonly Counter<long> CounterMemoryRemove = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.remove");
	public static readonly Counter<long> CounterMemoryEvict = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.evict");
	public static readonly Counter<long> CounterMemoryHit = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.hit");
	public static readonly Counter<long> CounterMemoryMiss = MeterMemoryLevel.CreateCounter<long>("fusioncache.memory.miss");

	// DISTRIBUTED
	public static readonly Counter<long> CounterDistributedSet = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.set");
	public static readonly Counter<long> CounterDistributedGet = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.get");
	public static readonly Counter<long> CounterDistributedRemove = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.remove");
	public static readonly Counter<long> CounterDistributedHit = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.hit");
	public static readonly Counter<long> CounterDistributedMiss = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.miss");
	public static readonly Counter<long> CounterDistributedCircuitBreakerChange = MeterDistributedLevel.CreateCounter<long>("fusioncache.distributed.circuit_breaker_change");

	// SERIALIZATION
	public static readonly Counter<long> CounterSerializationError = MeterDistributedLevel.CreateCounter<long>("fusioncache.serialize_error");
	public static readonly Counter<long> CounterDeserializationError = MeterDistributedLevel.CreateCounter<long>("fusioncache.deserialize_error");

	// BACKPLANE
	public static readonly Counter<long> CounterBackplanePublish = MeterBackplane.CreateCounter<long>("fusioncache.backplane.publish");
	public static readonly Counter<long> CounterBackplaneReceive = MeterBackplane.CreateCounter<long>("fusioncache.backplane.receive");
	public static readonly Counter<long> CounterBackplaneCircuitBreakerChange = MeterBackplane.CreateCounter<long>("fusioncache.backplane.circuit_breaker_change");

	public static Counter<T>? Maybe<T>(this Counter<T> counter)
		where T : struct
	{
		if (counter.Enabled == false)
			return null;

		return counter;
	}

	public static KeyValuePair<string, object?>[] GetCommonTags(string? cacheName, string? cacheInstanceId, params KeyValuePair<string, object?>[] extraTags)
	{
		return [
			new KeyValuePair<string, object?>(Tags.Names.CacheName, cacheName),
			// NOTE: NOT THE NEXT ONES SINCE, WITH METRICS, PEOPLE ARE USUALLY CHARGED PER UNIQUE ATTRIBUTES
			//new KeyValuePair<string, object?>(Tags.Names.CacheInstanceId, cacheInstanceId),
			//new KeyValuePair<string, object?>(Tags.Names.OperationKey, key),
			//new KeyValuePair<string, object?>(Tags.Names.OperationId, operationId),
			.. extraTags ?? []
		];
	}

	public static void AddWithCommonTags<T>(this Counter<T> counter, T delta, string? cacheName, string? cacheInstanceId, params KeyValuePair<string, object?>[] extraTags)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		counter.Add(delta, GetCommonTags(cacheName, cacheInstanceId, extraTags));
	}
}
