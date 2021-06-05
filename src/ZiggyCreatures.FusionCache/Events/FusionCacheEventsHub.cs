using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// The events hub for high-level events for a FusionCache instance, as a whole.
	/// </summary>
	public class FusionCacheEventsHub
		: FusionCacheBaseEventsHub
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheEventsHub" /> class.
		/// </summary>
		/// <param name="cache">The <see cref="IFusionCache" /> instance.</param>
		/// <param name="options">The <see cref="FusionCacheOptions" /> instance.</param>
		/// <param name="logger">The <see cref="ILogger" /> instance.</param>
		public FusionCacheEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
			Memory = new FusionCacheMemoryEventsHub(_cache, _options, _logger);
			Distributed = new FusionCacheDistributedEventsHub(_cache, _options, _logger);
		}

		/// <summary>
		/// The events hub for the memory layer.
		/// </summary>
		public FusionCacheMemoryEventsHub Memory { get; }

		/// <summary>
		/// The events hub for the distributed layer.
		/// </summary>
		public FusionCacheDistributedEventsHub Distributed { get; }

		/// <summary>
		/// The event for a fail-safe activation.
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? FailSafeActivate;

		/// <summary>
		/// The event for a synthetic timeout during a factory execution.
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? FactorySyntheticTimeout;

		/// <summary>
		/// The event for a generic error during a factory execution (excluding synthetic timeouts, for which there is the specific <see cref="FactorySyntheticTimeout"/> event).
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? FactoryError;

		/// <summary>
		/// The event for a generic error during a factory background execution (a factory that hit a synthetic timeout and has been relegated to background execution).
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? BackgroundFactoryError;

		/// <summary>
		/// The event for when a factory background execution (a factory that hit a synthetic timeout and has been relegated to background execution) completes successfully, therefore automatically updating the corresponsing cache entry.
		/// </summary>
		public event EventHandler<FusionCacheEntryEventArgs>? BackgroundFactorySuccess;

		internal void OnFailSafeActivate(string operationId, string key)
		{
			FailSafeActivate?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(FailSafeActivate), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnFactorySyntheticTimeout(string operationId, string key)
		{
			FactorySyntheticTimeout?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(FactorySyntheticTimeout), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnFactoryError(string operationId, string key)
		{
			FactoryError?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(FactoryError), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnBackgroundFactoryError(string operationId, string key)
		{
			BackgroundFactoryError?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(BackgroundFactoryError), _logger, _errorsLogLevel, _syncExecution);
		}

		internal void OnBackgroundFactorySuccess(string operationId, string key)
		{
			BackgroundFactorySuccess?.SafeExecute(operationId, key, _cache, () => new FusionCacheEntryEventArgs(key), nameof(BackgroundFactorySuccess), _logger, _errorsLogLevel, _syncExecution);
		}
	}
}
