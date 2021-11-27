using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Plugins.MemoryBackplane
{
	public class MemoryBackplanePlugin
		: IFusionCachePlugin
	{
		private readonly MemoryBackplaneOptions _options;
		private readonly ILogger? _logger;
		private string _channel;

		private static ConcurrentDictionary<string, ConcurrentDictionary<string, IFusionCache>> _channels = new ConcurrentDictionary<string, ConcurrentDictionary<string, IFusionCache>>();

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

			// CHANNEL
			_channel = "FusionCache.Eviction";
		}

		public void Start(IFusionCache cache)
		{
			// CHANNEL
			var _prefix = _options.ChannelPrefix;
			if (string.IsNullOrWhiteSpace(_prefix))
				_prefix = cache.CacheName;
			_channel = $"{_prefix}.Evict";

			StartListeningForRemoteEvictions(cache);

			cache.Events.Set += OnSet;
			cache.Events.Remove += OnRemove;
		}

		public void Stop(IFusionCache cache)
		{
			cache.Events.Set -= OnSet;
			cache.Events.Remove -= OnRemove;

			StopListeningForRemoteEvictions(cache);
		}

		private void OnSet(object sender, Events.FusionCacheEntryEventArgs e)
		{
			_ = NotifyEvictionAsync((IFusionCache)sender, e.Key);
		}

		private void OnRemove(object sender, Events.FusionCacheEntryEventArgs e)
		{
			_ = NotifyEvictionAsync((IFusionCache)sender, e.Key);
		}

		private ConcurrentDictionary<string, IFusionCache> GetChannel()
		{
			return _channels.GetOrAdd(_channel, _ => new ConcurrentDictionary<string, IFusionCache>());
		}

		private async Task NotifyEvictionAsync(IFusionCache cache, string cacheKey)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "An eviction notification has been sent for {CacheKey}", cacheKey);

			if (_options.NotificationsDelay.HasValue)
				await Task.Delay(_options.NotificationsDelay.Value).ConfigureAwait(false);

			foreach (var item in GetChannel())
			{
				if (item.Key != cache.InstanceId)
				{
					try
					{
						item.Value.Evict(cacheKey);

						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "An eviction notification has been received for {CacheKey}", cacheKey);
					}
					catch
					{
						// EMPTY
					}
				}
			}
		}

		public void StartListeningForRemoteEvictions(IFusionCache cache)
		{
			var channel = GetChannel();

			channel[cache.InstanceId] = cache;
		}

		public void StopListeningForRemoteEvictions(IFusionCache cache)
		{
			var channel = GetChannel();

			channel.TryRemove(cache.InstanceId, out _);
		}
	}
}
