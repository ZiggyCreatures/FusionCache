using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
}
