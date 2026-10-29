using Testcontainers.ServiceBus;
using Xunit;

namespace FusionCacheTests;

public sealed class L1L2AzureServiceBusFixture : IAsyncLifetime
{
	private const string ConfigurationFileName = "AzureServiceBus/L1L2BackplaneTests.servicebus-emulator.json";
	private const string ConfiguredTopicName = "fusioncache-tests";
	private static readonly string[] SubscriptionNames = ["fusioncache-tests-1", "fusioncache-tests-2", "fusioncache-tests-3"];

	private readonly Lazy<ServiceBusContainer> _container = new(CreateAndStartContainer, LazyThreadSafetyMode.ExecutionAndPublication);
	private int _nextSubscriptionIndex = -1;

	public ValueTask InitializeAsync() => default;

	public async ValueTask DisposeAsync()
	{
		if (_container.IsValueCreated)
			await _container.Value.DisposeAsync();
	}

	public string ConnectionString => _container.Value.GetConnectionString();

	public string TopicName => ConfiguredTopicName;

	public string GetNextSubscriptionName()
	{
		var subscriptionIndex = Interlocked.Increment(ref _nextSubscriptionIndex) % SubscriptionNames.Length;
		return SubscriptionNames[subscriptionIndex];
	}

	private static ServiceBusContainer CreateAndStartContainer()
	{
		var configurationFilePath = Path.Combine(AppContext.BaseDirectory, ConfigurationFileName);
		var container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
			.WithAcceptLicenseAgreement(true)
			.WithConfig(configurationFilePath)
			.Build();

		container.StartAsync().GetAwaiter().GetResult();
		return container;
	}
}