using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// A set of extension methods that add some commonly used setup actions to an instance of a <see cref="IFusionCacheBuilder"/> object.
/// </summary>
public static partial class FusionCacheBuilderExtMethods
{
	/// <summary>
	/// Specify a <see cref="FusionCacheOptions"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="options">The <see cref="FusionCacheOptions"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithOptions(this IFusionCacheBuilder builder, FusionCacheOptions options)
	{
		builder.Options = options;

		return builder;
	}

	/// <summary>
	/// Specify a custom logic to further configure the <see cref="FusionCacheOptions"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="action">The custom action that configure the <see cref="FusionCacheOptions"/> object.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithOptions(this IFusionCacheBuilder builder, Action<FusionCacheOptions> action)
	{
		builder.SetupOptionsAction += action;

		return builder;
	}

	/// <summary>
	/// Specify a <see cref="FusionCacheEntryOptions"/> instance to be used as the <see cref="FusionCacheOptions.DefaultEntryOptions"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="options">The <see cref="FusionCacheEntryOptions"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDefaultEntryOptions(this IFusionCacheBuilder builder, FusionCacheEntryOptions options)
	{
		builder.DefaultEntryOptions = options;

		return builder;
	}

	/// <summary>
	/// Specify a custom logic to further configure the <see cref="FusionCacheEntryOptions"/> instance to be used as the <see cref="FusionCacheOptions.DefaultEntryOptions"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="action">The custom action that configure the <see cref="FusionCacheOptions"/> object.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDefaultEntryOptions(this IFusionCacheBuilder builder, Action<FusionCacheEntryOptions> action)
	{
		builder.SetupDefaultEntryOptionsAction += action;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IMemoryCache"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredMemoryCache(this IFusionCacheBuilder builder)
	{
		builder.UseRegisteredMemoryCache = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IMemoryCache"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryCache(this IFusionCacheBuilder builder, IMemoryCache memoryCache)
	{
		builder.UseRegisteredMemoryCache = false;
		builder.MemoryCache = memoryCache;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IDistributedCache"/> service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user</param>
	/// <param name="throwIfMissingSerializer">Indicates if an exception should be thrown in case a valid <see cref="IFusionCacheSerializer"/> has not been provided: this is useful to avoid being convinced of having a distributed cache when, in reality, that is not the case since a serializer is needed for it to work</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredDistributedCache(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true, bool throwIfMissingSerializer = true)
	{
		builder.UseRegisteredDistributedCache = true;
		builder.IgnoreRegisteredMemoryDistributedCache = ignoreMemoryDistributedCache;
		builder.ThrowIfMissingSerializer = throwIfMissingSerializer;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IDistributedCache"/> and a custom <see cref="IFusionCacheSerializer"/> instances to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="distributedCache">The <see cref="IDistributedCache"/> instance to use.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDistributedCache(this IFusionCacheBuilder builder, IDistributedCache distributedCache, IFusionCacheSerializer serializer)
	{
		builder.UseRegisteredDistributedCache = false;
		builder.DistributedCache = distributedCache;
		builder.Serializer = serializer;

		return builder;
	}

	/// <summary>
	/// Indicates that the builder should not use a distributed case.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutDistributedCache(this IFusionCacheBuilder builder)
	{
		builder.UseRegisteredDistributedCache = false;
		builder.DistributedCache = null;
		builder.Serializer = null;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredBackplane(this IFusionCacheBuilder builder)
	{
		builder.UseRegisteredBackplane = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheBackplane"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="backplane">The <see cref="IFusionCacheBackplane"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithBackplane(this IFusionCacheBuilder builder, IFusionCacheBackplane backplane)
	{
		builder.UseRegisteredBackplane = false;
		builder.Backplane = backplane;

		return builder;
	}

	/// <summary>
	/// Indicates that the builder should not use a backplane.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutBackplane(this IFusionCacheBuilder builder)
	{
		builder.UseRegisteredBackplane = false;
		builder.Backplane = null;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use any available <see cref="IFusionCachePlugin"/> services registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAllRegisteredPlugins(this IFusionCacheBuilder builder)
	{
		builder.UseAllRegisteredPlugins = true;

		return builder;
	}

	/// <summary>
	/// Adds a custom <see cref="IFusionCachePlugin"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="plugin">The <see cref="IFusionCachePlugin"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithPlugin(this IFusionCacheBuilder builder, IFusionCachePlugin plugin)
	{
		builder.UseAllRegisteredPlugins = false;
		builder.Plugins.Add(plugin);

		return builder;
	}

	/// <summary>
	/// Adds one or more custom <see cref="IFusionCachePlugin"/> instances to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="plugins">The <see cref="IFusionCachePlugin"/> instances to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithPlugins(this IFusionCacheBuilder builder, params IFusionCachePlugin[] plugins)
	{
		builder.UseAllRegisteredPlugins = false;
		builder.Plugins.AddRange(plugins);

		return builder;
	}

	/// <summary>
	/// Indicates that the builder should not use any plugins.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutPlugins(this IFusionCacheBuilder builder)
	{
		builder.UseAllRegisteredPlugins = false;
		builder.Plugins.Clear();

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use all the compatible services registered in the DI container, like a distributed cache, a backplane, plugins, etc.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAllRegisteredComponents(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true)
	{
		return builder
			.WithRegisteredMemoryCache()
			.WithRegisteredDistributedCache(ignoreMemoryDistributedCache)
			.WithRegisteredBackplane()
			.WithAllRegisteredPlugins()
		;
	}

	/// <summary>
	/// Specify a custom post-setup action, that will be invoked just after the creation of the FusionCache instance, and before returning it to the caller.
	/// <br/><br/>
	/// <strong>NOTE:</strong> it is possible to call this multiple times, to add multiple post-setup calls one after the other to combine them for a powerful result.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="action">The custom post-setup action to be added to the builder pipeline.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithPostSetup(this IFusionCacheBuilder builder, Action<IServiceProvider, IFusionCache> action)
	{
		builder.PostSetupAction += action;

		return builder;
	}
}
