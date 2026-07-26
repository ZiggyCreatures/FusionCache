using Azure.Messaging.ServiceBus;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusClientWrapperTests
	: AbstractTests
{
	public AzureServiceBusClientWrapperTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private const string FakeConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
	private static AzureServiceBusBackplaneOptions Options => new() { LockTimeout = TimeSpan.FromSeconds(1) };

	[Fact]
	public void ConstructorUsesTheGivenTopicAndSubscriptionNames()
	{
		var client = new ServiceBusClient(FakeConnectionString);

		var communicator = new AzureServiceBusClientWrapper(
			serviceBusClient: client,
			topicName: "my-topic",
			subscriptionName: "my-existing-subscription",
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance,
			asbOptions: Options
		);

		Assert.Equal("my-topic", communicator.TopicName);
		Assert.Equal("my-existing-subscription", communicator.SubscriptionName);
	}

	[Fact]
	public void SubscriptionMissingEventCanBeAddedAndRemovedWithoutThrowing()
	{
		var client = new ServiceBusClient(FakeConnectionString);

		var communicator = new AzureServiceBusClientWrapper(
			serviceBusClient: client,
			topicName: "my-topic",
			subscriptionName: "my-existing-subscription",
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance,
			asbOptions: Options
		);

		Task Handler() => Task.CompletedTask;

		communicator.SubscriptionMissing += Handler;
		communicator.SubscriptionMissing -= Handler;
	}

	[Fact]
	public void GenerateIdReturnsAValidSubscriptionNameLength()
	{
		var id = AzureServiceBusHelpers.GenerateId();

		Assert.True(id.Length <= AzureServiceBusHelpers.MaxSubscriptionNameLength, $"Expected length <= {AzureServiceBusHelpers.MaxSubscriptionNameLength}, but was {id.Length} ('{id}')");
		Assert.NotEmpty(id);
	}

	[Fact]
	public void GenerateIdOnlyContainsValidServiceBusEntityNameCharacters()
	{
		var id = AzureServiceBusHelpers.GenerateId();

		foreach (var c in id)
		{
			Assert.True(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '/', $"Unexpected character '{c}' in generated id '{id}'");
		}
	}

	[Fact]
	public void GenerateIdReturnsDifferentValuesOnSuccessiveCalls()
	{
		var id1 = AzureServiceBusHelpers.GenerateId();
		var id2 = AzureServiceBusHelpers.GenerateId();

		Assert.NotEqual(id1, id2);
	}
}
