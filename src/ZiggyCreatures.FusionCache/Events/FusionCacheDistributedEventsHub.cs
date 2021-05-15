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
		public event EventHandler<FusionCacheEntryEventArgs> SerializationError;
		public event EventHandler<FusionCacheEntryEventArgs> DeserializationError;

		internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, CircuitBreakerChange, () => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _options.EventsErrorsLogLevel);
		}

		internal void OnSerializationError(string? operationId, string? key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, SerializationError, () => new FusionCacheEntryEventArgs(key), nameof(SerializationError), _logger, _options.EventsErrorsLogLevel);
		}

		internal void OnDeserializationError(string? operationId, string? key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, DeserializationError, () => new FusionCacheEntryEventArgs(key), nameof(DeserializationError), _logger, _options.EventsErrorsLogLevel);
		}

	}
}
