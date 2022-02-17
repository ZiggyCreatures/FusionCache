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
		/// The event for a sent backplane message.
		/// </summary>
		public event EventHandler<FusionCacheBackplaneMessageEventArgs>? MessagePublished;

		/// <summary>
		/// The event for a received backplane message.
		/// </summary>
		public event EventHandler<FusionCacheBackplaneMessageEventArgs>? MessageReceived;

		internal void OnCircuitBreakerChange(string? operationId, string? key, bool isClosed)
		{
			CircuitBreakerChange?.SafeExecute(operationId, key, _cache, () => new FusionCacheCircuitBreakerChangeEventArgs(isClosed), nameof(CircuitBreakerChange), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnMessagePublished(string operationId, BackplaneMessage message)
		{
			MessagePublished?.SafeExecute(operationId, message.CacheKey, _cache, () => new FusionCacheBackplaneMessageEventArgs(message), nameof(MessagePublished), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnMessageReceived(string operationId, BackplaneMessage message)
		{
			MessageReceived?.SafeExecute(operationId, message.CacheKey, _cache, () => new FusionCacheBackplaneMessageEventArgs(message), nameof(MessageReceived), _logger, _errorsLogLevel, _syncExecution);
		}
	}
}
