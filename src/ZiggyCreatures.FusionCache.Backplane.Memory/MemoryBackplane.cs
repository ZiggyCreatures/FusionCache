using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory
{
	/// <summary>
	/// An-in memory implementation of a FusionCache backplane
	/// </summary>
	public class MemoryBackplane
		: IFusionCacheBackplane
	{
		private static readonly ConcurrentDictionary<string, List<MemoryBackplane>> _channels = new ConcurrentDictionary<string, List<MemoryBackplane>>();

		private readonly MemoryBackplaneOptions _options;
		private BackplaneSubscriptionOptions? _subscriptionOptions;
		private readonly ILogger? _logger;
		private string _channelName = "FusionCache.Notifications";
		private Action<BackplaneMessage>? _handler;
		private List<MemoryBackplane>? _backplanes;

		/// <summary>
		/// Initializes a new instance of the MemoryBackplanePlugin class.
		/// </summary>
		/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
		/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
		public MemoryBackplane(IOptions<MemoryBackplaneOptions> optionsAccessor, ILogger<MemoryBackplane>? logger = null)
		{
			if (optionsAccessor is null)
				throw new ArgumentNullException(nameof(optionsAccessor));

			// OPTIONS
			_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

			// LOGGING
			if (logger is NullLogger<MemoryBackplane>)
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
		public void Subscribe(BackplaneSubscriptionOptions subscriptionOptions)
		{
			if (subscriptionOptions is null)
				throw new ArgumentNullException(nameof(subscriptionOptions));

			if (subscriptionOptions.ChannelName is null)
				throw new NullReferenceException("The ChannelName cannot be null");

			if (subscriptionOptions.Handler is null)
				throw new NullReferenceException("The BackplaneSubscriptionOptions.Handler cannot be null");

			_subscriptionOptions = subscriptionOptions;

			_channelName = _subscriptionOptions.ChannelName;
			_handler = _subscriptionOptions.Handler;

			_backplanes = _channels.GetOrAdd(_channelName, _ => new List<MemoryBackplane>());

			if (_backplanes is null)
				return;

			lock (_backplanes)
			{
				_backplanes.Add(this);
			}
		}

		/// <inheritdoc/>
		public void Unsubscribe()
		{
			if (_backplanes is null)
				return;

			_handler = null;

			lock (_backplanes)
			{
				_backplanes.Remove(this);
			}
		}

		/// <inheritdoc/>
		public ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
		{
			Publish(message, options);
			return new ValueTask();
		}

		/// <inheritdoc/>
		public void Publish(BackplaneMessage message, FusionCacheEntryOptions options)
		{
			if (message is null)
				throw new ArgumentNullException(nameof(message));

			if (message.IsValid() == false)
				throw new InvalidOperationException("The message is invalid");

			if (_backplanes is null)
				throw new NullReferenceException("Something went wrong :-|");

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "A backplane notification has been sent for {CacheKey}", message.CacheKey);

			foreach (var backplane in _backplanes)
			{
				if (backplane == this)
					continue;

				backplane.OnMessage(message);
			}
		}

		internal void OnMessage(BackplaneMessage message)
		{
			_handler?.Invoke(message);
		}
	}
}
