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
	/// <br/>
	/// This will be removed in a future release: please use the version of this method that uses the more common and robust Builder approach.
	/// <br/>
	/// The new call corresponding to AddFusionCache() is AddFusionCache(b => b.TryWithAutoSetup()) , see the docs for more.
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
	[Obsolete("This will be removed in a future release: please use the version of this method that uses the more common and robust Builder approach. The new call corresponding to this is AddFusionCache(b => b.WithAutoSetup())")]
	public static IServiceCollection AddFusionCache(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null, bool useDistributedCacheIfAvailable = true, bool ignoreMemoryDistributedCache = true, Action<IServiceProvider, IFusionCache>? setupCacheAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		services.AddFusionCacheProvider();

		// TODO: use the
		services.AddFusionCache(b =>
		{
			if (setupOptionsAction is not null)
				b = b.WithOptions(setupOptionsAction);

			b = b.TryWithRegisteredMemoryCache();

			if (useDistributedCacheIfAvailable)
				b = b.TryWithRegisteredDistributedCache(ignoreMemoryDistributedCache, false);

			b = b.TryWithRegisteredBackplane();

			b = b.WithAllRegisteredPlugins();

			if (setupCacheAction is not null)
				b = b.WithPostSetup(setupCacheAction);

			return b;
		});

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
	/// <param name="setupBuilderAction">The building logic to apply, usually consisting of a series of calls to common pre-built ext methods.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, string cacheName, Func<IFusionCacheBuilder, IFusionCacheBuilder> setupBuilderAction)
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

		services.AddSingleton<IFusionCache>(serviceProvider =>
		{
			IFusionCacheBuilder b = new FusionCacheBuilder(cacheName);

			if (setupBuilderAction is not null)
			{
				b = setupBuilderAction(b);
			}

			return b.Build(serviceProvider);
		});

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
	/// <param name="setupBuilderAction">The building logic to apply, usually consisting of a series of calls to common pre-built ext methods.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, Func<IFusionCacheBuilder, IFusionCacheBuilder> setupBuilderAction)
	{
		return services.AddFusionCache(FusionCacheOptions.DefaultCacheName, setupBuilderAction);
	}
}
