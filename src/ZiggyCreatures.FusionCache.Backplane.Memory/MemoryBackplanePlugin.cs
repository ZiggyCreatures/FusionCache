using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory
{
	/// <summary>
	/// An-in memory implementation of a FusionCache backplane
	/// </summary>
	public class MemoryBackplanePlugin
		: IFusionCachePlugin
		, IFusionCacheBackplane
	{
		private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IFusionCache>> _channels = new ConcurrentDictionary<string, ConcurrentDictionary<string, IFusionCache>>();

		private readonly MemoryBackplaneOptions _options;
		private readonly ILogger? _logger;
		private string _channel = "FusionCache.Evict";

		/// <summary>
		/// Initializes a new instance of the MemoryBackplanePlugin class.
		/// </summary>
		/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
		/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
		public MemoryBackplanePlugin(IOptions<MemoryBackplaneOptions> optionsAccessor, ILogger<MemoryBackplanePlugin>? logger = null)
		{
			if (optionsAccessor is null)
				throw new ArgumentNullException(nameof(optionsAccessor));

			// OPTIONS
			_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

			// LOGGING
			if (logger is NullLogger<MemoryBackplanePlugin>)
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
			// CHANNEL
			var _prefix = _options.ChannelPrefix;
			if (string.IsNullOrWhiteSpace(_prefix))
				_prefix = cache.CacheName;
			_channel = $"{_prefix}.Evict";

			StartListeningForRemoteEvictions(cache);
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			StopListeningForRemoteEvictions(cache);
		}

		private ConcurrentDictionary<string, IFusionCache> GetChannel()
		{
			return _channels.GetOrAdd(_channel, _ => new ConcurrentDictionary<string, IFusionCache>());
		}

		private void StartListeningForRemoteEvictions(IFusionCache cache)
		{
			var channel = GetChannel();

			channel[cache.InstanceId] = cache;
		}

		private void StopListeningForRemoteEvictions(IFusionCache cache)
		{
			var channel = GetChannel();

			channel.TryRemove(cache.InstanceId, out _);
		}

		/// <inheritdoc/>
		public async ValueTask SendNotificationAsync(IFusionCache cache, BackplaneMessage message, CancellationToken token)
		{
			if (message is null)
				throw new ArgumentNullException(nameof(message));

			if (message.IsValid() == false)
				throw new InvalidOperationException("The message is invalid");

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);

			foreach (var item in GetChannel())
			{
				await item.Value.OnBackplaneNotificationAsync(message, token).ConfigureAwait(false);
			}
		}

		/// <inheritdoc/>
		public void SendNotification(IFusionCache cache, BackplaneMessage message)
		{
			if (message is null)
				throw new ArgumentNullException(nameof(message));

			if (message.IsValid() == false)
				throw new InvalidOperationException("The message is invalid");

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", message.CacheKey);

			foreach (var item in GetChannel())
			{
				item.Value.OnBackplaneNotification(message);
			}
		}
	}
}
