using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// The events hub for events specific for the backplane.
	/// </summary>
	public class FusionCacheBackplaneEventsHub
		: FusionCacheAbstractEventsHub
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheBackplaneEventsHub"/> class.
		/// </summary>
		/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
		/// <param name="options">The <see cref="FusionCacheOptions"/> instance.</param>
		/// <param name="logger">The <see cref="ILogger"/> instance.</param>
		public FusionCacheBackplaneEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
			// EMPTY
		}

		/// <summary>
		/// The event for a state change in the circuit breaker.
		/// </summary>
		public event EventHandler<FusionCacheCircuitBreakerChangeEventArgs>? CircuitBreakerChange;

		/// <summary>
		/// The event for a backplane message.
		/// </summary>
		public event EventHandler<FusionCacheBackplaneMessageEventArgs>? Message;

		internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
		{
			CircuitBreakerChange?.SafeExecute(operationId, key, _cache, () => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnMessage(string operationId, string key, BackplaneMessage message)
		{
			Message?.SafeExecute(operationId, key, _cache, () => new FusionCacheBackplaneMessageEventArgs(message), nameof(Message), _logger, _errorsLogLevel, _syncExecution);
		}
	}
}
