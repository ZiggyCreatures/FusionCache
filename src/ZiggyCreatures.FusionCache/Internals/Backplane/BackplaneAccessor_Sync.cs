using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane;

internal partial class BackplaneAccessor
{
	private bool Publish(string operationId, BackplaneMessage message, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		if (CheckMessage(operationId, message, isAutoRecovery) == false)
			return false;

		var cacheKey = message.CacheKey!;

		// CHECK: CURRENTLY NOT USABLE
		if (IsCurrentlyUsable(operationId, cacheKey) == false)
		{
			return false;
		}

		token.ThrowIfCancellationRequested();

		// ACTIVITY
		using var activity = Activities.SourceBackplane.StartActivityWithCommonTags(Activities.Names.BackplanePublish, _options.CacheName, _options.InstanceId!, message.CacheKey!, operationId);
		activity?.SetTag(Tags.Names.BackplaneMessageAction, message.Action.ToString());

		if (isAutoRecovery == false)
		{
			_cache.AutoRecovery.TryRemoveItemByCacheKey(operationId, cacheKey);
		}

		var actionDescription = "sending a backplane notification" + isAutoRecovery.ToString(" (auto-recovery)") + isBackground.ToString(" (background)");

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);

			_backplane.Publish(message, options, token);

			// EVENT
			_events.OnMessagePublished(operationId, message);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [BP] after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, cacheKey);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, cacheKey, exc, actionDescription);

			// ACTIVITY
			Activity.Current?.SetStatus(ActivityStatusCode.Error, exc.Message);
			Activity.Current?.AddException(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowBackplaneExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheBackplaneException("An error occurred while working with the backplane", exc);
				}
			}

			return false;
		}

		return true;
	}

	public bool PublishSet(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = BackplaneMessage.CreateForEntrySet(_cache.InstanceId, key, timestamp);

		return Publish(operationId, message, options, isAutoRecovery, isBackground, token);
	}

	public bool PublishRemove(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return Publish(operationId, message, options, isAutoRecovery, isBackground, token);
	}

	public bool PublishExpire(string operationId, string key, long timestamp, FusionCacheEntryOptions options, bool isAutoRecovery, bool isBackground, CancellationToken token)
	{
		var message = options.IsFailSafeEnabled
			? BackplaneMessage.CreateForEntryExpire(_cache.InstanceId, key, timestamp)
			: BackplaneMessage.CreateForEntryRemove(_cache.InstanceId, key, timestamp);

		return Publish(operationId, message, options, isAutoRecovery, isBackground, token);
	}
}
