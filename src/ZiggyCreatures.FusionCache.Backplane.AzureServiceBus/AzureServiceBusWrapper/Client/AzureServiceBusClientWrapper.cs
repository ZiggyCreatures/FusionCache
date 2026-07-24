using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

// see https://github.com/ZiggyCreatures/FusionCache/issues/370

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

/// <summary>
/// An <see cref="IAzureServiceBusClientWrapper"/> implementation based on a Service Bus topic/subscription pair. It only
/// ever sends and receives messages: it has no knowledge of, and never performs, any administrative operation (creating
/// or deleting a topic, subscription, or rule). The topic and subscription must already exist by the time <see cref="Subscribe"/>
/// is called — provisioning that is the responsibility of an <see cref="IAzureServiceBusAdminWrapper"/>, which
/// <see cref="AzureServiceBusBackplane"/> orchestrates around calls to this class.
/// </summary>
/// <param name="serviceBusClient">The client used to send/receive messages.</param>
/// <param name="topicName">The name of the topic to use. Must already exist by the time <see cref="Subscribe"/> is called.</param>
/// <param name="subscriptionName">The name of the subscription to use. Must already exist by the time <see cref="Subscribe"/> is called.</param>
/// <param name="logger">The logger to use.</param>
public class AzureServiceBusClientWrapper(
		ServiceBusClient serviceBusClient,
		string topicName,
		string subscriptionName,
		ILogger<AzureServiceBusClientWrapper> logger, IOptions<AzureServiceBusBackplaneOptions> asbOptions) : IAzureServiceBusClientWrapper
{
	/// <summary>
	/// The application property used to carry the publishing instance's subscription name, for self-message filtering.
	/// Also used by <see cref="AzureServiceBusAdminWrapper"/> when creating the corresponding self-filter rule.
	/// </summary>
	internal const string ConnectionIdApplicationPropertyName = "ConnectionId";

	/// <inheritdoc/>
	public event Func<Task>? SubscriptionMissing;

	/// <summary>
	/// The name of the Service Bus subscription this instance is attached to (also used as the self-message filter value).
	/// </summary>
	internal string SubscriptionName => subscriptionName;

	/// <summary>
	/// The name of the Service Bus topic this instance talks on.
	/// </summary>
	internal string TopicName => topicName;
	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
	private readonly List<Func<ServiceBusReceivedMessage, Task>> _handlers = new();


	private ServiceBusProcessor? _serviceBusProcessor;
	private ServiceBusSender? _serviceBusSender;

	/// <inheritdoc/>
	public async Task Subscribe(Func<ServiceBusReceivedMessage, Task> handler)
	{
		if (!await _lock.WaitAsync(asbOptions.Value.LockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
			_handlers.Add(handler);
		}
		finally
		{
			_lock.Release();
		}

		await EnsureProcessor();
	}

	/// <inheritdoc/>
	public async Task Unsubscribe(Func<ServiceBusReceivedMessage, Task> handler)
	{
		if (!await _lock.WaitAsync(asbOptions.Value.LockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
			_handlers.Remove(handler);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task SendMessage(ServiceBusMessage message, CancellationToken cancellationToken)
	{
		var sender = await EnsureSender();
		message.ApplicationProperties.Add(ConnectionIdApplicationPropertyName, subscriptionName);
		await sender.SendMessageAsync(message, cancellationToken);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_serviceBusProcessor is null)
			return;

		if (!await _lock.WaitAsync(asbOptions.Value.LockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
			if (_serviceBusProcessor is not null)
			{
				await _serviceBusProcessor.StopProcessingAsync();
				await _serviceBusProcessor.DisposeAsync();
				_serviceBusProcessor = null;
			}
		}
		catch (Exception exc)
		{
			logger.LogError(exc, "An error occurred while stopping the processor for {subscriptionName}", subscriptionName);
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task<ServiceBusProcessor> EnsureProcessor()
	{
		if (_serviceBusProcessor is not null)
			return _serviceBusProcessor;

		if (!await _lock.WaitAsync(asbOptions.Value.LockTimeout))
			throw new TimeoutException("Can't acquire lock");
		try
		{
			if (_serviceBusProcessor is null)
			{
				_serviceBusProcessor = serviceBusClient.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
				{
					AutoCompleteMessages = true,
					Identifier = subscriptionName
				});

				_serviceBusProcessor.ProcessErrorAsync += ProcessErrorAsync;
				_serviceBusProcessor.ProcessMessageAsync += ProcessMessageAsync;

				await _serviceBusProcessor.StartProcessingAsync();
			}
		}
		finally
		{
			_lock.Release();
		}

		return _serviceBusProcessor!;
	}

	private async Task<ServiceBusSender> EnsureSender()
	{
		if (_serviceBusSender is not null)
			return _serviceBusSender;

		await EnsureProcessor();

		if (!await _lock.WaitAsync(asbOptions.Value.LockTimeout))
			throw new TimeoutException("Can't acquire lock");

		try
		{
			_serviceBusSender ??= serviceBusClient.CreateSender(topicName);
		}
		finally
		{
			_lock.Release();
		}

		return _serviceBusSender;
	}

	private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
	{
		if (!args.Message.ApplicationProperties.TryGetValue(ConnectionIdApplicationPropertyName, out var oOriginConnectionId)
			|| oOriginConnectionId is not string originConnectionId)
		{
			logger.LogError("Received a message without a {ConnectionId} application property", ConnectionIdApplicationPropertyName);
			return;
		}

		if (originConnectionId == subscriptionName)
		{
			logger.LogError("Received a message from itself ({ConnectionId}), it should not happen and may indicate that a subscription filter is missing", originConnectionId);
			return;
		}

		foreach (var handler in _handlers)
		{
			await handler(args.Message);
		}
	}

	private async Task ProcessErrorAsync(ProcessErrorEventArgs args)
	{
		if (logger.IsEnabled(LogLevel.Warning))
			logger.Log(LogLevel.Warning, args.Exception, "An error occurred while processing a ServiceBus message for connection {subscriptionName}", subscriptionName);

		if (args.Exception is ServiceBusException { Reason: ServiceBusFailureReason.MessagingEntityNotFound })
		{
			if (logger.IsEnabled(LogLevel.Information))
				logger.Log(LogLevel.Information, "Subscription {subscriptionName} appears to be missing", subscriptionName);

			var handler = SubscriptionMissing;
			if (handler is not null)
				await handler();
		}
	}
}
