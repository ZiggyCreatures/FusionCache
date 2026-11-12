using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

public partial class AzureServiceBusBackplane
{
	/// <inheritdoc/>
	public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions options)
	{
		if (options is null)
			throw new ArgumentNullException(nameof(options));
		if (options.ChannelName is null)
			throw new NullReferenceException($"The {nameof(BackplaneSubscriptionOptions)}.{nameof(options.ChannelName)} cannot be null");

		if (options.CacheName is null)
			throw new NullReferenceException($"The {nameof(BackplaneSubscriptionOptions)}.{nameof(options.CacheName)} cannot be null");

		if (options.CacheInstanceId is null)
			throw new NullReferenceException($"The {nameof(BackplaneSubscriptionOptions)}.{nameof(options.CacheInstanceId)} cannot be null");

		if (options.IncomingMessageHandler is null && options.IncomingMessageHandlerAsync is null)
			throw new ArgumentException("At least one of the incoming message handlers must be provided.");

		if (!await _lock.WaitAsync(_lockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
			await _serviceBusAdminWrapper.EnsureTopicAsync();
			await _serviceBusAdminWrapper.EnsureSubscriptionAsync();

			_cacheName = options.CacheName;
			_cacheInstanceId = options.CacheInstanceId;
			_incomingMessageHandler = async serviceBusMessage =>
			{
				if (serviceBusMessage.Subject != _cacheName)
					return;

				var data = serviceBusMessage.Body.ToArray();
				var msg = BackplaneMessage.FromByteArray(data);

				if (options.IncomingMessageHandlerAsync is not null)
					await options.IncomingMessageHandlerAsync(msg);
				else
					options.IncomingMessageHandler?.Invoke(msg);
			};

			_subscriptionMissingHandler = () => _serviceBusAdminWrapper.EnsureSubscriptionAsync().AsTask();
			_serviceBusClientWrapper.SubscriptionMissing += _subscriptionMissingHandler;

			await _serviceBusClientWrapper.Subscribe(_incomingMessageHandler);

			if (options.ConnectHandlerAsync is not null)
				await options.ConnectHandlerAsync(new BackplaneConnectionInfo(false));
			else
				options.ConnectHandler?.Invoke(new BackplaneConnectionInfo(false));
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] new message {Action} {CacheKey} - {Duration} - {DistributedDuration}", _cacheName, _cacheInstanceId, message.Action, message.CacheKey, options.Duration, options.DistributedCacheDuration);

		var asb_message = new ServiceBusMessage
		{
			Body = new BinaryData(BackplaneMessage.ToByteArray(message)),
			Subject = _cacheName
		};
		await _serviceBusClientWrapper.SendMessage(asb_message, token);
	}

	/// <inheritdoc/>
	public async ValueTask UnsubscribeAsync()
	{
		if (!await _lock.WaitAsync(_lockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
		if (_incomingMessageHandler is null)
			return;

		if (_subscriptionMissingHandler is not null)
		{
			_serviceBusClientWrapper.SubscriptionMissing -= _subscriptionMissingHandler;
			_subscriptionMissingHandler = null;
		}

		await _serviceBusClientWrapper.Unsubscribe(_incomingMessageHandler);
		_incomingMessageHandler = null;
		_cacheName = null;
		_cacheInstanceId = null;

		await _serviceBusClientWrapper.DisposeAsync();
		await _serviceBusAdminWrapper.DisposeAsync();
		}
		finally
		{
			_lock.Release();
		}
	}
}
