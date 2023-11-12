using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

/// <summary>
/// A Redis based implementation of a FusionCache backplane.
/// </summary>
public partial class RedisBackplane
	: IFusionCacheBackplane
{
	private readonly RedisBackplaneOptions _options;
	private BackplaneSubscriptionOptions? _subscriptionOptions;
	private readonly ILogger? _logger;

	private readonly SemaphoreSlim _connectionLock;
	private IConnectionMultiplexer? _connection;

	private string? _channelName = null;
	private RedisChannel _channel;
	private ISubscriber? _subscriber;

	private Action<BackplaneConnectionInfo>? _connectHandler;
	private Action<BackplaneMessage>? _incomingMessageHandler;

	/// <summary>
	/// Initializes a new instance of the RedisBackplane class.
	/// </summary>
	/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public RedisBackplane(IOptions<RedisBackplaneOptions> optionsAccessor, ILogger<RedisBackplane>? logger = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// LOGGING
		if (logger is NullLogger<RedisBackplaneOptions>)
		{
			// IGNORE NULL LOGGER (FOR BETTER PERF)
			_logger = null;
		}
		else
		{
			_logger = logger;
		}

		_connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
	}

	private ConfigurationOptions GetConfigurationOptions()
	{
		if (_options.ConfigurationOptions is not null)
			return _options.ConfigurationOptions;

		if (string.IsNullOrWhiteSpace(_options.Configuration) == false)
			return ConfigurationOptions.Parse(_options.Configuration!);

		throw new InvalidOperationException("Unable to connect to Redis: no Configuration nor ConfigurationOptions have been specified");
	}

	private void EnsureSubscriber()
	{
		if (_subscriber is null && _connection is not null)
			_subscriber = _connection.GetSubscriber();
	}

	private void OnReconnect(object sender, ConnectionFailedEventArgs e)
	{
		if (e.ConnectionType == ConnectionType.Subscription)
		{
			EnsureSubscriber();

			_connectHandler?.Invoke(new BackplaneConnectionInfo(true));
		}
	}

	private void Disconnect()
	{
		_connectHandler = null;

		if (_connection is null)
			return;

		try
		{
			_connection.ConnectionRestored -= OnReconnect;
			_connection.Dispose();
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION: An error occurred while disconnecting from Redis {Config}", _options.ConfigurationOptions?.ToString() ?? _options.Configuration);
		}

		_connection = null;
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions subscriptionOptions)
	{
		if (subscriptionOptions is null)
			throw new ArgumentNullException(nameof(subscriptionOptions));

		if (subscriptionOptions.ChannelName is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ChannelName cannot be null");

		if (subscriptionOptions.IncomingMessageHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.MessageHandler cannot be null");

		if (subscriptionOptions.ConnectHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ConnectHandler cannot be null");

		_subscriptionOptions = subscriptionOptions;

		_channelName = _subscriptionOptions.ChannelName;
		if (_channelName is null)
			throw new NullReferenceException("The backplane channel name is null");

		_channel = new RedisChannel(_channelName, RedisChannel.PatternMode.Literal);

		_incomingMessageHandler = _subscriptionOptions.IncomingMessageHandler;
		_connectHandler = _subscriptionOptions.ConnectHandler;

		// CONNECTION
		EnsureConnection();

		if (_subscriber is null)
			throw new NullReferenceException("The backplane subscriber is null");

		_subscriber.Subscribe(_channel, (_, v) =>
		{
			var message = GetMessageFromRedisValue(v, _logger);
			if (message is not null)
			{
				OnMessage(message);
			}
		});
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		_ = Task.Run(() =>
		{
			_incomingMessageHandler = null;
			_subscriber?.Unsubscribe(_channel);
			_subscriptionOptions = null;

			Disconnect();
		});
	}

	internal void OnMessage(BackplaneMessage message)
	{
		_incomingMessageHandler?.Invoke(message);
	}

	private static BackplaneMessage? GetMessageFromRedisValue(RedisValue value, ILogger? logger)
	{
		try
		{
			return BackplaneMessage.FromByteArray(value);
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION: an error occurred while converting a RedisValue into a BackplaneMessage");
		}

		return null;
	}

	private static RedisValue GetRedisValueFromMessage(BackplaneMessage message, ILogger? logger)
	{
		try
		{
			return BackplaneMessage.ToByteArray(message);
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION: an error occurred while converting a BackplaneMessage into a RedisValue");
		}

		return RedisValue.Null;
	}
}
