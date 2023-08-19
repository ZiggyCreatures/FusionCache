using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	private async ValueTask ExecuteOperationAsync(string operationId, string key, Func<CancellationToken, Task> action, string actionDescription, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return;

		token.ThrowIfCancellationRequested();

		var actionDescriptionInner = actionDescription + (options.AllowBackgroundBackplaneOperations ? " (background)" : null);

		await FusionCacheExecutionUtils
			.RunAsyncActionAdvancedAsync(
				action,
				Timeout.InfiniteTimeSpan,
				false,
				options.AllowBackgroundBackplaneOperations == false,
				exc => ProcessError(operationId, key, exc, actionDescriptionInner),
				false,
				token
			).ConfigureAwait(false)
		;
	}

	public async ValueTask<bool> PublishAsync(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, bool isFromAutoRecovery, CancellationToken token = default)
	{
		// IGNORE NULL
		if (message is null)
		{
			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a null backplane notification has been received (what!?)", _cache.CacheName, _cache.InstanceId, operationId);

			return false;
		}

		var cacheKey = message.CacheKey!;

		if (IsCurrentlyUsable(operationId, cacheKey) == false)
			return false;

		if (string.IsNullOrEmpty(message.SourceId))
		{
			// AUTO-ASSIGN LOCAL SOURCE ID
			message.SourceId = _cache.InstanceId;
		}
		else if (message.SourceId != _cache.InstanceId)
		{
			// IGNORE MESSAGES -NOT- FROM THIS SOURCE
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send a backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty) + " with a SourceId different than the local one (IFusionCache.InstanceId)", _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

			return false;
		}

		if (message.IsValid() == false)
		{
			// IGNORE INVALID MESSAGES
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send an invalid backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty), _cache.CacheName, _cache.InstanceId, operationId, cacheKey);

			return false;
		}

		token.ThrowIfCancellationRequested();

		if (isFromAutoRecovery == false)
		{
			TryRemoveAutoRecoveryItemByCacheKey(operationId, cacheKey);
		}

		await ExecuteOperationAsync(
			operationId,
			cacheKey,
			async ct =>
			{
				try
				{
					await _backplane.PublishAsync(message, options, ct).ConfigureAwait(false);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a notification has been sent" + (options.AllowBackgroundBackplaneOperations ? " in the background" : "") + (isFromAutoRecovery ? " (auto-recovery)" : "") + " ({Action})", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, message.Action);

					if (isFromAutoRecovery == false && _options.EnableBackplaneAutoRecovery)
					{
						TryProcessAutoRecoveryQueue(operationId);
					}
				}
				catch (Exception exc)
				{
					if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
						_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while sending a notification" + (options.AllowBackgroundBackplaneOperations ? " in the background" : "") + (isFromAutoRecovery ? " (auto-recovery)" : "") + " ({Action})", _cache.CacheName, _cache.InstanceId, operationId, cacheKey, message.Action);

					if (isFromAutoRecovery == false)
					{
						TryAddAutoRecoveryItem(operationId, message, options);
					}

					throw;
				}

				// EVENT
				_events.OnMessagePublished(operationId, message);
			},
			"sending a backplane notification" + (isFromAutoRecovery ? " (auto-recovery)" : ""),
			options,
			token
		).ConfigureAwait(false);

		return true;
	}
}
