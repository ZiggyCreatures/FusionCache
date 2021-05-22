using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{

	public class FusionCacheBaseEventsHub
	{

		protected IFusionCache _cache;
		protected readonly FusionCacheOptions _options;
		protected readonly ILogger? _logger;

		public FusionCacheBaseEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		{
			_cache = cache;
			_options = options;
			_logger = logger;
		}

		public event EventHandler<FusionCacheEntryHitEventArgs> Hit;
		public event EventHandler<FusionCacheEntryEventArgs> Miss;
		public event EventHandler<FusionCacheEntryEventArgs> Set;
		public event EventHandler<FusionCacheEntryEventArgs> Remove;
		public event EventHandler<FusionCacheEntryEventArgs> Expired;
		public event EventHandler<FusionCacheEntryEventArgs> Capacity;
		public event EventHandler<FusionCacheEntryEventArgs> Replaced;
		public event EventHandler<FusionCacheEntryEventArgs> Evicted;

		internal void OnHit(string operationId, string key, bool isStale)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Hit, () => new FusionCacheEntryHitEventArgs(key, isStale), nameof(Hit), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnMiss(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Miss, () => new FusionCacheEntryEventArgs(key), nameof(Miss), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnSet(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Set, () => new FusionCacheEntryEventArgs(key), nameof(Set), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnRemove(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Remove, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnCacheExpired(string operationId, string? key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Expired, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnCacheCapacity(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Capacity, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnCacheReplaced(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Replaced, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnCacheEvicted(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Evicted, () => new FusionCacheEntryEventArgs(key), nameof(Remove), _logger, _options.EventHandlingErrorsLogLevel);
		}
	}
}
