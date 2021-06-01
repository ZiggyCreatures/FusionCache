using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{

	public class FusionCacheMemoryEventsHub
		: FusionCacheBaseEventsHub
	{

		public FusionCacheMemoryEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
		}

		public event EventHandler<FusionCacheEntryEvictionEventArgs> Eviction;

		public bool HasEvictionSubscribers()
		{
			return Eviction is object;
		}

		internal void OnEviction(string operationId, string key, EvictionReason reason)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, Eviction, () => new FusionCacheEntryEvictionEventArgs(key, reason), nameof(Eviction), _logger, _options.EventHandlingErrorsLogLevel);
		}

	}
}
