using Azure.Messaging.ServiceBus;
using FusionCacheTests.AzureServiceBus.TestDoubles;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusBackplaneTests
	: AbstractTests
{
	public AzureServiceBusBackplaneTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private static (BackplaneSubscriptionOptions Options, List<BackplaneMessage> ReceivedMessages, List<bool> ConnectReconnectionFlags) CreateSubscriptionOptions(
		string cacheName = "TestCache",
		string cacheInstanceId = "TestInstance",
		string? channelName = "TestCache.Backplane:v1")
	{
		var receivedMessages = new List<BackplaneMessage>();
		var connectReconnectionFlags = new List<bool>();

		void IncomingMessageHandler(BackplaneMessage msg) => receivedMessages.Add(msg);
		ValueTask IncomingMessageHandlerAsync(BackplaneMessage msg) { receivedMessages.Add(msg); return default; }
		void ConnectHandler(BackplaneConnectionInfo info) => connectReconnectionFlags.Add(info.IsReconnection);
		ValueTask ConnectHandlerAsync(BackplaneConnectionInfo info) { connectReconnectionFlags.Add(info.IsReconnection); return default; }

		var options = new BackplaneSubscriptionOptions(
			cacheName,
			cacheInstanceId,
			channelName,
			ConnectHandler,
			IncomingMessageHandler,
			ConnectHandlerAsync,
			IncomingMessageHandlerAsync
		);

		return (options, receivedMessages, connectReconnectionFlags);
	}

	private static ServiceBusReceivedMessage CreateReceivedMessage(BackplaneMessage message, string? subject)
	{
		return ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: new BinaryData(BackplaneMessage.ToByteArray(message)),
			subject: subject
		);
	}

	private AzureServiceBusBackplane CreateBackplane(FakeAzureServiceBusCommunicator communicator, IAzureServiceBusAdminWrapper? adminWrapper = null)
	{
		return new AzureServiceBusBackplane(communicator, adminWrapper ?? NoOpAzureServiceBusAdminWrapper.Instance, CreateXUnitLogger<AzureServiceBusBackplane>());
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenOptionsIsNullAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());

		await Assert.ThrowsAsync<ArgumentNullException>(() => backplane.SubscribeAsync(null!).AsTask());
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenChannelNameIsNullAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());
		var (options, _, _) = CreateSubscriptionOptions(channelName: null);

		await Assert.ThrowsAsync<NullReferenceException>(() => backplane.SubscribeAsync(options).AsTask());
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenCacheNameIsNullAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());
		var (options, _, _) = CreateSubscriptionOptions(cacheName: null!);

		await Assert.ThrowsAsync<NullReferenceException>(() => backplane.SubscribeAsync(options).AsTask());
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenCacheInstanceIdIsNullAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());
		var (options, _, _) = CreateSubscriptionOptions(cacheInstanceId: null!);

		await Assert.ThrowsAsync<NullReferenceException>(() => backplane.SubscribeAsync(options).AsTask());
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenBothIncomingMessageHandlersAreNullAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());
		var options = new BackplaneSubscriptionOptions(
			"TestCache",
			"TestInstance",
			"TestCache.Backplane:v1",
			connectHandler: null,
			incomingMessageHandler: null,
			connectHandlerAsync: null,
			incomingMessageHandlerAsync: null
		);

		await Assert.ThrowsAsync<ArgumentException>(() => backplane.SubscribeAsync(options).AsTask());
	}

	[Fact]
	public async Task SubscribeAsyncCanBeCalledAgainWithCurrentImplementationAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options1, _, _) = CreateSubscriptionOptions();
		var (options2, _, _) = CreateSubscriptionOptions(cacheName: "OtherCache", cacheInstanceId: "OtherInstance");

		await backplane.SubscribeAsync(options1);

		await backplane.SubscribeAsync(options2);

		Assert.Equal(2, fake.SubscribeCallCount);
	}

	[Fact]
	public async Task SubscribeAsyncCallsCommunicatorSubscribeExactlyOnceAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		Assert.Equal(1, fake.SubscribeCallCount);
		Assert.NotNull(fake.SubscribedHandler);
	}

	[Fact]
	public async Task SubscribeAsyncRunsAdminWrapperBeforeClientWrapperSubscribeAsync()
	{
		var callLog = new List<string>();
		var adminWrapper = new FakeAzureServiceBusAdminWrapper(callLog);
		var communicator = new FakeAzureServiceBusCommunicator(callLog);
		var backplane = CreateBackplane(communicator, adminWrapper);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		Assert.Equal(new[] { nameof(IAzureServiceBusAdminWrapper.EnsureTopicAsync), nameof(IAzureServiceBusAdminWrapper.EnsureSubscriptionAsync), nameof(IAzureServiceBusClientWrapper.Subscribe) }, callLog);
	}

	[Fact]
	public async Task UnsubscribeAsyncRunsClientWrapperUnsubscribeBeforeAdminWrapperDisposeAsync()
	{
		var callLog = new List<string>();
		var adminWrapper = new FakeAzureServiceBusAdminWrapper(callLog);
		var communicator = new FakeAzureServiceBusCommunicator(callLog);
		var backplane = CreateBackplane(communicator, adminWrapper);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);
		callLog.Clear();

		await backplane.UnsubscribeAsync();

		Assert.Equal(new[] { nameof(IAzureServiceBusClientWrapper.Unsubscribe), nameof(IAsyncDisposable.DisposeAsync) }, callLog);
	}

	[Fact]
	public async Task SubscriptionMissingTriggersAdminWrapperEnsureSubscriptionAsync()
	{
		var adminWrapper = new FakeAzureServiceBusAdminWrapper();
		var communicator = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(communicator, adminWrapper);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		Assert.Equal(1, adminWrapper.EnsureSubscriptionCallCount);

		await communicator.RaiseSubscriptionMissingAsync();

		Assert.Equal(2, adminWrapper.EnsureSubscriptionCallCount);
	}

	[Fact]
	public async Task UnsubscribeAsyncStopsReactingToSubscriptionMissingAsync()
	{
		var adminWrapper = new FakeAzureServiceBusAdminWrapper();
		var communicator = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(communicator, adminWrapper);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);
		await backplane.UnsubscribeAsync();

		var countAfterUnsubscribe = adminWrapper.EnsureSubscriptionCallCount;

		await communicator.RaiseSubscriptionMissingAsync();

		Assert.Equal(countAfterUnsubscribe, adminWrapper.EnsureSubscriptionCallCount);
	}

	[Fact]
	public async Task IncomingMessageWithMatchingSubjectIsDispatchedToAsyncHandlerOnlyAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, receivedMessages, _) = CreateSubscriptionOptions(cacheName: "TestCache");

		await backplane.SubscribeAsync(options);

		var originalMessage = BackplaneMessage.CreateForEntrySet("source-instance", "my-key", 12345L);
		var receivedMessage = CreateReceivedMessage(originalMessage, subject: "TestCache");

		await fake.SubscribedHandler!(receivedMessage);

		// ONLY THE ASYNC HANDLER SHOULD HAVE FIRED (NOT BOTH), OTHERWISE THE SAME MESSAGE WOULD BE PROCESSED TWICE
		var received = Assert.Single(receivedMessages);
		Assert.Equal(originalMessage.SourceId, received.SourceId);
		Assert.Equal(originalMessage.CacheKey, received.CacheKey);
		Assert.Equal(originalMessage.Action, received.Action);
		Assert.Equal(originalMessage.Timestamp, received.Timestamp);
	}

	[Fact]
	public async Task IncomingMessageWithMismatchedSubjectIsIgnoredAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, receivedMessages, _) = CreateSubscriptionOptions(cacheName: "TestCache");

		await backplane.SubscribeAsync(options);

		var originalMessage = BackplaneMessage.CreateForEntrySet("source-instance", "my-key", 12345L);
		var receivedMessage = CreateReceivedMessage(originalMessage, subject: "SomeOtherCache");

		await fake.SubscribedHandler!(receivedMessage);

		Assert.Empty(receivedMessages);
	}

	[Fact]
	public async Task SubscribeAsyncInvokesConnectHandlerOnceWithIsReconnectionFalseAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, _, connectReconnectionFlags) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		// ONLY THE ASYNC HANDLER SHOULD HAVE FIRED (NOT BOTH)
		var flag = Assert.Single(connectReconnectionFlags);
		Assert.False(flag);
	}

	[Fact]
	public async Task PublishAsyncSendsMessageWithExpectedSubjectBodyAndTtlAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, _, _) = CreateSubscriptionOptions(cacheName: "TestCache");

		await backplane.SubscribeAsync(options);

		var message = BackplaneMessage.CreateForEntrySet("source-instance", "my-key", 987654321L);
		var entryOptions = new FusionCacheEntryOptions(TimeSpan.FromMinutes(10));

		await backplane.PublishAsync(message, entryOptions,TestContext.Current.CancellationToken);

		var sent = Assert.Single(fake.SentMessages);
		Assert.Equal("TestCache", sent.Subject);

		var roundTripped = BackplaneMessage.FromByteArray(sent.Body.ToArray());
		Assert.Equal(message.SourceId, roundTripped.SourceId);
		Assert.Equal(message.CacheKey, roundTripped.CacheKey);
		Assert.Equal(message.Action, roundTripped.Action);
		Assert.Equal(message.Timestamp, roundTripped.Timestamp);
	}

	[Fact]
	public async Task UnsubscribeAsyncCallsCommunicatorWithTheSameHandlerAsync()
	{
		var fake = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(fake);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);
		var subscribedHandler = fake.SubscribedHandler;

		await backplane.UnsubscribeAsync();

		Assert.Same(subscribedHandler, fake.UnsubscribedHandler);
	}

	[Fact]
	public void SubscribeBlocksUntilCommunicatorSubscribeCompletes()
	{
		var fake = new FakeAzureServiceBusCommunicator { SubscribeDelay = TimeSpan.FromMilliseconds(200) };
		var backplane = CreateBackplane(fake);
		var (options, _, _) = CreateSubscriptionOptions();

		var sw = System.Diagnostics.Stopwatch.StartNew();
		backplane.Subscribe(options);
		sw.Stop();

		Assert.True(sw.ElapsedMilliseconds >= 150, $"Expected Subscribe() to block for the artificial delay, but it only took {sw.ElapsedMilliseconds}ms");
		Assert.Equal(1, fake.SubscribeCallCount);
	}

	[Fact]
	public void PublishBlocksUntilCommunicatorSendMessageCompletes()
	{
		var fake = new FakeAzureServiceBusCommunicator { SendMessageDelay = TimeSpan.FromMilliseconds(200) };
		var backplane = CreateBackplane(fake);
		var (options, _, _) = CreateSubscriptionOptions(cacheName: "TestCache");

		backplane.Subscribe(options);

		var message = BackplaneMessage.CreateForEntrySet("source-instance", "my-key", 1L);
		var entryOptions = new FusionCacheEntryOptions(TimeSpan.FromMinutes(10));

		var sw = System.Diagnostics.Stopwatch.StartNew();
		backplane.Publish(message, entryOptions);
		sw.Stop();

		Assert.True(sw.ElapsedMilliseconds >= 150, $"Expected Publish() to block for the artificial delay, but it only took {sw.ElapsedMilliseconds}ms");
		Assert.Single(fake.SentMessages);
	}
}
