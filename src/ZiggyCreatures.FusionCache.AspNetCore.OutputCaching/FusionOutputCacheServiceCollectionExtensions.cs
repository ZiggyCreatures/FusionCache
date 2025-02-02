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
	/// <br/><br/>
	/// <strong>NOTE:</strong> please remember to call services.AddFusionCache(...) BEFORE calling this method to register a FusionCache instance, either the default one or a specific named one (in this case please specify the same name in <see cref="FusionOutputCacheOptions.CacheName"/>).
	/// <br/><br/>
	/// <strong>NOTE:</strong> please remember to call services.AddOutputCache(...) AFTER calling this method, to setup the core OutputCache services.
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
