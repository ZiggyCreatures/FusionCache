using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

/// <summary>
/// Creates and owns an instance subscription and its server-side self-message filter. Requires Manage permissions.
/// </summary>
/// <param name="serviceBusAdministrationClient">The administrative client used to create/delete the topic, subscription, and self-filter rule.</param>
/// <param name="topicName">The name of the topic to create if missing.</param>
/// <param name="subscriptionName">The name of the subscription to create if missing.</param>
/// <param name="subscriptionAutoDeleteOnIdle">The auto-delete timeout to apply to the created subscription.</param>
/// <param name="logger">The logger to use.</param>
public class AzureServiceBusAdminWrapper(
		ServiceBusAdministrationClient serviceBusAdministrationClient,
		string topicName,
		string subscriptionName,
		ILogger<AzureServiceBusAdminWrapper> logger) : IAzureServiceBusAdminWrapper
{
	internal const string SelfMessageFilterRuleName = "FilterOutOwnMessages";

	/// <inheritdoc/>
	public async ValueTask EnsureTopicAsync()
	{
		if (await serviceBusAdministrationClient.TopicExistsAsync(topicName))
			return;

		await serviceBusAdministrationClient.CreateTopicAsync(topicName);
	}

	/// <inheritdoc/>
	public async ValueTask EnsureSubscriptionAsync()
	{
		await EnsureTopicAsync();

		if (!await serviceBusAdministrationClient.SubscriptionExistsAsync(topicName, subscriptionName))
		{
			logger.LogInformation("Creating a new topic subscription: {SubscriptionName}", subscriptionName);
			await serviceBusAdministrationClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName));
		}

		if (await serviceBusAdministrationClient.RuleExistsAsync(topicName, subscriptionName, SelfMessageFilterRuleName))
			return;

		var escapedSubscriptionName = subscriptionName.Replace("'", "''");
		await serviceBusAdministrationClient.CreateRuleAsync(topicName, subscriptionName, new CreateRuleOptions(
			SelfMessageFilterRuleName,
			new SqlRuleFilter($"{AzureServiceBusClientWrapper.ConnectionIdApplicationPropertyName} <> '{escapedSubscriptionName}'")
		));
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
			logger.LogError(exc, "An error occurred while deleting subscription {SubscriptionName}", subscriptionName);
		}
	}
}
