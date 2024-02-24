﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The events hub for high-level events for a FusionCache instance, as a whole.
/// </summary>
public sealed class FusionCacheEventsHub
	: FusionCacheCommonEventsHub
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheEventsHub" /> class.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache" /> instance.</param>
	/// <param name="options">The <see cref="FusionCacheOptions" /> instance.</param>
	/// <param name="logger">The <see cref="ILogger" /> instance.</param>
	public FusionCacheEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		: base(cache, options, logger)
	{
		Memory = new FusionCacheMemoryEventsHub(_cache, _options, _logger);
		Distributed = new FusionCacheDistributedEventsHub(_cache, _options, _logger);
		Backplane = new FusionCacheBackplaneEventsHub(_cache, _options, _logger);
	}

	/// <summary>
	/// The events hub for the memory level.
	/// </summary>
	public FusionCacheMemoryEventsHub Memory { get; }

	/// <summary>
	/// The events hub for the distributed level.
	/// </summary>
	public FusionCacheDistributedEventsHub Distributed { get; }

	/// <summary>
	/// The events hub for the backplane.
	/// </summary>
	public FusionCacheBackplaneEventsHub Backplane { get; }

	/// <summary>
	/// The event for a fail-safe activation.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? FailSafeActivate;

	/// <summary>
	/// The event for a synthetic timeout during a factory execution.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? FactorySyntheticTimeout;

	/// <summary>
	/// The event for a generic error during a non-background factory execution (excluding synthetic timeouts, for which there is the specific <see cref="FactorySyntheticTimeout"/> event).
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? FactoryError;

	/// <summary>
	/// The event for when a non-background factory execution completes successfully, therefore automatically updating the corresponding cache entry.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? FactorySuccess;

	/// <summary>
	/// The event for a generic error during a factory background execution (a factory that hit a synthetic timeout and has been relegated to background execution).
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? BackgroundFactoryError;

	/// <summary>
	/// The event for when a factory background execution (a factory that hit a synthetic timeout and has been relegated to background execution) completes successfully, therefore automatically updating the corresponding cache entry.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? BackgroundFactorySuccess;

	/// <summary>
	/// The event for when a factory is being executed in advance, because a request came in during the eager refresh window (after the eager refresh threshold and before the expiration).
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? EagerRefresh;

	/// <summary>
	/// The event for a manual cache Expire() call.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? Expire;

	internal void OnFailSafeActivate(string operationId, string key)
	{
		Metrics.CounterFailSafeActivate.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		FailSafeActivate?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(FailSafeActivate), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnFactorySyntheticTimeout(string operationId, string key)
	{
		Metrics.CounterFactorySyntheticTimeout.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		FactorySyntheticTimeout?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(FactorySyntheticTimeout), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnFactoryError(string operationId, string key)
	{
		Metrics.CounterFactoryError.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.operation.background", false));

		FactoryError?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(FactoryError), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnFactorySuccess(string operationId, string key)
	{
		Metrics.CounterFactorySuccess.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.operation.background", false));

		FactorySuccess?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(FactorySuccess), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnBackgroundFactoryError(string operationId, string key)
	{
		Metrics.CounterFactoryError.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.operation.background", true));

		BackgroundFactoryError?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(BackgroundFactoryError), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnBackgroundFactorySuccess(string operationId, string key)
	{
		Metrics.CounterFactorySuccess.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.operation.background", true));

		BackgroundFactorySuccess?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(BackgroundFactorySuccess), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnEagerRefresh(string operationId, string key)
	{
		Metrics.CounterEagerRefresh.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		EagerRefresh?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(EagerRefresh), _logger, _errorsLogLevel, _syncExecution);
	}

	internal void OnExpire(string operationId, string key)
	{
		Metrics.CounterExpire.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		Expire?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(Expire), _logger, _errorsLogLevel, _syncExecution);
	}

	internal override void OnHit(string operationId, string key, bool isStale)
	{
		Metrics.CounterHit.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId, new KeyValuePair<string, object?>("fusioncache.stale", isStale));

		base.OnHit(operationId, key, isStale);
	}

	internal override void OnMiss(string operationId, string key)
	{
		Metrics.CounterMiss.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnMiss(operationId, key);
	}

	internal override void OnSet(string operationId, string key)
	{
		Metrics.CounterSet.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnSet(operationId, key);
	}

	internal override void OnRemove(string operationId, string key)
	{
		Metrics.CounterRemove.Maybe()?.AddWithCommonTags(1, _cache.CacheName, _cache.InstanceId);

		base.OnRemove(operationId, key);
	}
}
