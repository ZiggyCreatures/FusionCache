﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	private async ValueTask<bool> PublishAsync(string operationId, FusionCacheAction action, BackplaneMessage message, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (CheckMessage(operationId, message, isAutoRecovery) == false)
			return false;

		var cacheKey = message.CacheKey!;

		// CHECK: CURRENTLY NOT USABLE
		if (IsCurrentlyUsable(operationId, cacheKey) == false)
		{
			if (isAutoRecovery == false)
			{
				_ = _cache.TryAddAutoRecoveryItem(operationId, message.CacheKey, action, options, message);
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
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): before " + actionDescription, _options.CacheName, operationId, cacheKey);

			//await FusionCacheExecutionUtils.RunAsyncActionWithTimeoutAsync(
			//	async ct =>
			//	{
			//		await _backplane.PublishAsync(message, options, ct).ConfigureAwait(false);

			//		// EVENT
			//		_events.OnMessagePublished(operationId, message);
			//	},
			//	timeout,
			//	true,
			//	token: token
			//).ConfigureAwait(false);

			await _backplane.PublishAsync(message, options, token).ConfigureAwait(false);

			// EVENT
			_events.OnMessagePublished(operationId, message);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): after " + actionDescription, _options.CacheName, operationId, cacheKey);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, cacheKey, exc, actionDescription);

			if (isAutoRecovery == false)
			{
				_ = _cache.TryAddAutoRecoveryItem(operationId, message.CacheKey, action, options, message);
			}

			if (exc is not SyntheticTimeoutException && options.ReThrowBackplaneExceptions)
			{
				throw;
			}

			return false;
		}

		return true;
	}

	public async ValueTask<bool> PublishSetAsync(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		// TODO: MAYBE REMOVE ASYNC/AWAIT HERE

		//if (options.SkipBackplaneNotifications)
		//	return false;

		var message = BackplaneMessage.CreateForEntrySet(_cache.InstanceId, key, timestamp);

		return await PublishAsync(operationId, FusionCacheAction.EntrySet, message, options, isAutoRecovery, isBackground, token).ConfigureAwait(false);
	}

	public async ValueTask<bool> PublishRemoveAsync(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		// TODO: MAYBE REMOVE ASYNC/AWAIT HERE

		//if (options.SkipBackplaneNotifications)
		//	return false;

		var message = BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return await PublishAsync(operationId, FusionCacheAction.EntryRemove, message, options, isAutoRecovery, isBackground, token).ConfigureAwait(false);
	}

	public async ValueTask<bool> PublishExpireAsync(string operationId, string key, long? timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		// TODO: MAYBE REMOVE ASYNC/AWAIT HERE

		//if (options.SkipBackplaneNotifications)
		//	return false;

		var message = options.IsFailSafeEnabled
			? BackplaneMessage.CreateForEntryExpire(_cache.InstanceId, key, timestamp)
			: BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return await PublishAsync(operationId, FusionCacheAction.EntryExpire, message, options, isAutoRecovery, isBackground, token).ConfigureAwait(false);
	}
}
