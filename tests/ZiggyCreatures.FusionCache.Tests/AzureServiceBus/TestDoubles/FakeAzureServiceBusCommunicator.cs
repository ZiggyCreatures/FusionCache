using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace FusionCacheTests.AzureServiceBus.TestDoubles
{
	internal sealed class FakeAzureServiceBusCommunicator : IAzureServiceBusClientWrapper
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

		public ValueTask DisposeAsync() => default;
	}

}
