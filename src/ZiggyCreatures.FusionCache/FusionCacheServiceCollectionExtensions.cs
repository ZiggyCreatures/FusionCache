using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheServiceCollectionExtensions
{
	/// <summary>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{FusionCacheOptions}"/> to configure the provided <see cref="FusionCacheOptions"/>.</param>
	/// <param name="useDistributedCacheIfAvailable">Automatically wires up an <see cref="IDistributedCache"/> if it has been registered in the Dependendy Injection container </param>
	/// <param name="ignoreMemoryDistributedCache">If the registered <see cref="IDistributedCache"/> found is an instance of <see cref="MemoryDistributedCache"/> (typical when using asp.net) it will be ignored, since it is completely useless (and will consume cpu and memory).</param>
	/// <param name="setupCacheAction">The <see cref="Action{IServiceProvider,FusionCacheOptions}"/> to configure the newly created <see cref="IFusionCache"/> instance.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null, bool useDistributedCacheIfAvailable = true, bool ignoreMemoryDistributedCache = true, Action<IServiceProvider, IFusionCache>? setupCacheAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.Add(ServiceDescriptor.Singleton<IFusionCache>(serviceProvider =>
		{
			var logger = serviceProvider.GetService<ILogger<FusionCache>>();

			var cache = new FusionCache(
				serviceProvider.GetRequiredService<IOptions<FusionCacheOptions>>(),
				serviceProvider.GetService<IMemoryCache>(),
				logger: logger
			);

			// DISTRIBUTED CACHE
			if (useDistributedCacheIfAvailable)
			{
				var distributedCache = serviceProvider.GetService<IDistributedCache>();

				if (ignoreMemoryDistributedCache && distributedCache is MemoryDistributedCache)
				{
					distributedCache = null;
				}

				if (distributedCache is not null)
				{
					var serializer = serviceProvider.GetService<IFusionCacheSerializer>();

					if (serializer is null)
					{
						if (logger?.IsEnabled(LogLevel.Warning) ?? false)
							logger.LogWarning("FUSION: a usable implementation of IDistributedCache was found (CACHE={DistributedCacheType}) but no implementation of IFusionCacheSerializer was found, so the distributed cache subsystem has not been set up", distributedCache.GetType().FullName);
					}
					else
					{
						cache.SetupDistributedCache(distributedCache, serializer);
					}
				}
			}

			// BACKPLANE
			var backplane = serviceProvider.GetService<IFusionCacheBackplane>();

			if (backplane is not null)
			{
				cache.SetupBackplane(backplane);
			}

			// PLUGINS
			foreach (var plugin in serviceProvider.GetServices<IFusionCachePlugin>())
			{
				try
				{
					cache.AddPlugin(plugin);
				}
				catch
				{
					// EMPTY: EVERYTHING HAS BEEN ALREADY LOGGED, IF NECESSARY
				}
			}

			// CUSTOM SETUP ACTION
			setupCacheAction?.Invoke(serviceProvider, cache);

			return cache;
		}));

		return services;
	}

	/// <summary>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="cacheName">The name of the cache. It also automatically sets <see cref="FusionCacheOptions.CacheName"/>.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{FusionCacheOptions}"/> to configure the provided <see cref="FusionCacheOptions"/>.</param>
	/// <param name="useDistributedCacheIfAvailable">Automatically wires up an <see cref="IDistributedCache"/> if it has been registered in the Dependendy Injection container </param>
	/// <param name="ignoreMemoryDistributedCache">If the registered <see cref="IDistributedCache"/> found is an instance of <see cref="MemoryDistributedCache"/> (typical when using asp.net) it will be ignored, since it is completely useless (and will consume cpu and memory).</param>
	/// <param name="setupCacheAction">The <see cref="Action{IServiceProvider,FusionCacheOptions}"/> to configure the newly created <see cref="IFusionCache"/> instance.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, string cacheName, Action<FusionCacheOptions>? setupOptionsAction = null, bool useDistributedCacheIfAvailable = true, bool ignoreMemoryDistributedCache = true, Action<IServiceProvider, IFusionCache>? setupCacheAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (cacheName is null)
			throw new ArgumentNullException(nameof(cacheName));

		services.AddOptions();

		services.Configure<FusionCacheOptions>(cacheName, opt =>
		{
			opt.CacheName = cacheName;
			if (setupOptionsAction is not null)
				setupOptionsAction(opt);
		});

		services.TryAddSingleton<IFusionCacheProvider, FusionCacheProvider>();

		services.Add(ServiceDescriptor.Singleton<IFusionCache>(serviceProvider =>
		{
			var logger = serviceProvider.GetService<ILogger<FusionCache>>();

			var options = serviceProvider.GetRequiredService<IOptionsMonitor<FusionCacheOptions>>().Get(cacheName);

			var cache = new FusionCache(
				options,
				serviceProvider.GetService<IMemoryCache>(),
				logger: logger
			);

			// DISTRIBUTED CACHE
			if (useDistributedCacheIfAvailable)
			{
				var distributedCache = serviceProvider.GetService<IDistributedCache>();

				if (ignoreMemoryDistributedCache && distributedCache is MemoryDistributedCache)
				{
					distributedCache = null;
				}

				if (distributedCache is not null)
				{
					var serializer = serviceProvider.GetService<IFusionCacheSerializer>();

					if (serializer is null)
					{
						if (logger?.IsEnabled(LogLevel.Warning) ?? false)
							logger.LogWarning("FUSION: a usable implementation of IDistributedCache was found (CACHE={DistributedCacheType}) but no implementation of IFusionCacheSerializer was found, so the distributed cache subsystem has not been set up", distributedCache.GetType().FullName);
					}
					else
					{
						cache.SetupDistributedCache(distributedCache, serializer);
					}
				}
			}

			// BACKPLANE
			var backplane = serviceProvider.GetService<IFusionCacheBackplane>();

			if (backplane is not null)
			{
				cache.SetupBackplane(backplane);
			}

			// PLUGINS
			foreach (var plugin in serviceProvider.GetServices<IFusionCachePlugin>())
			{
				try
				{
					cache.AddPlugin(plugin);
				}
				catch
				{
					// EMPTY: EVERYTHING HAS BEEN ALREADY LOGGED, IF NECESSARY
				}
			}

			// CUSTOM SETUP ACTION
			setupCacheAction?.Invoke(serviceProvider, cache);

			return cache;
		}));

		return services;
	}

	/// <summary>
	/// Adds the standard implementation of <see cref="IFusionCache"/> to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="cacheName">The name of the cache. It also automatically sets <see cref="FusionCacheOptions.CacheName"/>.</param>
	/// <param name="cache">The direct FusionCache instance.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCache(this IServiceCollection services, string cacheName, IFusionCache cache)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (cacheName is null)
			throw new ArgumentNullException(nameof(cacheName));

		if (cache is null)
			throw new ArgumentNullException(nameof(cache));

		if (cacheName != cache.CacheName)
			throw new ArgumentException($"The provided FusionCache instance has a name ({cache.CacheName}) that is different from the provided name ({cacheName})", nameof(cacheName));

		services.AddOptions();

		services.Configure<FusionCacheOptions>(cacheName, opt =>
		{
			opt.CacheName = cacheName;
		});

		services.TryAddSingleton<IFusionCacheProvider, FusionCacheProvider>();

		services.Add(ServiceDescriptor.Singleton<IFusionCache>(cache));

		return services;
	}
}
