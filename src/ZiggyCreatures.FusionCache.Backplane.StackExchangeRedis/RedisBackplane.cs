using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis
{
	/// <summary>
	/// A Redis based implementation of a FusionCache backplane.
	/// </summary>
	public class RedisBackplane
		: IFusionCacheBackplane
	{
		private const char _messageSeparator = ':';
		private static readonly char[] _messageSeparatorArray = new char[] { _messageSeparator };

		//private IFusionCache? _cache;
		private readonly RedisBackplaneOptions _options;
		private readonly SemaphoreSlim _connectionLock;
		private readonly ILogger? _logger;
		private IConnectionMultiplexer? _connection;
		private ISubscriber? _subscriber;
		private RedisChannel _channel;

		/// <summary>
		/// Initializes a new instance of the RedisBackplanePlugin class.
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
			if (_options.ConfigurationOptions is null && string.IsNullOrWhiteSpace(_options.Configuration))
				throw new InvalidOperationException("Unable to connect to Redis: no Configuration nor ConfigurationOptions have been specified");

			return _options.ConfigurationOptions ?? ConfigurationOptions.Parse(_options.Configuration);
		}

		private void EnsureConnection()
		{
			if (_connection is object)
				return;

			_connectionLock.Wait();
			try
			{
				if (_connection is object)
					return;

				_connection = ConnectionMultiplexer.Connect(GetConfigurationOptions());
			}
			finally
			{
				_connectionLock.Release();
			}

			if (_connection is null)
				throw new NullReferenceException("A connection to Redis is not available");

			OnAfterConnect();
		}

		private async ValueTask EnsureConnectionAsync(CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();

			if (_connection is object)
				return;

			await _connectionLock.WaitAsync(token).ConfigureAwait(false);
			try
			{
				if (_connection is object)
					return;

				_connection = await ConnectionMultiplexer.ConnectAsync(GetConfigurationOptions()).ConfigureAwait(false);
			}
			finally
			{
				_connectionLock.Release();
			}

			if (_connection is null)
				throw new NullReferenceException("A connection to Redis is not available");

			OnAfterConnect();
		}

		private void OnAfterConnect()
		{
			if (_subscriber is null)
				_subscriber = _connection!.GetSubscriber();
		}

		///// <inheritdoc/>
		//public void Start(IFusionCache cache)
		//{
		//	_ = Task.Run(async () =>
		//	{
		//		// CONNECTION
		//		await EnsureConnectionAsync().ConfigureAwait(false);

		//		// CHANNEL
		//		var _prefix = _options.ChannelPrefix;
		//		if (string.IsNullOrWhiteSpace(_prefix))
		//			_prefix = cache.CacheName;
		//		_channel = $"{_prefix}.Notifications";

		//		// LISTEN FOR REMOTE EVENTS TO PROPAGATE LOCALLY
		//		await SubscribeAsync(cache).ConfigureAwait(false);
		//	}).ConfigureAwait(false);
		//}

		///// <inheritdoc/>
		//public void Stop(IFusionCache cache)
		//{
		//	_ = Task.Run(() => Disconnect());
		//}

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

		//private async Task SubscribeAsync(IFusionCache cache)
		//{
		//	await EnsureConnectionAsync().ConfigureAwait(false);

		//	try
		//	{
		//		await _subscriber!.SubscribeAsync(_channel, (c, m) =>
		//		{
		//			var message = ParseMessage(m);
		//			if (message is object)
		//			{
		//				//cache.OnBackplaneNotification(message);
		//				OnMessage(message);
		//			}
		//		}).ConfigureAwait(false);
		//	}
		//	//catch (Exception exc)
		//	catch
		//	{
		//		//if (_logger?.IsEnabled(LogLevel.Error) ?? false)
		//		//	_logger.Log(LogLevel.Error, exc, "An error occurred while trying to subscribe for notifications to the Redis backplane");
		//	}
		//}

		/// <inheritdoc/>
		public event EventHandler<FusionCacheBackplaneMessageEventArgs>? Message;

		internal void OnMessage(BackplaneMessage message)
		{
			Message?.Invoke(this, new FusionCacheBackplaneMessageEventArgs(message));
		}

		/// <inheritdoc/>
		public void Subscribe(string channelName)
		{
			//_cache = cache;

			//// CHANNEL
			//var _prefix = _options.ChannelPrefix;
			//if (string.IsNullOrWhiteSpace(_prefix))
			//	_prefix = _cache.CacheName;
			//_channel = $"{_prefix}.Notifications";

			_channel = channelName;

			_ = Task.Run(async () =>
			{
				// CONNECTION
				await EnsureConnectionAsync().ConfigureAwait(false);

				await _subscriber!.SubscribeAsync(_channel, (c, m) =>
				{
					var message = ParseMessage(m);
					if (message is object)
					{
						//cache.OnBackplaneNotification(message);
						OnMessage(message);
					}
				}).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Unsubscribe()
		{
			_ = Task.Run(() => Disconnect());
		}

		/// <inheritdoc/>
		public async ValueTask SendNotificationAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
		{
			await EnsureConnectionAsync().ConfigureAwait(false);

			var redisMessage = CreateRedisMessage(message);

			try
			{
				var receivedAmount = await _subscriber!.PublishAsync(_channel, redisMessage).ConfigureAwait(false);
				if (receivedAmount == 0)
				{
					if (_logger?.IsEnabled(LogLevel.Error) ?? false)
						_logger.Log(LogLevel.Error, "An error occurred while trying to send a notification to the Redis backplane");

					return;
				}

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);
			}
			//catch (Exception exc)
			catch
			{
				//if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				//	_logger.Log(LogLevel.Error, exc, "An error occurred while trying to send a notification to the Redis backplane");
			}
		}

		/// <inheritdoc/>
		public void SendNotification(BackplaneMessage message, FusionCacheEntryOptions options)
		{
			EnsureConnection();

			var redisMessage = CreateRedisMessage(message);

			try
			{
				var receivedAmount = _subscriber!.Publish(_channel, redisMessage);
				if (receivedAmount == 0)
				{
					if (_logger?.IsEnabled(LogLevel.Error) ?? false)
						_logger.Log(LogLevel.Error, "An error occurred while trying to send a notification to the Redis backplane");

					return;
				}

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);
			}
			//catch (Exception exc)
			catch
			{
				//if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				//	_logger.Log(LogLevel.Error, exc, "An error occurred while trying to send a notification to the Redis backplane");
			}
		}
	}
}
