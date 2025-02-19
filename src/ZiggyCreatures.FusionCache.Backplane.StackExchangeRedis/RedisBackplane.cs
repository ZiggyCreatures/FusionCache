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
	private Func<BackplaneConnectionInfo, ValueTask>? _connectHandlerAsync;
	private Func<BackplaneMessage, ValueTask>? _incomingMessageHandlerAsync;

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

	private void Disconnect()
	{
		_connectHandler = null;
		_connectHandlerAsync = null;

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
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] An error occurred while disconnecting from Redis {Config}", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId, _options.ConfigurationOptions?.ToString() ?? _options.Configuration);
		}

		_connection = null;
	}

	private static BackplaneMessage? GetMessageFromRedisValue(RedisValue value, ILogger? logger, BackplaneSubscriptionOptions? subscriptionOptions)
	{
		try
		{
			return BackplaneMessage.FromByteArray(value);
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] an error occurred while converting a RedisValue into a BackplaneMessage", subscriptionOptions?.CacheName, subscriptionOptions?.CacheInstanceId);
		}

		return null;
	}

	private static RedisValue GetRedisValueFromMessage(BackplaneMessage message, ILogger? logger, BackplaneSubscriptionOptions? subscriptionOptions)
	{
		try
		{
			return BackplaneMessage.ToByteArray(message);
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] an error occurred while converting a BackplaneMessage into a RedisValue", subscriptionOptions?.CacheName, subscriptionOptions?.CacheInstanceId);
		}

		return RedisValue.Null;
	}
}
