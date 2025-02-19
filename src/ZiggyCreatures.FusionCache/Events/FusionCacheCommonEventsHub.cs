using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// A class with base events that are common to any cache level (general, memory or distributed)
/// </summary>
public abstract class FusionCacheCommonEventsHub
	: FusionCacheAbstractEventsHub
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheCommonEventsHub"/> class.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="options">The <see cref="FusionCacheOptions"/> instance.</param>
	/// <param name="logger">The <see cref="ILogger"/> instance.</param>
	protected FusionCacheCommonEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		: base(cache, options, logger)
	{
		// EMPTY
	}

	/// <summary>
	/// The event for a cache hit (either fresh or stale).
	/// </summary>
	public event EventHandler<FusionCacheEntryHitEventArgs>? Hit;

	/// <summary>
	/// The event for a cache miss.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? Miss;

	/// <summary>
	/// The event for a cache set.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? Set;

	/// <summary>
	/// The event for a cache remove.
	/// </summary>
	public event EventHandler<FusionCacheEntryEventArgs>? Remove;

	internal virtual void OnHit(string operationId, string key, bool isStale, Activity? activity)
	{
		// ACTIVITY
		activity?.AddTag(Tags.Names.Hit, true);
		activity?.AddTag(Tags.Names.Stale, isStale);

		Hit?.SafeExecute(operationId, key, _cache, new FusionCacheEntryHitEventArgs(key, isStale), nameof(Hit), _logger, _errorsLogLevel, _syncExecution);
	}

	internal virtual void OnMiss(string operationId, string key, Activity? activity)
	{
		// ACTIVITY
		activity?.AddTag(Tags.Names.Hit, false);

		Miss?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(Miss), _logger, _errorsLogLevel, _syncExecution);
	}

	internal virtual void OnSet(string operationId, string key)
	{
		Set?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(Set), _logger, _errorsLogLevel, _syncExecution);
	}

	internal virtual void OnRemove(string operationId, string key)
	{
		Remove?.SafeExecute(operationId, key, _cache, new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _errorsLogLevel, _syncExecution);
	}
}
