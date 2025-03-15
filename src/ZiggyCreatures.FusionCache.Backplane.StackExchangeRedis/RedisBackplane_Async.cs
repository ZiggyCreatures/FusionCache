using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public partial class RedisBackplane
{
	private async ValueTask EnsureConnectionAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_connection is not null)
			return;

		await _connectionLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			if (_connection is not null)
				return;

			if (_options.ConnectionMultiplexerFactory is not null)
			{
				_connection = await _options.ConnectionMultiplexerFactory().ConfigureAwait(false);
			}
			else
			{
				_connection = await ConnectionMultiplexer.ConnectAsync(GetConfigurationOptions());
			}

			if (_connection is not null)
			{
				_connection.ConnectionRestored += OnReconnect;
				var tmp = _connectHandlerAsync;
				if (tmp is not null)
				{
					await tmp(new BackplaneConnectionInfo(false)).ConfigureAwait(false);
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
	public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions subscriptionOptions)
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
		await EnsureConnectionAsync().ConfigureAwait(false);

		if (_subscriber is null)
			throw new NullReferenceException("The backplane subscriber is null");

		await _subscriber.SubscribeAsync(_channel, (rc, value) =>
		{
			var message = GetMessageFromRedisValue(value, _logger, _subscriptionOptions);
			if (message is null) return;

			_ = Task.Run(async () =>
			{
				try
				{
					await OnMessageAsync(message).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger?.LogError(ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error in incoming message handler", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
				}
			}, CancellationToken.None);
		});
	}

	/// <inheritdoc/>
	public async ValueTask UnsubscribeAsync()
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
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// CONNECTION
		await EnsureConnectionAsync(token).ConfigureAwait(false);

		var value = GetRedisValueFromMessage(message, _logger, _subscriptionOptions);

		if (value.IsNull)
			return;

		token.ThrowIfCancellationRequested();

		await _subscriber!.PublishAsync(_channel, value).ConfigureAwait(false);
	}

	private async ValueTask OnReconnectAsync(object sender, ConnectionFailedEventArgs e)
	{
		if (e.ConnectionType == ConnectionType.Subscription)
		{
			EnsureSubscriber();

			_connectHandler?.Invoke(new BackplaneConnectionInfo(true));
		}
	}

	internal async ValueTask OnMessageAsync(BackplaneMessage message)
	{
		var tmp = _incomingMessageHandlerAsync;
		if (tmp is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] incoming message handler was null", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			return;
		}

		await tmp(message).ConfigureAwait(false);
	}
}
