using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

/// <summary>
/// An <see cref="IAzureServiceBusAdminWrapper"/> implementation that creates/deletes the topic, subscription, and
/// self-message-filter rule via a <see cref="ServiceBusAdministrationClient"/>. Requires Manage permissions.
/// </summary>
/// <param name="serviceBusAdministrationClient">The administrative client used to create/delete the topic, subscription, and self-filter rule.</param>
/// <param name="topicName">The name of the topic to create if missing.</param>
/// <param name="subscriptionName">The name of the subscription to create if missing.</param>
/// <param name="logger">The logger to use.</param>
public class AzureServiceBusAdminWrapper(
		ServiceBusAdministrationClient serviceBusAdministrationClient,
		string topicName,
		string subscriptionName,
		ILogger<AzureServiceBusAdminWrapper> logger) : IAzureServiceBusAdminWrapper
{
	/// <inheritdoc/>
	public async ValueTask EnsureTopicAsync()
	{
		if (!await serviceBusAdministrationClient.TopicExistsAsync(topicName))
			await serviceBusAdministrationClient.CreateTopicAsync(topicName);
	}

	/// <inheritdoc/>
	public async ValueTask EnsureSubscriptionAsync()
	{
		if (await serviceBusAdministrationClient.SubscriptionExistsAsync(topicName, subscriptionName))
			return;
		logger.LogInformation("Creating a new topic subscription: {subscriptionName}", subscriptionName);

		await EnsureTopicAsync();

		await serviceBusAdministrationClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName));
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			await serviceBusAdministrationClient.DeleteSubscriptionAsync(topicName, subscriptionName);
		}
		catch (Exception exc)
		{
			logger.LogError(exc, "An error occurred while deleting subscription {subscriptionName}", subscriptionName);
		}
	}
}
