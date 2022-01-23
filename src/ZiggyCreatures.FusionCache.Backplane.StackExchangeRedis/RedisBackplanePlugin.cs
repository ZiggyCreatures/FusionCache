using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis
{
	/// <summary>
	/// A Redis based implementation of a FusionCache backplane.
	/// </summary>
	public class RedisBackplanePlugin
		: IFusionCacheBackplane
	{
		private const char _messageSeparator = ':';
		private static readonly char[] _messageSeparatorArray = new char[] { _messageSeparator };

		private readonly RedisBackplaneOptions _options;
		private readonly ILogger? _logger;
		private IConnectionMultiplexer? _connection;
		private ISubscriber? _subscriber;
		private RedisChannel _channel;
		private readonly SimpleCircuitBreaker _breaker;

		/// <summary>
		/// Initializes a new instance of the RedisBackplanePlugin class.
		/// </summary>
		/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
		/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
		public RedisBackplanePlugin(IOptions<RedisBackplaneOptions> optionsAccessor, ILogger<RedisBackplanePlugin>? logger = null)
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

			// CIRCUIT-BREAKER
			_breaker = new SimpleCircuitBreaker(_options.CircuitBreakerDuration);
		}

		private async ValueTask EnsureConnectionAsync()
		{
			try
			{
				if (_connection is object)
					return;

				var co = GetConfigurationOptions();

				_connection = await ConnectionMultiplexer.ConnectAsync(co).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				UpdateLastError();

				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError(exc, "An error occurred while connecting to the Redis backplane");
			}
		}

		/// <inheritdoc/>
		public void Start(IFusionCache cache)
		{
			_ = Task.Run(async () =>
			{
				// CONNECTION
				try
				{
					if (_connection is object)
						return;

					var co = GetConfigurationOptions();

					_connection = await ConnectionMultiplexer.ConnectAsync(co).ConfigureAwait(false);
				}
				catch (Exception exc)
				{
					UpdateLastError();

					if (_logger?.IsEnabled(LogLevel.Error) ?? false)
						_logger.LogError(exc, "An error occurred while connecting to the Redis backplane");
				}

				if (_connection is null)
					throw new NullReferenceException("A connection to Redis is not available");

				// CHANNEL
				var _prefix = _options.ChannelPrefix;
				if (string.IsNullOrWhiteSpace(_prefix))
					_prefix = cache.CacheName;
				_channel = $"{_prefix}.Evict";

				// SUBSCRIBER
				_subscriber = _connection.GetSubscriber();

				// LISTEN FOR REMOTE EVENTS TO PROPAGATE LOCALLY
				await StartListeningForRemoteEvictionsAsync(cache).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			_ = Task.Run(() => Disconnect());
		}

		private void UpdateLastError()
		{
			var res = _breaker.TryOpen(out var hasChanged);

			if (res && hasChanged)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION: Redis backplane temporarily de-activated for {BreakDuration}", _breaker.BreakDuration);
			}
		}

		private bool IsCurrentlyUsable()
		{
			var res = _breaker.IsClosed(out var hasChanged);

			if (res && hasChanged)
			{
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger.LogWarning("FUSION: Redis backplane activated again");
			}

			return res;
		}

		private ConfigurationOptions GetConfigurationOptions()
		{
			if (_options.ConfigurationOptions is null && string.IsNullOrWhiteSpace(_options.Configuration))
				throw new InvalidOperationException("Unable to connect to Redis: no Configuration nor ConfigurationOptions have been specified");

			var res = _options.ConfigurationOptions;

			if (res is null)
				res = ConfigurationOptions.Parse(_options.Configuration);

			return res;
		}

		private void Disconnect()
		{
			if (_connection is null)
				return;

			try
			{
				_connection.Dispose();
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "An error occurred while disconnecting from Redis");
			}

			_connection = null;
		}

		private static string GetActionString(BackplaneMessageAction action)
		{
			switch (action)
			{
				case BackplaneMessageAction.Evict:
					return string.Empty;
				default:
					throw new InvalidOperationException($"Unknown backplane action {action}");
			}
		}

		private static BackplaneMessageAction ParseAction(string? s)
		{
			if (string.IsNullOrEmpty(s))
				return BackplaneMessageAction.Evict;

			throw new InvalidOperationException($"Unknown backplane action \"{s}\"");
		}

		private static RedisValue CreateRedisMessage(BackplaneMessage message)
		{
			return message.SourceId + _messageSeparator + GetActionString(message.Action) + _messageSeparator + message.CacheKey;
		}

		private static BackplaneMessage? ParseMessage(RedisValue redisMessage)
		{
			var payload = (string)redisMessage;
			var parts = payload.Split(_messageSeparatorArray, 3, StringSplitOptions.None);

			if (parts.Length < 3)
				return null;

			return BackplaneMessage.Create(parts[0], ParseAction(parts[1]), parts[2]);
		}

		private async Task StartListeningForRemoteEvictionsAsync(IFusionCache cache)
		{
			if (_subscriber is null)
				return;

			try
			{
				await _subscriber.SubscribeAsync(_channel, (c, m) =>
				{
					var message = ParseMessage(m);
					if (message is object)
						cache.OnBackplaneNotification(message);
				}).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				UpdateLastError();

				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "An error occurred while trying to subscribe for notifications to the Redis backplane");
			}
		}

		/// <inheritdoc/>
		public void SendNotification(IFusionCache cache, BackplaneMessage message)
		{
			if (_subscriber is null)
				return;

			if (IsCurrentlyUsable() == false)
				return;

			if (_connection is object && _connection.IsConnected == false)
			{
				UpdateLastError();
				return;
			}

			var redisMessage = CreateRedisMessage(message);

			try
			{
				if (_options.AllowBackgroundOperations)
				{
					// FIRE AND FORGET

					_subscriber.Publish(_channel, redisMessage, CommandFlags.FireAndForget);
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey} (maybe)", message.CacheKey);
				}
				else
				{
					// WAITED

					var receivedAmount = _subscriber.Publish(_channel, redisMessage);
					if (receivedAmount == 0)
					{
						UpdateLastError();

						if (_logger?.IsEnabled(LogLevel.Error) ?? false)
							_logger.Log(LogLevel.Error, "An error occurred while trying to send a notification to the Redis backplane");

						return;
					}

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);
				}
			}
			catch (Exception exc)
			{
				UpdateLastError();

				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "An error occurred while trying to send a notification to the Redis backplane");
			}
		}

		/// <inheritdoc/>
		public async ValueTask SendNotificationAsync(IFusionCache cache, BackplaneMessage message, CancellationToken token)
		{
			if (_subscriber is null)
				return;

			if (IsCurrentlyUsable() == false)
				return;

			if (_connection is object && _connection.IsConnected == false)
			{
				UpdateLastError();
				return;
			}

			var redisMessage = CreateRedisMessage(message);

			try
			{
				if (_options.AllowBackgroundOperations)
				{
					// FIRE AND FORGET

					await _subscriber.PublishAsync(_channel, redisMessage, CommandFlags.FireAndForget).ConfigureAwait(false);
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey} (maybe)", message.CacheKey);
				}
				else
				{
					// WAITED

					var receivedAmount = await _subscriber.PublishAsync(_channel, redisMessage).ConfigureAwait(false);
					if (receivedAmount == 0)
					{
						UpdateLastError();

						if (_logger?.IsEnabled(LogLevel.Error) ?? false)
							_logger.Log(LogLevel.Error, "An error occurred while trying to send a notification to the Redis backplane");

						return;
					}

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);
				}
			}
			catch (Exception exc)
			{
				UpdateLastError();

				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "An error occurred while trying to send a notification to the Redis backplane");
			}
		}
	}
}
