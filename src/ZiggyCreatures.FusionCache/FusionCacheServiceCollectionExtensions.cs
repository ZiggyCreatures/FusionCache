using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals.Builder;
using ZiggyCreatures.Caching.Fusion.Internals.Provider;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheServiceCollectionExtensions
{
	private static IServiceCollection AddFusionCacheProvider(this IServiceCollection services)
	{
		services.TryAddSingleton<IFusionCacheProvider, FusionCacheProvider>();

		return services;
	}

	/// <summary>
	/// !!! OBSOLETE !!!
	/// <br/><br/>
	/// This will be removed in a future release: please use the version of this method that uses the more common and robust Builder approach.
	/// <br/><br/>
	/// The new call corresponding to the old <c>AddFusionCache()</c> (that did some auto-setup) is <c>AddFusionCache().TryWithAutoSetup()</c>, see the docs for more.
	/// <br/><br/>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{FusionCacheOptions}"/> to configure the provided <see cref="FusionCacheOptions"/>.</param>
	/// <param name="useDistributedCacheIfAvailable">Automatically wires up an <see cref="IDistributedCache"/> if it has been registered in the Dependendy Injection container </param>
	/// <param name="ignoreMemoryDistributedCache">If the registered <see cref="IDistributedCache"/> found is an instance of <see cref="MemoryDistributedCache"/> (typical when using asp.net) it will be ignored, since it is completely useless (and will consume cpu and memory).</param>
	/// <param name="setupCacheAction">The <see cref="Action{IServiceProvider,FusionCacheOptions}"/> to configure the newly created <see cref="IFusionCache"/> instance.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	[Obsolete("This will be removed in a future release: please use the version of this method that uses the more common and robust Builder approach. The new call corresponding to the parameterlss version of this is AddFusionCache().TryWithAutoSetup()")]
	public static IServiceCollection AddFusionCache(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null, bool useDistributedCacheIfAvailable = true, bool ignoreMemoryDistributedCache = true, Action<IServiceProvider, IFusionCache>? setupCacheAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		services.AddFusionCacheProvider();

		var builder = services.AddFusionCache();

		if (setupOptionsAction is not null)
			builder.WithOptions(setupOptionsAction);

		builder.TryWithRegisteredMemoryCache();

		if (useDistributedCacheIfAvailable)
			builder.TryWithRegisteredDistributedCache(ignoreMemoryDistributedCache, false);

		builder.TryWithRegisteredBackplane();

		builder.WithAllRegisteredPlugins();

		if (setupCacheAction is not null)
			builder.WithPostSetup(setupCacheAction);

		return services;
	}

	/// <summary>
	/// Adds a custom instance of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="cache">The custom FusionCache instance.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, IFusionCache cache)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (cache is null)
			throw new ArgumentNullException(nameof(cache));

		services.AddOptions();

		services.AddFusionCacheProvider();

		services.AddSingleton<IFusionCache>(cache);

		if (cache.CacheName == FusionCacheOptions.DefaultCacheName)
		{
			services.AddSingleton<IFusionCache>(cache);
		}
		else
		{
			services.AddSingleton<NamedCacheWrapper>(new NamedCacheWrapper(cache.CacheName, cache));
		}

		return services;
	}

	/// <summary>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// <br/><br/>
	/// <strong>NOTE: </strong> by using this method, no default logic is applied: to automatically use all the available registered components please call the WithAutoSetup() method.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="cacheName">The name of the cache. It also automatically sets <see cref="FusionCacheOptions.CacheName"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder AddFusionCache(this IServiceCollection services, string cacheName)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (cacheName is null)
			throw new ArgumentNullException(nameof(cacheName));

		services.AddOptions();

		services.Configure<FusionCacheOptions>(cacheName, opt =>
		{
			opt.CacheName = cacheName;
		});

		services.AddFusionCacheProvider();

		IFusionCacheBuilder builder = new FusionCacheBuilder(cacheName);

		if (cacheName == FusionCacheOptions.DefaultCacheName)
		{
			services.AddSingleton<IFusionCache>(serviceProvider =>
			{
				return builder.Build(serviceProvider);
			});
		}
		else
		{
			services.AddSingleton<NamedCacheWrapper>(serviceProvider =>
			{
				return new NamedCacheWrapper(builder.CacheName, () => builder.Build(serviceProvider));
			});
		}

		return builder;
	}

	/// <summary>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// <br/><br/>
	/// <strong>NOTE: </strong> by using this method, no default logic is applied: to automatically use all the available registered components please call the WithAutoSetup() method.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder AddFusionCache(this IServiceCollection services)
	{
		return services.AddFusionCache(FusionCacheOptions.DefaultCacheName);
	}
}
