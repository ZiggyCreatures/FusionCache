using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
	/// </summary>
	public static class MemoryBackplaneServiceCollectionExtensions
	{
		/// <summary>
		/// Adds an-memory implementation of a backplane to the <see cref="IServiceCollection" />.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
		/// <param name="setupOptionsAction">The <see cref="Action{MemoryBackplaneOptions}"/> to configure the provided <see cref="MemoryBackplaneOptions"/>.</param>
		/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
		public static IServiceCollection AddFusionCacheMemoryBackplane(this IServiceCollection services, Action<MemoryBackplaneOptions>? setupOptionsAction = null)
		{
			if (services is null)
				throw new ArgumentNullException(nameof(services));

			services.AddOptions();

			if (setupOptionsAction is object)
				services.Configure(setupOptionsAction);

			services.TryAdd(ServiceDescriptor.Transient<IFusionCacheBackplane>(serviceProvider =>
			{
				var logger = serviceProvider.GetService<ILogger<MemoryBackplane>>();

				var backplane = new MemoryBackplane(
					serviceProvider.GetRequiredService<IOptions<MemoryBackplaneOptions>>(),
					logger: logger
				);

				return backplane;
			}));

			return services;
		}
	}
}
