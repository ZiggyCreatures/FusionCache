using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{

	public class FusionCacheDistributedEventsHub
		: FusionCacheBaseEventsHub
	{

		public FusionCacheDistributedEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
		}

		public event EventHandler<FusionCacheCircuitBreakerChangeEventArgs> CircuitBreakerChange;

		internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, CircuitBreakerChange, () => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _options.EventsErrorsLogLevel);
		}

	}
}
