using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class MemoryBackplaneExtensions
{
	/// <summary>
	/// Adds an in-memory implementation of a backplane to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{MemoryBackplaneOptions}"/> to configure the provided <see cref="MemoryBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheMemoryBackplane(this IServiceCollection services, Action<MemoryBackplaneOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<MemoryBackplane>();
		services.TryAddTransient<IFusionCacheBackplane, MemoryBackplane>();

		return services;
	}

	/// <summary>
	/// Adds an in-memory implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{MemoryBackplaneOptions}"/> to configure the provided <see cref="MemoryBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryBackplane(this IFusionCacheBuilder builder, Action<MemoryBackplaneOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithBackplane(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<MemoryBackplaneOptions>>()?.Get(builder.CacheName);

				if (options is null)
					throw new NullReferenceException($"Unable to find an instance of {nameof(MemoryBackplaneOptions)} for the cache named '{builder.CacheName}'.");

				setupOptionsAction?.Invoke(options);

				var logger = sp.GetService<ILogger<MemoryBackplane>>();

				return new MemoryBackplane(options, logger);
			})
		;
	}
}
