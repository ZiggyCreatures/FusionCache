using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NATS.Client.Core;

namespace ZiggyCreatures.Caching.Fusion.Backplane.NATS;

/// <summary>
/// A NATS based implementation of a FusionCache backplane.
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
	/// Initializes a new instance of the NatsBackplane class.
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
						if (BackplaneMessage.TryParse(msg.Data.Span, out BackplaneMessage message))
						{
							await OnMessageAsync(message).ConfigureAwait(false);
						}
					}
				}
			}
		});
	}


	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		// TODO: IS THERE A BETTER WAY INSTEAD OF SYNC OVER ASYNC ?
		SubscribeAsync(options).GetAwaiter().GetResult();
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
		// TODO: IS THERE A BETTER WAY INSTEAD OF SYNC OVER ASYNC ?
		UnsubscribeAsync().GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// TH TYPE NatsBufferWriter SEEMS TO BE DISPOSABLE: SHOULD IT BE DISPOSED?
		var writer = new NatsBufferWriter<byte>();
		message.WriteTo(writer);
		await _connection.PublishAsync(_channelName, writer).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Publish(in BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// TODO: IS THERE A BETTER WAY INSTEAD OF SYNC OVER ASYNC ?
		PublishAsync(message, options, token).GetAwaiter().GetResult();
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
