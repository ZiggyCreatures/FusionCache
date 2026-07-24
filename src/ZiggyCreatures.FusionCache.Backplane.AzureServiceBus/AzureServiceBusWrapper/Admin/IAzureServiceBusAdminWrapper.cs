namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

/// <summary>
/// Abstracts the administrative operations (creating/deleting a topic, subscription, and self-message-filter rule) that
/// <see cref="AzureServiceBusBackplane"/> orchestrates around an <see cref="IAzureServiceBusClientWrapper"/>: it always
/// ensures the topic and subscription before asking the communicator to subscribe, and tears down whatever it provisioned
/// after asking the communicator to unsubscribe.
/// </summary>
public interface IAzureServiceBusAdminWrapper :IAsyncDisposable
{
	/// <summary>
	/// Ensures the topic exists, creating it if missing.
	/// </summary>
	ValueTask EnsureTopicAsync();

	/// <summary>
	/// Ensures the subscription (and its self-message-filter rule) exists, creating it if missing.
	/// </summary>
	ValueTask EnsureSubscriptionAsync();
}
