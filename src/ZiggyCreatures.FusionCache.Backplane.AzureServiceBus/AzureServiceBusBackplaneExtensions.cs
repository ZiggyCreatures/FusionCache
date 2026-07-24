using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.AzureServiceBusWrapper;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class AzureServiceBusBackplaneExtensions
{
	private static AzureServiceBusBackplane BuildBackplane(IServiceProvider sp, AzureServiceBusBackplaneOptions options, string topicNameFallback)
	{
		var backplaneLogger = sp.GetService<ILogger<AzureServiceBusBackplane>>();

		var client = new ServiceBusClient(options.ConnectionString);
		var adminClient = new ServiceBusAdministrationClient(options.ConnectionString);
		var topicName = AzureServiceBusHelpers.ResolveTopicName(options.TopicName, topicNameFallback);

		string subscriptionName;
		IAzureServiceBusAdminWrapper provisioner;

		if (options.IsAdmin)
		{
			subscriptionName = options.SubscriptionName ?? AzureServiceBusHelpers.GenerateId();

			var provisionerLogger = sp.GetService<ILogger<AzureServiceBusAdminWrapper>>() ?? NullLogger<AzureServiceBusAdminWrapper>.Instance;
			provisioner = new AzureServiceBusAdminWrapper(adminClient, topicName, subscriptionName, provisionerLogger);
		}
		else
		{
			if (string.IsNullOrWhiteSpace(options.SubscriptionName))
				throw new InvalidOperationException($"{nameof(AzureServiceBusBackplaneOptions)}.{nameof(AzureServiceBusBackplaneOptions.IsAdmin)} is false, but no {nameof(AzureServiceBusBackplaneOptions.SubscriptionName)} was provided: without administrative rights, an existing subscription name must be specified upfront.");

			subscriptionName = options.SubscriptionName!;
			provisioner = NoOpAzureServiceBusAdminWrapper.Instance;
		}

		var communicatorLogger = sp.GetService<ILogger<AzureServiceBusClientWrapper>>() ?? NullLogger<AzureServiceBusClientWrapper>.Instance;
		var communicator = new AzureServiceBusClientWrapper(client, topicName, subscriptionName, communicatorLogger,options);

		return new AzureServiceBusBackplane(communicator, provisioner, backplaneLogger);
	}

	/// <summary>
	/// Adds an Azure Service Bus based implementation of a backplane to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureServiceBusBackplaneOptions}"/> to configure the provided <see cref="AzureServiceBusBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheAzureServiceBusBackplane(this IServiceCollection services, Action<AzureServiceBusBackplaneOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<IFusionCacheBackplane>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AzureServiceBusBackplaneOptions>>().Value;

			return BuildBackplane(sp, options, FusionCacheOptions.DefaultCacheName);
		});

		return services;
	}

	/// <summary>
	/// Adds an Azure Service Bus based implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureServiceBusBackplaneOptions}"/> to configure the provided <see cref="AzureServiceBusBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAzureServiceBusBackplane(this IFusionCacheBuilder builder, Action<AzureServiceBusBackplaneOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithBackplane(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<AzureServiceBusBackplaneOptions>>()?.Get(builder.CacheName);

				if (options is null)
					throw new InvalidOperationException($"Unable to find a valid {nameof(AzureServiceBusBackplaneOptions)} instance for the current cache name '{builder.CacheName}'.");

				setupOptionsAction?.Invoke(options);

				return BuildBackplane(sp, options, builder.CacheName);
			})
		;
	}
}
