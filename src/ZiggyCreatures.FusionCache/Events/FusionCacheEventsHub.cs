using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheEventsHub
		: FusionCacheBaseEventsHub
	{
		public FusionCacheEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
			Memory = new FusionCacheBaseEventsHub(_cache, _options, _logger);
			Distributed = new FusionCacheDistributedEventsHub(_cache, _options, _logger);
		}

		public FusionCacheBaseEventsHub Memory { get; }
		public FusionCacheDistributedEventsHub Distributed { get; }

		public event EventHandler<FusionCacheEntryEventArgs> FailSafeActivate;

		internal void OnFailSafeActivate(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, FailSafeActivate, () => new FusionCacheEntryEventArgs(key), nameof(FailSafeActivate), _logger, _options.EventsErrorsLogLevel);
		}
	}
}
