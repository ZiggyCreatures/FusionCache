﻿using System;
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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send a backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty) + " with a SourceId different than the local one (IFusionCache.InstanceId)", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		if (message.IsValid() == false)
		{
			// IGNORE INVALID MESSAGES
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot send an invalid backplane message" + (isFromAutoRecovery ? " (auto-recovery)" : String.Empty), _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey);

			return false;
		}

		token.ThrowIfCancellationRequested();

		if (isFromAutoRecovery == false)
		{
			TryRemoveAutoRecoveryItemByCacheKey(operationId, message.CacheKey);
		}

		await ExecuteOperationAsync(
			operationId,
			message.CacheKey!,
			async ct =>
			{
				try
				{
					// IF:
					// - THE MESSAGE IS FROM AUTO-RECOVERY
					// - AND EnableDistributedExpireOnBackplaneAutoRecovery IS ENABLED
					// - AND THERE IS A DISTRIBUTED CACHE
					// THEN:
					// - REMOVE THE ENTRY (BUT ONLY FROM THE DISTRIBUTED CACHE)
					if (isFromAutoRecovery && _options.EnableDistributedExpireOnBackplaneAutoRecovery && _cache.HasDistributedCache)
					{
						//await _cache.ExpireAsync(message.CacheKey!, _autoRecoveryEntryOptions, ct).ConfigureAwait(false);
						var dca = _cache.GetCurrentDistributedAccessor(_autoRecoveryEntryOptions);
						if (dca.CanBeUsed(operationId, message.CacheKey))
						{
							await dca!.RemoveEntryAsync(operationId, message.CacheKey!, _autoRecoveryEntryOptions, ct).ConfigureAwait(false);
						}
					}

					await _backplane.PublishAsync(message, options, ct).ConfigureAwait(false);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a notification has been sent" + (options.AllowBackgroundBackplaneOperations ? " in the background" : "") + (isFromAutoRecovery ? " (auto-recovery)" : "") + " ({Action})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.Action);

					if (isFromAutoRecovery == false && _options.EnableBackplaneAutoRecovery)
					{
						TryProcessAutoRecoveryQueue(operationId);
					}
				}
				catch (Exception exc)
				{
					if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
						_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while sending a notification" + (options.AllowBackgroundBackplaneOperations ? " in the background" : "") + (isFromAutoRecovery ? " (auto-recovery)" : "") + " ({Action})", _cache.CacheName, _cache.InstanceId, operationId, message.CacheKey, message.Action);

					if (isFromAutoRecovery == false && _options.EnableBackplaneAutoRecovery)
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
