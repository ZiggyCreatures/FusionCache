using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane
{
	internal sealed partial class BackplaneAccessor
	{
		private readonly FusionCache _cache;
		private readonly IFusionCacheBackplane _backplane;
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly FusionCacheBackplaneEventsHub _events;
		private readonly SimpleCircuitBreaker _breaker;

		public BackplaneAccessor(FusionCache cache, IFusionCacheBackplane backplane, FusionCacheOptions options, ILogger? logger, FusionCacheBackplaneEventsHub events)
		{
			if (cache is null)
				throw new ArgumentNullException(nameof(cache));

			if (backplane is null)
				throw new ArgumentNullException(nameof(backplane));

			_cache = cache;
			_backplane = backplane;

			_options = options;

			_logger = logger;
			_events = events;

			// CIRCUIT-BREAKER
			_breaker = new SimpleCircuitBreaker(options.BackplaneCircuitBreakerDuration);
		}

		private void UpdateLastError(string key, string operationId)
		{
			// NO DISTRIBUTEC CACHE
			if (_backplane is null)
				return;

			var res = _breaker.TryOpen(out var hasChanged);

			if (res && hasChanged)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION (O={CacheOperationId} K={CacheKey}): backplane temporarily de-activated for {BreakDuration}", operationId, key, _breaker.BreakDuration);

				// EVENT
				_events.OnCircuitBreakerChange(operationId, key, false);
			}
		}

		public bool IsCurrentlyUsable(string? operationId, string? key)
		{
			var res = _breaker.IsClosed(out var hasChanged);

			if (res && hasChanged)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION (O={CacheOperationId} K={CacheKey}): backplane activated again", operationId, key);

				// EVENT
				_events.OnCircuitBreakerChange(operationId, key, true);
			}

			return res;
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessError(string operationId, string key, Exception exc, string actionDescription)
		{
			//if (exc is SyntheticTimeoutException)
			//{
			//	if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
			//		_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, operationId, key);

			//	return;
			//}

			UpdateLastError(key, operationId);

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, operationId, key);
		}

		public void Subscribe()
		{
			_backplane.Subscribe(
				new BackplaneSubscriptionOptions
				{
					ChannelName = _options.GetBackplaneChannelName(),
					Handler = ProcessMessage
				}
			);
		}

		public void Unsubscribe()
		{
			_backplane.Unsubscribe();
		}

		private void ProcessMessage(BackplaneMessage message)
		{
			// IGNORE INVALID MESSAGES
			if (message is null || message.IsValid() == false)
			{
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "An invalid message has been received on the backplane from cache {CacheInstanceId} for key {CacheKey} and action {Action}", message?.SourceId, message?.CacheKey, message?.Action);

				return;
			}

			// IGNORE MESSAGES FROM THIS SOURCE
			if (message.SourceId == _cache.InstanceId)
				return;

			switch (message.Action)
			{
				case BackplaneMessageAction.EntrySet:
					_cache.Evict(message.CacheKey!, true);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "A backplane notification has been received for {CacheKey} (SET)", message.CacheKey);
					break;
				case BackplaneMessageAction.EntryRemove:
					_cache.Evict(message.CacheKey!, false);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "A backplane notification has been received for {CacheKey} (REMOVE)", message.CacheKey);
					break;
				default:
					if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
						_logger.Log(_options.BackplaneErrorsLogLevel, "An unknown backplane notification has been received for {CacheKey}: {Type}", message.CacheKey, message.Action);
					break;
			}

			// EVENT
			_events.OnMessageReceived("", message);
		}
	}
}
