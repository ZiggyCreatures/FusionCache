using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace FusionCacheTests.AzureServiceBus;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusClientWrapper"/> and <see cref="AzureServiceBusAdminWrapper"/> that
/// require a real or emulated Azure Service Bus broker. Skipped automatically unless the <see cref="ConnectionStringEnvVarName"/>
/// environment variable is set, e.g. to:
/// - a real Azure Service Bus namespace connection string, or
/// - the local connection string exposed by the Azure Service Bus emulator (mcr.microsoft.com/azure-messaging/servicebus-emulator).
/// Each test provisions its own uniquely-named topic and deletes it afterward, so tests can run concurrently/repeatedly without colliding.
/// These tests manually do what <see cref="AzureServiceBusBackplane"/> does internally: run the admin wrapper before subscribing
/// the communicator, and (where relevant) after unsubscribing it.
/// </summary>
public class AzureServiceBusClientWrapperIntegrationTests
	: AbstractTests
{
	private const string ConnectionStringEnvVarName = "FUSIONCACHE_TESTS_AZURESERVICEBUS_CONNECTIONSTRING";

	public AzureServiceBusClientWrapperIntegrationTests(ITestOutputHelper output)
		: base(output, null)
	{
		_connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVarName);
	}

	private readonly string? _connectionString;

	private void SkipIfNoBrokerConfigured()
	{
		if (string.IsNullOrWhiteSpace(_connectionString))
			Assert.Skip($"Set the {ConnectionStringEnvVarName} environment variable (pointing at a real Azure Service Bus namespace or the local emulator) to run these integration tests.");
	}

	private (ServiceBusClient Client, ServiceBusAdministrationClient AdminClient) CreateClients()
	{
		return (new ServiceBusClient(_connectionString), new ServiceBusAdministrationClient(_connectionString));
	}

	private static string CreateUniqueTopicName(string testName)
	{
		var topicName = $"fusioncache-tests-{testName}-{Guid.NewGuid():N}".ToLowerInvariant();

		return topicName.Length > AzureServiceBusHelpers.MaxTopicNameLength
			? topicName.Substring(0, AzureServiceBusHelpers.MaxTopicNameLength)
			: topicName;
	}

	private static AzureServiceBusAdminWrapper CreateAdminWrapper(ServiceBusAdministrationClient adminClient, string topicName, string subscriptionName)
	{
		return new AzureServiceBusAdminWrapper(adminClient, topicName, subscriptionName, NullLogger<AzureServiceBusAdminWrapper>.Instance);
	}

	private static AzureServiceBusClientWrapper CreateCommunicator(ServiceBusClient client, string topicName, string subscriptionName)
	{
		return new AzureServiceBusClientWrapper(client, topicName, subscriptionName, NullLogger<AzureServiceBusClientWrapper>.Instance, new AzureServiceBusBackplaneOptions());
	}

	[Fact]
	public async Task AdminWrapperEnsureMethodsAreIdempotentWhenCalledTwiceAsync()
	{
		SkipIfNoBrokerConfigured();

		var topicName = CreateUniqueTopicName(nameof(AdminWrapperEnsureMethodsAreIdempotentWhenCalledTwiceAsync));
		const string subscriptionName = "idempotent-test-subscription";
		var (_, adminClient) = CreateClients();

		try
		{
			var adminWrapper = CreateAdminWrapper(adminClient, topicName, subscriptionName);

			await adminWrapper.EnsureTopicAsync();
			await adminWrapper.EnsureTopicAsync();
			await adminWrapper.EnsureSubscriptionAsync();
			await adminWrapper.EnsureSubscriptionAsync();

			Assert.True(await adminClient.TopicExistsAsync(topicName, TestContext.Current.CancellationToken));
			Assert.True(await adminClient.SubscriptionExistsAsync(topicName, subscriptionName, TestContext.Current.CancellationToken));
		}
		finally
		{
			await TryDeleteTopicAsync(adminClient, topicName);
		}
	}

	[Fact]
	public async Task SelfPublishedMessagesAreFilteredOutBySubscriptionRuleAsync()
	{
		SkipIfNoBrokerConfigured();

		var topicName = CreateUniqueTopicName(nameof(SelfPublishedMessagesAreFilteredOutBySubscriptionRuleAsync));
		var (clientA, adminClientA) = CreateClients();

		try
		{
			var adminWrapperA = CreateAdminWrapper(adminClientA, topicName, "subscription-a");
			await adminWrapperA.EnsureTopicAsync();
			await adminWrapperA.EnsureSubscriptionAsync();

			var (clientB, adminClientB) = CreateClients();
			var adminWrapperB = CreateAdminWrapper(adminClientB, topicName, "subscription-b");
			await adminWrapperB.EnsureSubscriptionAsync();

			await using var communicatorA = CreateCommunicator(clientA, topicName, "subscription-a");
			await using var communicatorB = CreateCommunicator(clientB, topicName, "subscription-b");

			var aReceivedOwnMessage = false;
			var bReceivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			await communicatorA.Subscribe(_ => { aReceivedOwnMessage = true; return Task.CompletedTask; });
			await communicatorB.Subscribe(_ => { bReceivedTcs.TrySetResult(true); return Task.CompletedTask; });

			await communicatorA.SendMessage(new ServiceBusMessage(new BinaryData(new byte[] { 1, 2, 3 })), TestContext.Current.CancellationToken);

			var completed = await Task.WhenAny(bReceivedTcs.Task, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

			Assert.Same(bReceivedTcs.Task, completed);
			Assert.True(await bReceivedTcs.Task);
			Assert.False(aReceivedOwnMessage, "Communicator A should never receive its own published message: the per-subscription 'FilterOutOwnMessages' rule should have filtered it out server-side.");
		}
		finally
		{
			await TryDeleteTopicAsync(adminClientA, topicName);
		}
	}

	[Fact]
	public async Task SubscriptionMissingEventFiresAndAdminWrapperRecreatesTheSubscriptionAsync()
	{
		SkipIfNoBrokerConfigured();

		var topicName = CreateUniqueTopicName(nameof(SubscriptionMissingEventFiresAndAdminWrapperRecreatesTheSubscriptionAsync));
		const string subscriptionName = "self-healing-test-subscription";
		var (client, adminClient) = CreateClients();

		try
		{
			var adminWrapper = CreateAdminWrapper(adminClient, topicName, subscriptionName);
			await adminWrapper.EnsureTopicAsync();
			await adminWrapper.EnsureSubscriptionAsync();

			await using var communicator = CreateCommunicator(client, topicName, subscriptionName);

			var missingSignaled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			communicator.SubscriptionMissing += async () =>
			{
				missingSignaled.TrySetResult(true);
				await adminWrapper.EnsureSubscriptionAsync();
			};

			await communicator.Subscribe(_ => Task.CompletedTask);

			// SIMULATE THE SUBSCRIPTION BEING REAPED BY IDLE AUTO-DELETE (E.G. A DEV MACHINE GOING TO SLEEP)
			await adminClient.DeleteSubscriptionAsync(topicName, subscriptionName, TestContext.Current.CancellationToken);

			// THE PROCESSOR'S OWN BACKGROUND RECEIVE LOOP SHOULD EVENTUALLY HIT MessagingEntityNotFound ON ITS OWN,
			// RAISING SubscriptionMissing, WITHOUT NEEDING ANY MESSAGE TO BE PUBLISHED
			var completed = await Task.WhenAny(missingSignaled.Task, Task.Delay(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));

			Assert.Same(missingSignaled.Task, completed);
			Assert.True(await adminClient.SubscriptionExistsAsync(topicName, subscriptionName, TestContext.Current.CancellationToken));
		}
		finally
		{
			await TryDeleteTopicAsync(adminClient, topicName);
		}
	}

	[Fact]
	public async Task DisposeAsyncDeletesTheSubscriptionButNotTheTopicAsync()
	{
		SkipIfNoBrokerConfigured();

		var topicName = CreateUniqueTopicName(nameof(DisposeAsyncDeletesTheSubscriptionButNotTheTopicAsync));
		const string subscriptionName = "unprovision-test-subscription";
		var (_, adminClient) = CreateClients();

		try
		{
			var adminWrapper = CreateAdminWrapper(adminClient, topicName, subscriptionName);
			await adminWrapper.EnsureTopicAsync();
			await adminWrapper.EnsureSubscriptionAsync();

			Assert.True(await adminClient.SubscriptionExistsAsync(topicName, subscriptionName, TestContext.Current.CancellationToken));

			await adminWrapper.DisposeAsync();

			Assert.False(await adminClient.SubscriptionExistsAsync(topicName, subscriptionName, TestContext.Current.CancellationToken));
			Assert.True(await adminClient.TopicExistsAsync(topicName, TestContext.Current.CancellationToken));
		}
		finally
		{
			await TryDeleteTopicAsync(adminClient, topicName);
		}
	}

	[Fact]
	public async Task ClientWrapperWorksAgainstAnExternallyProvisionedSubscriptionWithoutAnAdminWrapperAsync()
	{
		SkipIfNoBrokerConfigured();

		var topicName = CreateUniqueTopicName(nameof(ClientWrapperWorksAgainstAnExternallyProvisionedSubscriptionWithoutAnAdminWrapperAsync));
		const string subscriptionName = "no-provisioner-test-subscription";
		var (_, adminClient) = CreateClients();

		try
		{
			await adminClient.CreateTopicAsync(topicName, TestContext.Current.CancellationToken);
			await adminClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName), TestContext.Current.CancellationToken);

			var (client, _) = CreateClients();
			await using var communicator = CreateCommunicator(client, topicName, subscriptionName);

			var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			await communicator.Subscribe(_ => { receivedTcs.TrySetResult(true); return Task.CompletedTask; });

			await communicator.SendMessage(new ServiceBusMessage(new BinaryData(new byte[] { 7 })), TestContext.Current.CancellationToken);

			var completed = await Task.WhenAny(receivedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
			Assert.NotSame(receivedTcs.Task, completed);

			await communicator.DisposeAsync();

			Assert.True(await adminClient.SubscriptionExistsAsync(topicName, subscriptionName, TestContext.Current.CancellationToken));
		}
		finally
		{
			await TryDeleteTopicAsync(adminClient, topicName);
		}
	}

	private static async Task TryDeleteTopicAsync(ServiceBusAdministrationClient adminClient, string topicName)
	{
		await adminClient.DeleteTopicAsync(topicName);
	}
}
