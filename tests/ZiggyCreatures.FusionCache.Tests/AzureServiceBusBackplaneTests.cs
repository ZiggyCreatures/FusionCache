using Azure.Messaging.ServiceBus;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace FusionCacheTests;

public class AzureServiceBusBackplaneTests
	: AbstractTests
{
	public AzureServiceBusBackplaneTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private sealed class FakeAzureServiceBusCommunicator
		: IAzureServiceBusClientWrapper
	{
		public FakeAzureServiceBusCommunicator(List<string>? callLog = null)
		{
			_callLog = callLog;
		}

		private readonly List<string>? _callLog;

		public int SubscribeCallCount { get; private set; }
		public Func<ServiceBusReceivedMessage, Task>? SubscribedHandler { get; private set; }
		public Func<ServiceBusReceivedMessage, Task>? UnsubscribedHandler { get; private set; }
		public List<ServiceBusMessage> SentMessages { get; } = new();

		public TimeSpan? SubscribeDelay { get; set; }
		public TimeSpan? SendMessageDelay { get; set; }

		public event Func<Task>? SubscriptionMissing;

		public async Task RaiseSubscriptionMissingAsync()
		{
			var handler = SubscriptionMissing;
			if (handler is not null)
				await handler();
		}

		public async Task Subscribe(Func<ServiceBusReceivedMessage, Task> handler)
		{
			if (SubscribeDelay.HasValue)
				await Task.Delay(SubscribeDelay.Value);

			_callLog?.Add(nameof(Subscribe));
			SubscribeCallCount++;
			SubscribedHandler = handler;
		}

		public Task Unsubscribe(Func<ServiceBusReceivedMessage, Task> handler)
		{
			_callLog?.Add(nameof(Unsubscribe));
			UnsubscribedHandler = handler;
			return Task.CompletedTask;
		}

		public async Task SendMessage(ServiceBusMessage message, CancellationToken cancellationToken)
		{
			if (SendMessageDelay.HasValue)
				await Task.Delay(SendMessageDelay.Value);

			SentMessages.Add(message);
		}
	}

	private sealed class FakeAzureServiceBusProvisioner
		: IAzureServiceBusAdminWrapper
	{
		public FakeAzureServiceBusProvisioner(List<string>? callLog = null)
		{
			_callLog = callLog;
		}

		private readonly List<string>? _callLog;

		public int EnsureTopicCallCount { get; private set; }
		public int EnsureSubscriptionCallCount { get; private set; }
		public int UnprovisionCallCount { get; private set; }

		public ValueTask EnsureTopicAsync()
		{
			_callLog?.Add(nameof(EnsureTopicAsync));
			EnsureTopicCallCount++;
			return default;
		}

		public ValueTask EnsureSubscriptionAsync()
		{
			_callLog?.Add(nameof(EnsureSubscriptionAsync));
			EnsureSubscriptionCallCount++;
			return default;
		}

		public ValueTask UnprovisionAsync()
		{
			_callLog?.Add(nameof(UnprovisionAsync));
			UnprovisionCallCount++;
			return default;
		}
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

	private AzureServiceBusBackplane CreateBackplane(FakeAzureServiceBusCommunicator communicator, IAzureServiceBusAdminWrapper? provisioner = null)
	{
		return new AzureServiceBusBackplane(communicator, provisioner ?? NoOpAzureServiceBusAdminWrapper.Instance, CreateXUnitLogger<AzureServiceBusBackplane>());
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
	public async Task SubscribeAsyncThrowsWhenAlreadyInitializedAsync()
	{
		var backplane = CreateBackplane(new FakeAzureServiceBusCommunicator());
		var (options1, _, _) = CreateSubscriptionOptions();
		var (options2, _, _) = CreateSubscriptionOptions(cacheName: "OtherCache", cacheInstanceId: "OtherInstance");

		await backplane.SubscribeAsync(options1);

		await Assert.ThrowsAsync<InvalidOperationException>(() => backplane.SubscribeAsync(options2).AsTask());
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
	public async Task SubscribeAsyncRunsProvisionerBeforeCommunicatorSubscribeAsync()
	{
		var callLog = new List<string>();
		var provisioner = new FakeAzureServiceBusProvisioner(callLog);
		var communicator = new FakeAzureServiceBusCommunicator(callLog);
		var backplane = CreateBackplane(communicator, provisioner);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		Assert.Equal(new[] { nameof(IAzureServiceBusAdminWrapper.EnsureTopicAsync), nameof(IAzureServiceBusAdminWrapper.EnsureSubscriptionAsync), nameof(IAzureServiceBusClientWrapper.Subscribe) }, callLog);
	}

	[Fact]
	public async Task UnsubscribeAsyncRunsCommunicatorUnsubscribeBeforeProvisionerUnprovisionAsync()
	{
		var callLog = new List<string>();
		var provisioner = new FakeAzureServiceBusProvisioner(callLog);
		var communicator = new FakeAzureServiceBusCommunicator(callLog);
		var backplane = CreateBackplane(communicator, provisioner);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);
		callLog.Clear();

		await backplane.UnsubscribeAsync();

		Assert.Equal(new[] { nameof(IAzureServiceBusClientWrapper.Unsubscribe), nameof(IAzureServiceBusAdminWrapper.UnprovisionAsync) }, callLog);
	}

	[Fact]
	public async Task SubscriptionMissingTriggersProvisionerEnsureSubscriptionAsync()
	{
		var provisioner = new FakeAzureServiceBusProvisioner();
		var communicator = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(communicator, provisioner);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);

		Assert.Equal(1, provisioner.EnsureSubscriptionCallCount);

		await communicator.RaiseSubscriptionMissingAsync();

		Assert.Equal(2, provisioner.EnsureSubscriptionCallCount);
	}

	[Fact]
	public async Task UnsubscribeAsyncStopsReactingToSubscriptionMissingAsync()
	{
		var provisioner = new FakeAzureServiceBusProvisioner();
		var communicator = new FakeAzureServiceBusCommunicator();
		var backplane = CreateBackplane(communicator, provisioner);
		var (options, _, _) = CreateSubscriptionOptions();

		await backplane.SubscribeAsync(options);
		await backplane.UnsubscribeAsync();

		var countAfterUnsubscribe = provisioner.EnsureSubscriptionCallCount;

		await communicator.RaiseSubscriptionMissingAsync();

		Assert.Equal(countAfterUnsubscribe, provisioner.EnsureSubscriptionCallCount);
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

		await backplane.PublishAsync(message, entryOptions);

		var sent = Assert.Single(fake.SentMessages);
		Assert.Equal("TestCache", sent.Subject);
		Assert.Equal(TimeSpan.FromSeconds(5) + entryOptions.Duration, sent.TimeToLive);

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
