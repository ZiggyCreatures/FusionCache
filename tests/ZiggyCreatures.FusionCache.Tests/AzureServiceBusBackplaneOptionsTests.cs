using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

namespace FusionCacheTests;

public class AzureServiceBusBackplaneOptionsTests
	: AbstractTests
{
	public AzureServiceBusBackplaneOptionsTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private sealed class FakeTokenCredential
		: TokenCredential
	{
		public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
		{
			return new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1));
		}

		public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
		{
			return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
		}
	}

	[Fact]
	public async Task GetOrCreateClientsAsyncThrowsWhenNothingIsConfiguredAsync()
	{
		var options = new AzureServiceBusBackplaneOptions();

		await Assert.ThrowsAsync<InvalidOperationException>(() => options.GetOrCreateClientsAsync());
	}

	[Fact]
	public async Task GetOrCreateClientsAsyncUsesConnectionStringAsync()
	{
		var options = new AzureServiceBusBackplaneOptions
		{
			ConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk="
		};

		var (client, adminClient) = await options.GetOrCreateClientsAsync();

		Assert.NotNull(client);
		Assert.NotNull(adminClient);
		Assert.Equal("fake-namespace.servicebus.windows.net", client.FullyQualifiedNamespace);
	}

	[Fact]
	public async Task GetOrCreateClientsAsyncUsesFullyQualifiedNamespaceAndCredentialAsync()
	{
		var options = new AzureServiceBusBackplaneOptions
		{
			FullyQualifiedNamespace = "fake-namespace.servicebus.windows.net",
			Credential = new FakeTokenCredential()
		};

		var (client, adminClient) = await options.GetOrCreateClientsAsync();

		Assert.NotNull(client);
		Assert.NotNull(adminClient);
		Assert.Equal("fake-namespace.servicebus.windows.net", client.FullyQualifiedNamespace);
	}

	[Fact]
	public async Task ServiceBusClientFactoryTakesPrecedenceOverConnectionStringAsync()
	{
		const string factoryConnectionString = "Endpoint=sb://factory-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
		var factoryClient = new ServiceBusClient(factoryConnectionString);
		var factoryAdminClient = new ServiceBusAdministrationClient(factoryConnectionString);

		var options = new AzureServiceBusBackplaneOptions
		{
			// AN INVALID CONNECTION STRING: IF THE RESOLVER TRIED TO USE IT INSTEAD OF THE FACTORY, CONSTRUCTING A CLIENT FROM IT WOULD THROW
			ConnectionString = "this is not a valid connection string",
			ServiceBusClientFactory = () => Task.FromResult((factoryClient, factoryAdminClient))
		};

		var (client, adminClient) = await options.GetOrCreateClientsAsync();

		Assert.Same(factoryClient, client);
		Assert.Same(factoryAdminClient, adminClient);
	}
}
