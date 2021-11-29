using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Plugins.StackExchangeRedisBackplane
{
	/// <summary>
	/// A Redis based implementation of a FusionCache backplane.
	/// </summary>
	public class RedisBackplanePlugin
		: IFusionCachePlugin
	{
		private const char _messageSeparator = ':';
		private static readonly char[] _messageSeparatorArray = new char[] { _messageSeparator };

		private readonly RedisBackplaneOptions _options;
		private readonly ILogger? _logger;
		private IConnectionMultiplexer? _connection;
		private ISubscriber? _subscriber;
		private RedisChannel _channel;

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
					if (_logger?.IsEnabled(LogLevel.Error) ?? false)
						_logger.LogError(exc, "An error occurred while connecting to the Redis backplane");

					throw;
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

			// LISTEN FOR LOCAL EVENTS TO PROPAGATE REMOTELY
			cache.Events.Set += OnSet;
			cache.Events.Remove += OnRemove;
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			cache.Events.Set -= OnSet;
			cache.Events.Remove -= OnRemove;

			_ = Task.Run(() =>
			{
				Disconnect();
			});
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

		private void OnSet(object sender, Events.FusionCacheEntryEventArgs e)
		{
			_ = NotifyEvictionAsync((IFusionCache)sender, e.Key, string.Empty);
		}

		private void OnRemove(object sender, Events.FusionCacheEntryEventArgs e)
		{
			_ = NotifyEvictionAsync((IFusionCache)sender, e.Key, string.Empty);
		}

		private static RedisValue CreateMessage(string instanceId, string action, string cacheKey)
		{
			return instanceId + _messageSeparator + action + _messageSeparator + cacheKey;
		}

		private static (string? InstanceId, string? Action, string? CacheKey) ParseMessage(RedisValue message)
		{
			var payload = (string)message;
			var parts = payload.Split(_messageSeparatorArray, 3, StringSplitOptions.None);

			if (parts.Length < 3)
				return (null, null, null);

			return (parts[0], parts[1], parts[2]);
		}

		private async Task NotifyEvictionAsync(IFusionCache cache, string cacheKey, string type)
		{
			if (_subscriber is null)
				return;

			var message = CreateMessage(cache.InstanceId, type, cacheKey);

			await _subscriber.PublishAsync(_channel, message, CommandFlags.FireAndForget).ConfigureAwait(false);

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", cacheKey);
		}

		private async Task StartListeningForRemoteEvictionsAsync(IFusionCache cache)
		{
			if (_subscriber is null)
				return;

			await _subscriber.SubscribeAsync(_channel, (c, m) =>
			{
				var (instanceId, type, cacheKey) = ParseMessage(m);

				// IGNORE INVALID MESSAGES
				if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(cacheKey))
					return;

				// IGNORE MESSAGES FROM THIS SOURCE
				if (instanceId == cache.InstanceId)
					return;

				if (string.IsNullOrWhiteSpace(type))
				{
					cache.Evict(cacheKey!);

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An eviction notification has been received for {CacheKey}", cacheKey);
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "An unknown notification has been received for {CacheKey}: {Type}", cacheKey, type);
				}
			}).ConfigureAwait(false);
		}
	}
}
