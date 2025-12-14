using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Locking.MemoryDistributed;

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
}
