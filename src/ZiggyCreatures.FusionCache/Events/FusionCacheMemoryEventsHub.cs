using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The events hub for events specific for the memory layer.
/// </summary>
public class FusionCacheMemoryEventsHub
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
	/// Check if the <see cref="Eviction"/> event has subscribers or not.
	/// </summary>
	/// <returns><see langword="true"/> if the <see cref="Eviction"/> event has subscribers, otherwhise <see langword="false"/>.</returns>
	public bool HasEvictionSubscribers()
	{
		return Eviction is not null;
	}

	internal void OnEviction(string operationId, string key, EvictionReason reason)
	{
		Eviction?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEvictionEventArgs(key, reason), nameof(Eviction), _logger, _errorsLogLevel, _syncExecution);
	}
}
