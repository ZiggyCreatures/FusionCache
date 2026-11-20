namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

/// <summary>
/// A no-op <see cref="IAzureServiceBusAdminWrapper"/>, used when a backplane instance has no administrative capability: it
/// assumes the topic and subscription already exist (provisioned out of band, e.g. via IaC) and never attempts to create,
/// delete, or otherwise administer anything. Used as a Null Object instead of a nullable/optional provisioner dependency.
/// </summary>
public sealed class NoOpAzureServiceBusAdminWrapper : IAzureServiceBusAdminWrapper
{
	/// <summary>
	/// A shared, stateless instance.
	/// </summary>
	public static readonly NoOpAzureServiceBusAdminWrapper Instance = new();

	/// <inheritdoc/>
	public ValueTask EnsureTopicAsync() => default;

	/// <inheritdoc/>
	public ValueTask EnsureSubscriptionAsync() => default;

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => default;
}
