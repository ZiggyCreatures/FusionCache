using System.Buffers;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NATS.Client.Core;

using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Backplane.NATS;

/// <summary>
/// A Redis based implementation of a FusionCache backplane.
/// </summary>
public partial class NatsBackplane
	: IFusionCacheBackplane
{
	private BackplaneSubscriptionOptions? _subscriptionOptions;
	private readonly ILogger? _logger;
	private INatsConnection _connection;
	private string _channelName = "";
	private Func<BackplaneMessage, ValueTask>? _incomingMessageHandlerAsync;
	private INatsSub<NatsMemoryOwner<byte>>? _subscription;

	/// <summary>
	/// Initializes a new instance of the RedisBackplane class.
	/// </summary>
	/// <param name="natsConnection">The NATS connection instance to use.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public NatsBackplane(INatsConnection? natsConnection, ILogger<NatsBackplane>? logger = null)
	{
		_connection = natsConnection ?? throw new ArgumentNullException(nameof(natsConnection));

		// LOGGING
		if (logger is NullLogger<NatsBackplane>)
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
		if (string.IsNullOrEmpty(_channelName))
			throw new NullReferenceException("The backplane channel name must have a value");

		_incomingMessageHandlerAsync = _subscriptionOptions.IncomingMessageHandlerAsync;
		_subscription = await _connection.SubscribeCoreAsync<NatsMemoryOwner<byte>>(_channelName);
		_ = Task.Run(async () =>
		{
			while (await _subscription.Msgs.WaitToReadAsync().ConfigureAwait(false))
			{
				while (_subscription.Msgs.TryRead(out var msg))
				{
					using (msg.Data)
					{
						var message = BackplaneMessage.FromByteArray(msg.Data.Memory.ToArray());
						await OnMessageAsync(message).ConfigureAwait(false);
					}
				}
			}
		});
	}


	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
#pragma warning disable VSTHRD002 // Suppressing since this is a sync-over-async method intentionally as the library doesn't provide sync APIs
		SubscribeAsync(options).AsTask().Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
	}

	/// <inheritdoc/>
	public async ValueTask UnsubscribeAsync()
	{
		if (_subscription is not null)
		{
			await _subscription.UnsubscribeAsync().ConfigureAwait(false);
			await _subscription.Msgs.Completion;
		}
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
#pragma warning disable VSTHRD002 // Suppressing since this is a sync-over-async method intentionally as the library doesn't provide sync APIs
		UnsubscribeAsync().AsTask().Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		var writer = new NatsBufferWriter<byte>();
		writer.Write(BackplaneMessage.ToByteArray(message));
		await _connection.PublishAsync(_channelName, writer).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
#pragma warning disable VSTHRD002 // Suppressing since this is a sync-over-async method intentionally as the library doesn't provide sync APIs
		PublishAsync(message, options, token).AsTask().Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
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
