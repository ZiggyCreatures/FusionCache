using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory;

public partial class MemoryBackplane
{
	/// <inheritdoc/>
	public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions subscriptionOptions)
	{
		Subscribe(subscriptionOptions);
	}

	/// <inheritdoc/>
	public async ValueTask UnsubscribeAsync()
	{
		if (_subscribers is not null)
		{
			_incomingMessageHandler = null;
			_incomingMessageHandlerAsync = null;

			lock (_subscribers)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] before unsubscribing (Subscribers: {SubscribersCount})", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, _subscribers.Count);

				var removed = _subscribers.Remove(this);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] after unsubscribing (Subscribers: {SubscribersCount}, Removed: {Removed})", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, _subscribers.Count, removed);

				_subscriptionOptions = null;
				_channelName = null;

				_incomingMessageHandler = null;
				_connectHandler = null;
				_incomingMessageHandlerAsync = null;
				_connectHandlerAsync = null;
			}

			_subscribers = null;
		}

		Disconnect();
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		EnsureConnection();

		if (message is null)
			throw new ArgumentNullException(nameof(message));

		if (message.IsValid() == false)
			throw new InvalidOperationException("The message is invalid");

		if (_subscribers is null)
			throw new NullReferenceException("Something went wrong :-|");

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] about to send a backplane notification to {BackplanesCount} backplanes (including self)", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, _subscribers.Count);

		var payload = BackplaneMessage.ToByteArray(message);

		foreach (var backplane in _subscribers)
		{
			token.ThrowIfCancellationRequested();

			if (backplane == this)
				continue;

			try
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] before sending a backplane notification to channel {BackplaneChannel}", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, backplane._channelName);

				await backplane.OnMessageAsync(payload).ConfigureAwait(false);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] after sending a backplane notification to channel {BackplaneChannel}", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, backplane._channelName);
			}
			catch
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] An error occurred while publishing a message to a subscriber", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey);
			}
		}
	}

	internal async ValueTask OnMessageAsync(byte[] payload)
	{
		var message = BackplaneMessage.FromByteArray(payload);

		var handler = _incomingMessageHandlerAsync;

		if (handler is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] a backplane notification received from {BackplaneMessageSourceId} will not be processed because the handler is null", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, message.SourceId);
		}
		else
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] before processing a backplane notification received from {BackplaneMessageSourceId}", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, message.SourceId);

			await handler(message).ConfigureAwait(false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (K={CacheKey}): [BP] after processing a backplane notification received from {BackplaneMessageSourceId}", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, message.CacheKey, message.SourceId);
		}
	}
}
