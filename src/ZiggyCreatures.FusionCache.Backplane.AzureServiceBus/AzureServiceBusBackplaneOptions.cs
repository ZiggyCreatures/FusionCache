using Azure.Core;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

/// <summary>
/// Represents the options available for the Azure Service Bus backplane.
/// </summary>
public class AzureServiceBusBackplaneOptions
{
	/// <summary>
	/// The connection string used to connect to Azure Service Bus.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// The fully qualified namespace (e.g. "mynamespace.servicebus.windows.net") used, together with <see cref="Credential"/>, to connect to Azure Service Bus via Azure Identity.
	/// This is an alternative to <see cref="ConnectionString"/>.
	/// </summary>
	public string? FullyQualifiedNamespace { get; set; }

	/// <summary>
	/// The <see cref="TokenCredential"/> to use together with <see cref="FullyQualifiedNamespace"/> for Azure Identity based authentication.
	/// </summary>
	public TokenCredential? Credential { get; set; }

	/// <summary>
	/// The name of the Service Bus topic to use.
	/// If <see langword="null"/> (the default), the cache name is used instead (sanitized into a valid Service Bus entity name).
	/// Set this explicitly to use a specific topic, e.g. to share a single topic across multiple differently-named caches.
	/// </summary>
	public string? TopicName { get; set; }

	/// <summary>
	/// Whether this backplane instance is allowed to perform administrative operations against Azure Service Bus:
	/// creating/deleting the topic, the per-instance subscription, and its self-message-filter rule. Defaults to <see langword="true"/>.
	/// <br/>
	/// Set to <see langword="false"/> for least-privilege deployments where the connection string/credential only has
	/// Send/Listen claims, not Manage. In that case <see cref="SubscriptionName"/> must be set to an already-existing
	/// subscription (provisioned out of band, e.g. via IaC), since one cannot be created on the fly, and it will never
	/// be deleted either.
	/// </summary>
	public bool IsAdmin { get; set; } = true;

	/// <summary>
	/// The name of the Service Bus subscription to attach to.
	/// Required when <see cref="IsAdmin"/> is <see langword="false"/> (the subscription must already exist).
	/// When <see cref="IsAdmin"/> is <see langword="true"/> and this is left <see langword="null"/> (the default), a unique
	/// subscription name is generated automatically for this instance.
	/// </summary>
	public string? SubscriptionName { get; set; }

	/// <summary>
	/// The <see cref="TimeSpan"/> after which an idle, auto-created per-instance subscription will be deleted by the Service Bus service.
	/// </summary>
	public TimeSpan SubscriptionAutoDeleteOnIdle { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// The max amount of time to wait to acquire the internal lock used to coordinate connection/subscription setup.
	/// </summary>
	public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
