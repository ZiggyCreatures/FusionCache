using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class StackExchangeRedisBackplaneServiceCollectionExtensions
{
	/// <summary>
	/// Adds a Redis based implementation of a backplane to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{RedisBackplaneOptions}"/> to configure the provided <see cref="RedisBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheStackExchangeRedisBackplane(this IServiceCollection services, Action<RedisBackplaneOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAdd(ServiceDescriptor.Transient<IFusionCacheBackplane>(serviceProvider =>
		{
			var logger = serviceProvider.GetService<ILogger<RedisBackplane>>();

			var backplane = new RedisBackplane(
				serviceProvider.GetRequiredService<IOptions<RedisBackplaneOptions>>(),
				logger: logger
			);

			return backplane;
		}));

		return services;
	}
}
