using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Memory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class MemoryDistributedLockerExtensions
{
	/// <summary>
	/// Adds an in-memory implementation of a distributed locker to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{MemoryDistributedLockerOptions}"/> to configure the provided <see cref="MemoryDistributedLockerOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheMemoryDistributedLocker(this IServiceCollection services, Action<MemoryDistributedLockerOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<MemoryDistributedLocker>();
		services.TryAddTransient<IFusionCacheDistributedLocker, MemoryDistributedLocker>();

		return services;
	}

	/// <summary>
	/// Adds a Redis based implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{MemoryDistributedLockerOptions}"/> to configure the provided <see cref="MemoryDistributedLockerOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryDistributedLocker(this IFusionCacheBuilder builder, Action<MemoryDistributedLockerOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithDistributedLocker(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<MemoryDistributedLockerOptions>>()?.Get(builder.CacheName);

				if (options is null)
					throw new InvalidOperationException($"Unable to find a valid {nameof(MemoryDistributedLockerOptions)} instance for the current cache name '{builder.CacheName}'.");

				setupOptionsAction?.Invoke(options);

				return new MemoryDistributedLocker(options);
			})
		;
	}
}
