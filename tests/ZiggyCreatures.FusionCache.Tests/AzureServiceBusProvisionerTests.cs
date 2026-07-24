using Azure.Messaging.ServiceBus.Administration;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace FusionCacheTests;

public class AzureServiceBusProvisionerTests
	: AbstractTests
{
	public AzureServiceBusProvisionerTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private const string FakeConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";

	[Fact]
	public void AdminProvisionerConstructorThrowsWhenAdministrationClientIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new AzureServiceBusAdminProvisioner(
			serviceBusAdministrationClient: null!,
			topicName: "my-topic",
			subscriptionName: "my-subscription",
			logger: NullLogger<AzureServiceBusAdminProvisioner>.Instance
		));
	}

	[Fact]
	public void AdminProvisionerImplementsTheProvisionerInterface()
	{
		var adminClient = new ServiceBusAdministrationClient(FakeConnectionString);

		var provisioner = new AzureServiceBusAdminProvisioner(adminClient, "my-topic", "my-subscription", NullLogger<AzureServiceBusAdminProvisioner>.Instance);

		Assert.IsAssignableFrom<IAzureServiceBusAdminWrapper>(provisioner);
	}

	[Fact]
	public async Task NoOpProvisionerMethodsCompleteWithoutThrowingAsync()
	{
		var provisioner = NoOpAzureServiceBusAdminWrapper.Instance;

		await provisioner.EnsureTopicAsync();
		await provisioner.EnsureSubscriptionAsync();
		await provisioner.UnprovisionAsync();
	}

	[Fact]
	public void NoOpProvisionerInstanceIsASingleton()
	{
		Assert.Same(NoOpAzureServiceBusAdminWrapper.Instance, NoOpAzureServiceBusAdminWrapper.Instance);
	}
}
