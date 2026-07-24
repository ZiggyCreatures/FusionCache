using Azure.Messaging.ServiceBus;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace FusionCacheTests;

public class AzureServiceBusCommunicatorTests
	: AbstractTests
{
	public AzureServiceBusCommunicatorTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private const string FakeConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";

	[Fact]
	public void ConstructorThrowsWhenSubscriptionNameIsMissing()
	{
		var client = new ServiceBusClient(FakeConnectionString);

		Assert.Throws<ArgumentException>(() => new AzureServiceBusClientWrapper(
			serviceBusClient: client,
			topicName: "my-topic",
			subscriptionName: null!,
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance
		));
	}

	[Fact]
	public void ConstructorThrowsWhenSubscriptionNameIsWhitespace()
	{
		var client = new ServiceBusClient(FakeConnectionString);

		Assert.Throws<ArgumentException>(() => new AzureServiceBusClientWrapper(
			serviceBusClient: client,
			topicName: "my-topic",
			subscriptionName: "   ",
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance
		));
	}

	[Fact]
	public void ConstructorUsesTheGivenTopicAndSubscriptionNames()
	{
		var client = new ServiceBusClient(FakeConnectionString);

		var communicator = new AzureServiceBusClientWrapper(
			serviceBusClient: client,
			topicName: "my-topic",
			subscriptionName: "my-existing-subscription",
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance
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
			logger: NullLogger<AzureServiceBusClientWrapper>.Instance
		);

		Task Handler() => Task.CompletedTask;

		communicator.SubscriptionMissing += Handler;
		communicator.SubscriptionMissing -= Handler;
	}

	[Fact]
	public void GenerateIdReturnsAValidSubscriptionNameLength()
	{
		var id = AzureServiceBusClientWrapper.GenerateId();

		Assert.True(id.Length <= AzureServiceBusNaming.MaxSubscriptionNameLength, $"Expected length <= {AzureServiceBusNaming.MaxSubscriptionNameLength}, but was {id.Length} ('{id}')");
		Assert.NotEmpty(id);
	}

	[Fact]
	public void GenerateIdOnlyContainsValidServiceBusEntityNameCharacters()
	{
		var id = AzureServiceBusClientWrapper.GenerateId();

		foreach (var c in id)
		{
			Assert.True(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '/', $"Unexpected character '{c}' in generated id '{id}'");
		}
	}

	[Fact]
	public void GenerateIdReturnsDifferentValuesOnSuccessiveCalls()
	{
		var id1 = AzureServiceBusClientWrapper.GenerateId();
		var id2 = AzureServiceBusClientWrapper.GenerateId();

		Assert.NotEqual(id1, id2);
	}
}
