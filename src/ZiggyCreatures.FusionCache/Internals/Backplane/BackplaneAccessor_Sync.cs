﻿using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	private bool Publish(string operationId, FusionCacheAction action, BackplaneMessage message, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (CheckMessage(operationId, message, isAutoRecovery) == false)
			return false;

		var cacheKey = message.CacheKey!;

		// CHECK: CURRENTLY NOT USABLE
		if (IsCurrentlyUsable(operationId, cacheKey) == false)
		{
			if (isAutoRecovery == false)
			{
				_cache.TryAddAutoRecoveryItem(operationId, message.CacheKey, action, message.Timestamp, options, message);
			}

			return false;
		}

		token.ThrowIfCancellationRequested();

		if (isAutoRecovery == false)
		{
			_cache.TryRemoveAutoRecoveryItemByCacheKey(operationId, cacheKey);
		}

		var actionDescription = "sending a backplane notification" + isAutoRecovery.ToString(" (auto-recovery)") + isBackground.ToString(" (background)");

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);

			_backplane.Publish(message, options, token);

			// EVENT
			_events.OnMessagePublished(operationId, message);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, cacheKey, exc, actionDescription);

			if (isAutoRecovery == false)
			{
				_cache.TryAddAutoRecoveryItem(operationId, message.CacheKey, action, message.Timestamp, options, message);
			}

			if (exc is not SyntheticTimeoutException && options.ReThrowBackplaneExceptions)
			{
				throw;
			}

			return false;
		}

		return true;
	}

	public bool PublishSet(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = BackplaneMessage.CreateForEntrySet(_cache.InstanceId, key, timestamp);

		return Publish(operationId, FusionCacheAction.EntrySet, message, options, isAutoRecovery, isBackground, token);
	}

	public bool PublishRemove(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return Publish(operationId, FusionCacheAction.EntryRemove, message, options, isAutoRecovery, isBackground, token);
	}

	public bool PublishExpire(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = options.IsFailSafeEnabled
			? BackplaneMessage.CreateForEntryExpire(_cache.InstanceId, key, timestamp)
			: BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return Publish(operationId, FusionCacheAction.EntryExpire, message, options, isAutoRecovery, isBackground, token);
	}
}
