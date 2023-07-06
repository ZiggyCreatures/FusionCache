﻿using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	private void ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return;

		token.ThrowIfCancellationRequested();

		var actionDescriptionInner = actionDescription + (options.AllowBackgroundBackplaneOperations ? " (background)" : null);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): " + actionDescriptionInner, _cache.CacheName, operationId, key);

		FusionCacheExecutionUtils
			.RunSyncActionAdvanced(
				action,
				Timeout.InfiniteTimeSpan,
				false,
				options.AllowBackgroundBackplaneOperations == false,
				exc => ProcessError(operationId, key, exc, actionDescriptionInner),
				false,
				token
			)
		;
	}

	public bool Publish(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, bool isFromAutoRecovery, CancellationToken token = default)
	{
		if (IsCurrentlyUsable(operationId, message.CacheKey) == false)
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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): cannot send a backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty) + " with a SourceId different than the local one (IFusionCache.InstanceId)", _cache.CacheName, operationId, message.CacheKey);

			return false;
		}

		if (message.IsValid() == false)
		{
			// IGNORE INVALID MESSAGES
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): cannot send an invalid backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty), _cache.CacheName, operationId, message.CacheKey);

			return false;
		}

		token.ThrowIfCancellationRequested();

		ExecuteOperation(
			operationId,
			message.CacheKey!,
			_ =>
			{
				try
				{
					_backplane.Publish(message, options);

					if (isFromAutoRecovery == false && _options.EnableBackplaneAutoRecovery)
					{
						TryProcessAutoRecoveryQueue();
					}
				}
				catch
				{
					if (isFromAutoRecovery == false && _options.EnableBackplaneAutoRecovery)
					{
						TryAddAutoRecoveryItem(message, options);
					}

					throw;
				}

				// EVENT
				_events.OnMessagePublished(operationId, message);
			},
			"sending a backplane notification" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty),
			options,
			token
		);

		return true;
	}
}
