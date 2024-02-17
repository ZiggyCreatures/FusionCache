using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The events hub for events specific for the distributed level.
/// </summary>
public sealed class FusionCacheDistributedEventsHub
	: FusionCacheCommonEventsHub
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheDistributedEventsHub" /> class.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache" /> instance.</param>
	/// <param name="options">The <see cref="FusionCacheOptions" /> instance.</param>
	/// <param name="logger">The <see cref="ILogger" /> instance.</param>
	public FusionCacheDistributedEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		: base(cache, options, logger)
	{
	}

	/// <summary>
	/// The event for a state change in the circuit breaker.
	/// </summary>
	public event EventHandler<FusionCacheCircuitBreakerChangeEventArgs>? CircuitBreakerChange;

	/// <summary>
	/// The event for data serialization.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? SerializationError;

	/// <summary>
	/// The event for data deserialization.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? DeserializationError;

	internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
	{
		Metrics.CounterDistributedCircuitBreakerChange.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.distributed.circuit_breaker.closed", isClosed));

		CircuitBreakerChange?.SafeExecute(operationId, key, _cache, x => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnSerializationError(string? operationId, string? key)
	{
		Metrics.CounterSerializationError.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		SerializationError?.SafeExecute(operationId, key, _cache, static x => new FusionCacheEntryEventArgs(x ?? string.Empty), nameof(SerializationError), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnDeserializationError(string? operationId, string? key)
	{
		Metrics.CounterDeserializationError.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		DeserializationError?.SafeExecute(operationId, key, _cache, static x => new FusionCacheEntryEventArgs(x ?? string.Empty), nameof(DeserializationError), _logger, _errorsLogLevel, _syncExecution);
	}

	internal override void OnHit(string operationId, string key, bool isStale)
	{
		Metrics.CounterDistributedHit.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.stale", isStale));

		base.OnHit(operationId, key, isStale);
	}

	internal override void OnMiss(string operationId, string key)
	{
		Metrics.CounterDistributedMiss.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnMiss(operationId, key);
	}

	internal override void OnSet(string operationId, string key)
	{
		Metrics.CounterDistributedSet.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnSet(operationId, key);
	}

	internal override void OnRemove(string operationId, string key)
	{
		Metrics.CounterDistributedRemove.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnRemove(operationId, key);
	}
}
