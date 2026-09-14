using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class RedisDistributedLockerExtensions
{
	/// <summary>
	/// Adds a Redis based implementation of a distributed locker to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{RedisBackplaneOptions}"/> to configure the provided <see cref="RedisDistributedLockerOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheRedisDistributedLocker(this IServiceCollection services, Action<RedisDistributedLockerOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<RedisDistributedLocker>();
		services.TryAddTransient<IFusionCacheDistributedLocker, RedisDistributedLocker>();

		return services;
	}

	/// <summary>
	/// Adds a Redis based implementation of a distributed locker to the <see cref="IFusionCacheDistributedLocker" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{RedisDistributedLockerOptions}"/> to configure the provided <see cref="RedisDistributedLockerOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRedisDistributedLocker(this IFusionCacheBuilder builder, Action<RedisDistributedLockerOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
				.WithDistributedLocker(sp =>
				{
					var options = sp.GetService<IOptionsMonitor<RedisDistributedLockerOptions>>()?.Get(builder.CacheName);

					if (options is null)
						throw new InvalidOperationException($"Unable to find a valid {nameof(RedisDistributedLockerOptions)} instance for the current cache name '{builder.CacheName}'.");

					setupOptionsAction?.Invoke(options);

					var logger = sp.GetService<ILogger<RedisDistributedLocker>>();

					return new RedisDistributedLocker(options, logger);
				})
			;
	}
}
