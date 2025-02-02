using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public partial class RedisBackplane
{
	private void EnsureConnection(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_connection is not null)
			return;

		_connectionLock.Wait(token);
		try
		{
			if (_connection is not null)
				return;

			if (_options.ConnectionMultiplexerFactory is not null)
			{
				_connection = _options.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
			}
			else
			{
				_connection = ConnectionMultiplexer.Connect(GetConfigurationOptions());
			}

			if (_connection is not null)
			{
				_connection.ConnectionRestored += OnReconnect;
				var tmp = _connectHandler;
				if (tmp is not null)
				{
					tmp(new BackplaneConnectionInfo(false));
				}

			}
		}
		finally
		{
			_connectionLock.Release();
		}

		if (_connection is null)
			throw new NullReferenceException("A connection to Redis is not available");

		EnsureSubscriber();
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions subscriptionOptions)
	{
		if (subscriptionOptions is null)
			throw new ArgumentNullException(nameof(subscriptionOptions));

		if (subscriptionOptions.ChannelName is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ChannelName cannot be null");

		if (subscriptionOptions.IncomingMessageHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.IncomingMessageHandler cannot be null");

		if (subscriptionOptions.ConnectHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ConnectHandler cannot be null");

		if (subscriptionOptions.IncomingMessageHandlerAsync is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.IncomingMessageHandlerAsync cannot be null");

		if (subscriptionOptions.ConnectHandlerAsync is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ConnectHandlerAsync cannot be null");

		_subscriptionOptions = subscriptionOptions;

		_channelName = _subscriptionOptions.ChannelName;
		if (_channelName is null)
			throw new NullReferenceException("The backplane channel name is null");

		_channel = new RedisChannel(_channelName, RedisChannel.PatternMode.Literal);

		_incomingMessageHandler = _subscriptionOptions.IncomingMessageHandler;
		_connectHandler = _subscriptionOptions.ConnectHandler;
		_incomingMessageHandlerAsync = _subscriptionOptions.IncomingMessageHandlerAsync;
		_connectHandlerAsync = _subscriptionOptions.ConnectHandlerAsync;

		// CONNECTION
		EnsureConnection();

		if (_subscriber is null)
			throw new NullReferenceException("The backplane subscriber is null");

		_subscriber.Subscribe(_channel, (_, v) =>
		{
			var message = GetMessageFromRedisValue(v, _logger, _subscriptionOptions);

			if (message is null)
				return;

			OnMessage(message);
		});

		//_subscriber.SubscribeAsync(_channel, (_, v) =>
		//{
		//	var message = GetMessageFromRedisValue(v, _logger, _subscriptionOptions);

		//	if (message is null)
		//		return;

		//	OnMessage(message);
		//});
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		_ = Task.Run(() =>
		{
			_incomingMessageHandler = null;
			_incomingMessageHandlerAsync = null;
			_subscriber?.Unsubscribe(_channel);
			_subscriptionOptions = null;

			Disconnect();
		});
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// CONNECTION
		EnsureConnection(token);

		var value = GetRedisValueFromMessage(message, _logger, _subscriptionOptions);

		if (value.IsNull)
			return;

		token.ThrowIfCancellationRequested();

		_subscriber!.Publish(_channel, value);
	}

	private void OnReconnect(object sender, ConnectionFailedEventArgs e)
	{
		if (e.ConnectionType == ConnectionType.Subscription)
		{
			EnsureSubscriber();

			_connectHandler?.Invoke(new BackplaneConnectionInfo(true));
		}
	}

	internal void OnMessage(BackplaneMessage message)
	{
		var tmp = _incomingMessageHandler;
		if (tmp is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] incoming message handler was null", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			return;
		}

		tmp(message);
	}
}
