using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{

	/// <summary>
	/// A class with base events that are common to any cache layer (general, memroy or distributed)
	/// </summary>
	public class FusionCacheBaseEventsHub
	{

		/// <summary>
		/// The <see cref="IFusionCache"/> instance.
		/// </summary>
		protected IFusionCache _cache;

		/// <summary>
		/// The <see cref="FusionCacheOptions"/> instance.
		/// </summary>
		protected readonly FusionCacheOptions _options;

		/// <summary>
		/// The <see cref="ILogger"/> instance.
		/// </summary>
		protected readonly ILogger? _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheBaseEventsHub"/> class.
		/// </summary>
		/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
		/// <param name="options">The <see cref="FusionCacheOptions"/> instance.</param>
		/// <param name="logger">The <see cref="ILogger"/> instance.</param>
		public FusionCacheBaseEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		{
			_cache = cache;
			_options = options;
			_logger = logger;
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

		internal void OnHit(string operationId, string key, bool isStale)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Hit, () => new FusionCacheEntryHitEventArgs(key, isStale), nameof(Hit), _logger, _options.EventHandlingErrorsLogLevel, _options.EnableSyncEventHandlersExecution);
		}

		internal void OnMiss(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Miss, () => new FusionCacheEntryEventArgs(key), nameof(Miss), _logger, _options.EventHandlingErrorsLogLevel, _options.EnableSyncEventHandlersExecution);
		}

		internal void OnSet(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Set, () => new FusionCacheEntryEventArgs(key), nameof(Set), _logger, _options.EventHandlingErrorsLogLevel, _options.EnableSyncEventHandlersExecution);
		}

		internal void OnRemove(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Remove, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel, _options.EnableSyncEventHandlersExecution);
		}
	}
}
