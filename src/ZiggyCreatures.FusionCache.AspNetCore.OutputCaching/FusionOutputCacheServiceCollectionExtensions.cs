using System;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.AspNetCore.OutputCaching;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up OutputCache based on FusionCache in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionOutputCacheServiceCollectionExtensions
{
	/// <summary>
	/// Adds services to the specified <see cref="IServiceCollection" /> to have output caching based on FusionCache.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupAction">An <see cref="Action{FusionOutputCacheOptions}"/> to configure the provided <see cref="FusionOutputCacheOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionOutputCache(this IServiceCollection services, Action<FusionOutputCacheOptions>? setupAction = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions();

		if (setupAction is not null)
			services.Configure(setupAction);

		services.AddSingleton<IOutputCacheStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<FusionOutputCacheOptions>>().Value;
			var cacheProvider = sp.GetRequiredService<IFusionCacheProvider>();

			IFusionCache cache;
			if (options.CacheName is null)
			{
				cache = cacheProvider.GetDefaultCache();
			}
			else
			{
				cache = cacheProvider.GetCache(options.CacheName);
			}

			return new FusionOutputCacheStore(cache);
		});

		return services;
	}
}
