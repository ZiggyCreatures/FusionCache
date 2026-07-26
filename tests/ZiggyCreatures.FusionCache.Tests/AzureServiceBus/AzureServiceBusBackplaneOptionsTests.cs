using Azure.Core;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusBackplaneOptionsTests
	: AbstractTests
{
	public AzureServiceBusBackplaneOptionsTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private sealed class FakeTokenCredential : TokenCredential
	{
		public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> new("fake-token", DateTimeOffset.UtcNow.AddHours(1));

		public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> new(GetToken(requestContext, cancellationToken));
	}

	[Fact]
	public void DefaultsAreSuitableForAdminBackplane()
	{
		var options = new AzureServiceBusBackplaneOptions();

		Assert.True(options.IsAdmin);
		Assert.Equal(TimeSpan.FromMinutes(10), options.SubscriptionAutoDeleteOnIdle);
		Assert.Equal(TimeSpan.FromSeconds(5), options.LockTimeout);
	}

	[Fact]
	public void ConnectionStringCanBeConfigured()
	{
		var options = new AzureServiceBusBackplaneOptions
		{
			ConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk="
		};

		Assert.Equal("Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=", options.ConnectionString);
	}

	[Fact]
	public void ManagedIdentityCanBeConfigured()
	{
		var options = new AzureServiceBusBackplaneOptions
		{
			FullyQualifiedNamespace = "fake-namespace.servicebus.windows.net",
			Credential = new FakeTokenCredential()
		};

		Assert.Equal("fake-namespace.servicebus.windows.net", options.FullyQualifiedNamespace);
		Assert.NotNull(options.Credential);
	}

}
