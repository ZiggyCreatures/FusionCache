using Azure.Messaging.ServiceBus.Administration;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusAdminWrapperTests
	: AbstractTests
{
	public AzureServiceBusAdminWrapperTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private const string FakeConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";

	[Fact]
	public void AdminWrapperImplementsTheAdminInterface()
	{
		var adminClient = new ServiceBusAdministrationClient(FakeConnectionString);

		var provisioner = new AzureServiceBusAdminWrapper(adminClient, "my-topic", "my-subscription", NullLogger<AzureServiceBusAdminWrapper>.Instance);

		Assert.IsAssignableFrom<IAzureServiceBusAdminWrapper>(provisioner);
	}

	[Fact]
	public async Task NoOpAdminWrapperMethodsCompleteWithoutThrowingAsync()
	{
		var provisioner = NoOpAzureServiceBusAdminWrapper.Instance;

		await provisioner.EnsureTopicAsync();
		await provisioner.EnsureSubscriptionAsync();
		await provisioner.DisposeAsync();
	}

	[Fact]
	public void NoOpAdminWrapperInstanceIsASingleton()
	{
		Assert.Same(NoOpAzureServiceBusAdminWrapper.Instance, NoOpAzureServiceBusAdminWrapper.Instance);
	}
}
