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
			Memory = new FusionCacheMemoryEventsHub(_cache, _options, _logger);
			Distributed = new FusionCacheDistributedEventsHub(_cache, _options, _logger);
		}

		public FusionCacheMemoryEventsHub Memory { get; }
		public FusionCacheDistributedEventsHub Distributed { get; }

		public event EventHandler<FusionCacheEntryEventArgs> FailSafeActivate;
		public event EventHandler<FusionCacheEntryEventArgs> FactorySyntheticTimeout;
		public event EventHandler<FusionCacheEntryEventArgs> FactoryError;
		public event EventHandler<FusionCacheEntryEventArgs> BackgroundFactoryError;
		public event EventHandler<FusionCacheEntryEventArgs> BackgroundFactorySuccess;

		internal void OnFailSafeActivate(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, FailSafeActivate, () => new FusionCacheEntryEventArgs(key), nameof(FailSafeActivate), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnFactorySyntheticTimeout(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, FactorySyntheticTimeout, () => new FusionCacheEntryEventArgs(key), nameof(FactorySyntheticTimeout), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnFactoryError(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, FactoryError, () => new FusionCacheEntryEventArgs(key), nameof(FactoryError), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnBackgroundFactoryError(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, BackgroundFactoryError, () => new FusionCacheEntryEventArgs(key), nameof(BackgroundFactoryError), _logger, _options.EventHandlingErrorsLogLevel);
		}

		internal void OnBackgroundFactorySuccess(string operationId, string key)
		{
			FusionCacheInternalUtils.SafeExecuteEvent(operationId, key, _cache, BackgroundFactorySuccess, () => new FusionCacheEntryEventArgs(key), nameof(BackgroundFactorySuccess), _logger, _options.EventHandlingErrorsLogLevel);
		}
	}
}
