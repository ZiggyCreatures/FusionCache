using FusionCacheTests.Stuff;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusBackplaneExtensionsTests
	: AbstractTests
{
	public AzureServiceBusBackplaneExtensionsTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	[Fact]
	public void AddFusionCacheAzureServiceBusBackplaneRegistersAResolvableBackplane()
	{
		var services = new ServiceCollection();

		services.AddFusionCacheAzureServiceBusBackplane(opt =>
		{
			opt.ConnectionString = "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
		});

		using var serviceProvider = services.BuildServiceProvider();

		var backplane = serviceProvider.GetRequiredService<IFusionCacheBackplane>();

		Assert.NotNull(backplane);
		Assert.IsType<AzureServiceBusBackplane>(backplane);
	}

	[Fact]
	public void WithAzureServiceBusBackplaneResolvesOptionsPerCacheName()
	{
		var services = new ServiceCollection();

		services.Configure<AzureServiceBusBackplaneOptions>("Foo", opt =>
		{
			opt.ConnectionString = "Endpoint=sb://foo-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
		});

		services.AddFusionCache("Foo")
			.WithAzureServiceBusBackplane()
		;

		services.AddFusionCache("Bar")
			.WithAzureServiceBusBackplane(opt =>
			{
				opt.ConnectionString = "Endpoint=sb://bar-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
				opt.TopicName = "custom-bar-topic";
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("Foo");
		var barCache = cacheProvider.GetCache("Bar");

		var fooBackplane = TestsUtils.GetBackplane<AzureServiceBusBackplane>(fooCache);
		var fooTopicName = TestsUtils.GetAzureServiceBusCommunicatorTopicName(fooCache);
		var barBackplane = TestsUtils.GetBackplane<AzureServiceBusBackplane>(barCache);
		var barTopicName = TestsUtils.GetAzureServiceBusCommunicatorTopicName(barCache);

		Assert.True(fooCache.HasBackplane);
		Assert.NotNull(fooBackplane);
		// NO EXPLICIT TopicName WAS SET FOR "Foo": IT SHOULD DEFAULT TO THE (SANITIZED) CACHE NAME
		Assert.Equal("Foo", fooTopicName);

		Assert.True(barCache.HasBackplane);
		Assert.NotNull(barBackplane);
		// AN EXPLICIT TopicName WAS SET FOR "Bar": IT SHOULD BE USED AS-IS INSTEAD OF THE CACHE NAME
		Assert.Equal("custom-bar-topic", barTopicName);

		Assert.NotEqual(fooTopicName, barTopicName);
	}

	[Fact]
	public void WithAzureServiceBusBackplaneThrowsWhenNonAdminWithoutSubscriptionName()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo")
			.WithAzureServiceBusBackplane(opt =>
			{
				opt.ConnectionString = "Endpoint=sb://foo-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
				opt.IsAdmin = false;
				// NOTE: SubscriptionName IS DELIBERATELY LEFT UNSET HERE
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		Assert.Throws<InvalidOperationException>(() => cacheProvider.GetCache("Foo"));
	}

	[Fact]
	public void WithAzureServiceBusBackplaneAllowsNonAdminWithSubscriptionName()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo")
			.WithAzureServiceBusBackplane(opt =>
			{
				opt.ConnectionString = "Endpoint=sb://foo-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=";
				opt.IsAdmin = false;
				opt.SubscriptionName = "my-existing-subscription";
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();
		var fooCache = cacheProvider.GetCache("Foo");

		Assert.True(fooCache.HasBackplane);
		Assert.NotNull(TestsUtils.GetBackplane<AzureServiceBusBackplane>(fooCache));
	}
}
