using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

// see https://github.com/ZiggyCreatures/FusionCache/issues/370

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

/// <summary>
/// An Azure Service Bus based implementation of a FusionCache backplane. It owns the subscribe/unsubscribe orchestration:
/// provisioning (via <see cref="IAzureServiceBusAdminWrapper"/>) always runs before the underlying
/// <see cref="IAzureServiceBusClientWrapper"/> is asked to subscribe, and unprovisioning always runs after it is asked to
/// unsubscribe. If self-healing isn't needed (or isn't possible, e.g. no administrative rights), pass
/// <see cref="NoOpAzureServiceBusAdminWrapper.Instance"/> — there is no nullable/optional provisioner dependency.
/// </summary>
public partial class AzureServiceBusBackplane
	: IFusionCacheBackplane
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusBackplane"/> class.
	/// </summary>
	/// <param name="serviceBusCommunicator">The <see cref="IAzureServiceBusClientWrapper"/> to use for sending/receiving messages.</param>
	/// <param name="serviceBusProvisioner">
	/// The <see cref="IAzureServiceBusAdminWrapper"/> to use for provisioning the topic/subscription before subscribing, and
	/// tearing it down after unsubscribing. Use <see cref="NoOpAzureServiceBusAdminWrapper.Instance"/> when this instance has
	/// no administrative capability and the topic/subscription are provisioned out of band.
	/// </param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public AzureServiceBusBackplane(
		IAzureServiceBusClientWrapper serviceBusCommunicator,
		IAzureServiceBusAdminWrapper serviceBusProvisioner,
		ILogger<AzureServiceBusBackplane>? logger = null,
		TimeSpan? lockTimeout = null)
	{
		_serviceBusCommunicator = serviceBusCommunicator ?? throw new ArgumentNullException(nameof(serviceBusCommunicator));
		_serviceBusProvisioner = serviceBusProvisioner ?? throw new ArgumentNullException(nameof(serviceBusProvisioner));
		_logger = logger;
		_lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(5);
		if (_lockTimeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(lockTimeout));
	}

	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
	private readonly IAzureServiceBusClientWrapper _serviceBusCommunicator;
	private readonly IAzureServiceBusAdminWrapper _serviceBusProvisioner;
	private readonly ILogger? _logger;
	private readonly TimeSpan _lockTimeout;

	private string? _cacheName;
	private string? _cacheInstanceId;
	private Func<ServiceBusReceivedMessage, Task>? _incomingMessageHandler;
	private Func<Task>? _subscriptionMissingHandler;
}
