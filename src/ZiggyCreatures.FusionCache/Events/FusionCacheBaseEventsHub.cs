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
	}
}
