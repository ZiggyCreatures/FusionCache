using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The events hub for events specific for the memory level.
/// </summary>
public sealed class FusionCacheMemoryEventsHub
	: FusionCacheCommonEventsHub
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheMemoryEventsHub" /> class.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache" /> instance.</param>
	/// <param name="options">The <see cref="FusionCacheOptions" /> instance.</param>
	/// <param name="logger">The <see cref="ILogger" /> instance.</param>
	public FusionCacheMemoryEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		: base(cache, options, logger)
	{
	}

	/// <summary>
	/// The event for a cache eviction.
	/// </summary>
	public event EventHandler<FusionCacheEntryEvictionEventArgs>? Eviction;

	/// <summary>
	/// The event for a manual cache Expire() call.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? Expire;

	/// <summary>
	/// Check if the <see cref="Eviction"/> event has subscribers or not.
	/// </summary>
	/// <returns><see langword="true"/> if the <see cref="Eviction"/> event has subscribers, otherwhise <see langword="false"/>.</returns>
	public bool HasEvictionSubscribers()
	{
		return Eviction is not null;
	}

	internal void OnEviction(string operationId, string key, EvictionReason reason, object? value)
	{
		Metrics.CounterMemoryEvict.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.memory.evict_reason", reason.ToString()));

		Eviction?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEvictionEventArgs(key, reason, value), nameof(Eviction), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnExpire(string operationId, string key)
	{
		Metrics.CounterMemoryExpire.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		Expire?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(Expire), _logger, _errorsLogLevel, _syncExecution);
	}

	internal override void OnHit(string operationId, string key, bool isStale)
	{
		Metrics.CounterMemoryHit.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.stale", isStale));

		base.OnHit(operationId, key, isStale);
	}

	internal override void OnMiss(string operationId, string key)
	{
		Metrics.CounterMemoryMiss.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnMiss(operationId, key);
	}

	internal override void OnSet(string operationId, string key)
	{
		Metrics.CounterMemorySet.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnSet(operationId, key);
	}

	internal override void OnRemove(string operationId, string key)
	{
		Metrics.CounterMemoryRemove.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnRemove(operationId, key);
	}
}
