using Azure.Messaging.ServiceBus;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

/// <summary>
/// Abstracts the Azure Service Bus data-plane operations (subscribe/unsubscribe/send) used by <see cref="AzureServiceBusBackplane"/>.
/// This has no knowledge of topic/subscription provisioning: see <see cref="IAzureServiceBusAdminWrapper"/> for that —
/// <see cref="AzureServiceBusBackplane"/> is what orchestrates the two together.
/// </summary>
public interface IAzureServiceBusClientWrapper : IAsyncDisposable
{
	/// <summary>
	/// Registers a handler to be invoked for every incoming message, ensuring the underlying processor is running.
	/// </summary>
	Task Subscribe(Func<ServiceBusReceivedMessage, Task> handler);

	/// <summary>
	/// Removes a previously registered handler.
	/// </summary>
	Task Unsubscribe(Func<ServiceBusReceivedMessage, Task> handler);

	/// <summary>
	/// Sends a message to the topic.
	/// </summary>
	Task SendMessage(ServiceBusMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Raised when the underlying processor reports that the subscription appears to be missing (e.g. it was reaped by
	/// idle auto-delete). <see cref="AzureServiceBusBackplane"/> reacts to this by re-running its <see cref="IAzureServiceBusAdminWrapper"/>.
	/// </summary>
	event Func<Task>? SubscriptionMissing;
}
