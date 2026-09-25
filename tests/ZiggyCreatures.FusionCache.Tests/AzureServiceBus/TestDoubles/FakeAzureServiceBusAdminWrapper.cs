using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;

namespace FusionCacheTests.AzureServiceBus.TestDoubles
{
	internal sealed class FakeAzureServiceBusAdminWrapper : IAzureServiceBusAdminWrapper
	{
		public FakeAzureServiceBusAdminWrapper(List<string>? callLog = null)
		{
			_callLog = callLog;
		}

		private readonly List<string>? _callLog;

		public int EnsureTopicCallCount { get; private set; }
		public int EnsureSubscriptionCallCount { get; private set; }
		public int DisposeCallCount { get; private set; }

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

		public ValueTask DisposeAsync()
		{
			_callLog?.Add(nameof(DisposeAsync));
			DisposeCallCount++;
			return default;
		}
	}

}
