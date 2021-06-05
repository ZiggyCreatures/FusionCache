using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// The events hub for events specific for the distributed layer.
	/// </summary>
	public class FusionCacheDistributedEventsHub
		: FusionCacheBaseEventsHub
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheDistributedEventsHub" /> class.
		/// </summary>
		/// <param name="cache">The <see cref="IFusionCache" /> instance.</param>
		/// <param name="options">The <see cref="FusionCacheOptions" /> instance.</param>
		/// <param name="logger">The <see cref="ILogger" /> instance.</param>
		public FusionCacheDistributedEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
		}

		/// <summary>
		/// The event for a state change in the circuit breaker.
		/// </summary>
		public event EventHandler<FusionCacheCircuitBreakerChangeEventArgs>? CircuitBreakerChange;

		/// <summary>
		/// The event for data serialization.
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? SerializationError;

		/// <summary>
		/// The event for data deserialization.
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? DeserializationError;

		internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
		{
			CircuitBreakerChange?.SafeExecute(operationId, key, _cache, () => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnSerializationError(string? operationId, string? key)
		{
			SerializationError?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key ?? string.Empty), nameof(SerializationError), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnDeserializationError(string? operationId, string? key)
		{
			DeserializationError?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key ?? string.Empty), nameof(DeserializationError), _logger, _errorsLogLevel, _syncExecution);
		}
	}
}
