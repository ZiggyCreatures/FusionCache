using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class StackExchangeRedisBackplaneExtensions
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

		services.TryAddTransient<RedisBackplane>();
		services.TryAddTransient<IFusionCacheBackplane, RedisBackplane>();

		return services;
	}

	/// <summary>
	/// Adds a Redis based implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{RedisBackplaneOptions}"/> to configure the provided <see cref="RedisBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithStackExchangeRedisBackplane(this IFusionCacheBuilder builder, Action<RedisBackplaneOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithBackplane(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<RedisBackplaneOptions>>().Get(builder.CacheName);
				if (setupOptionsAction is not null)
					setupOptionsAction?.Invoke(options);
				var logger = sp.GetService<ILogger<RedisBackplane>>();

				return new RedisBackplane(options, logger);
			})
		;
	}
}
