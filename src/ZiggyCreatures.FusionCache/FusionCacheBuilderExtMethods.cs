using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// A set of extension methods that add some commonly used setup actions to an instance of a <see cref="IFusionCacheBuilder"/> object.
/// </summary>
public static partial class FusionCacheBuilderExtMethods
{
	#region OPTIONS

	/// <summary>
	/// Specify a <see cref="FusionCacheOptions"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredOptions(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.Options = null;
		builder.UseRegisteredOptions = true;

		return builder;
	}

	/// <summary>
	/// Specify a <see cref="FusionCacheOptions"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="options">The <see cref="FusionCacheOptions"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithOptions(this IFusionCacheBuilder builder, FusionCacheOptions? options)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.Options = options;

		builder.UseRegisteredOptions = options is null;

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (action is null)
			throw new ArgumentNullException(nameof(action));

		builder.SetupOptionsAction += action;

		return builder;
	}

	/// <summary>
	/// Set the cache key prefix to use.
	/// <br/><br/>
	/// <strong>EXAMPLE</strong>: if the CacheKeyPrefix specified is "MyCache:", a later call to cache.GetOrDefault("Product/123") will actually work on the cache key "MyCache:Product/123".
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="cacheKeyPrefix">The cache key prefix to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithCacheKeyPrefix(this IFusionCacheBuilder builder, string? cacheKeyPrefix)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseCacheKeyPrefix = true;
		builder.CacheKeyPrefix = cacheKeyPrefix;

		return builder;
	}

	/// <summary>
	/// Specify to use a cache key prefix, composed by the CacheName and a ":" separator.
	/// <br/><br/>
	/// <strong>EXAMPLE</strong>: if the CacheName is "MyCache" the CacheKeyPrefix will be "MyCache:", so that a later call to cache.GetOrDefault("Product/123") will actually work on the cache key "MyCache:Product/123".
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithCacheKeyPrefix(this IFusionCacheBuilder builder)
	{
		return builder.WithCacheKeyPrefix(builder.CacheName + ":");
	}

	/// <summary>
	/// Specify NOT to use a cache key prefix.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutCacheKeyPrefix(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseCacheKeyPrefix = false;
		builder.CacheKeyPrefix = null;

		return builder;
	}

	#endregion

	#region DEFAULT ENTRY OPTIONS

	/// <summary>
	/// Specify a <see cref="FusionCacheEntryOptions"/> instance to be used as the <see cref="FusionCacheOptions.DefaultEntryOptions"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="options">The <see cref="FusionCacheEntryOptions"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDefaultEntryOptions(this IFusionCacheBuilder builder, FusionCacheEntryOptions? options)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (action is null)
			throw new ArgumentNullException(nameof(action));

		builder.SetupDefaultEntryOptionsAction += action;

		return builder;
	}

	#endregion

	#region LOGGER

	/// <summary>
	/// The builder will look for an <see cref="ILogger{FusionCache}"/> service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredLogger(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredLogger = true;
		builder.Logger = null;
		builder.LoggerFactory = null;
		builder.ThrowIfMissingLogger = true;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="ILogger{FusionCache}"/> keyed service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedLogger(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.WithRegisteredLogger();
		builder.LoggerServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="ILogger{FusionCache}"/> keyed service registered in the DI container (with the CacheName as the serviceKey) and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedLoggerByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.WithRegisteredKeyedLogger(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="ILogger{FusionCache}"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredLogger(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredLogger();
		builder.ThrowIfMissingLogger = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="ILogger{FusionCache}"/> keyed service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedLogger(this IFusionCacheBuilder builder, object? serviceKey)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedLogger(serviceKey);
		builder.ThrowIfMissingLogger = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="ILogger{FusionCache}"/> keyed service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedLogger(this IFusionCacheBuilder builder)
	{
		return builder.TryWithRegisteredKeyedLogger(builder.CacheName);
	}

	/// <summary>
	/// Specify a custom <see cref="ILogger{FusionCache}"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="logger">The <see cref="ILogger{FusionCache}"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithLogger(this IFusionCacheBuilder builder, ILogger<FusionCache> logger)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (logger is null)
			throw new ArgumentNullException(nameof(logger));

		builder.UseRegisteredLogger = false;
		builder.Logger = logger;
		builder.LoggerFactory = null;
		builder.ThrowIfMissingLogger = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="ILogger{FusionCache}"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the logger, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithLogger(this IFusionCacheBuilder builder, Func<IServiceProvider, ILogger<FusionCache>> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredLogger = false;
		builder.Logger = null;
		builder.LoggerFactory = factory;
		builder.ThrowIfMissingLogger = true;

		return builder;
	}

	/// <summary>
	/// Indicates that the builder should not use a logger at all.
	/// </summary>
	/// <param name="builder"></param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutLogger(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredLogger = false;
		builder.Logger = null;
		builder.LoggerFactory = null;
		builder.ThrowIfMissingLogger = false;

		return builder;
	}

	#endregion

	#region MEMORY CACHE

	/// <summary>
	/// The builder will look for an <see cref="IMemoryCache"/> service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use WithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a memory cache is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredMemoryCache().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredMemoryCache(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredMemoryCache = true;
		builder.MemoryCache = null;
		builder.MemoryCacheFactory = null;
		builder.ThrowIfMissingMemoryCache = true;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IMemoryCache"/> keyed service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use WithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a memory cache is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredMemoryCache().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedMemoryCache(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.WithRegisteredMemoryCache();
		builder.MemoryCacheServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IMemoryCache"/> keyed service registered in the DI container (with the CacheName as the serviceKey) and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use WithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a memory cache is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredMemoryCache().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedMemoryCacheByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.WithRegisteredKeyedMemoryCache(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IMemoryCache"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use TryWithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredMemoryCache(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredMemoryCache();
		builder.ThrowIfMissingMemoryCache = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IMemoryCache"/> keyed service registered in the DI container.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use TryWithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedMemoryCache(this IFusionCacheBuilder builder, object? serviceKey)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedMemoryCache(serviceKey);
		builder.ThrowIfMissingMemoryCache = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IMemoryCache"/> keyed service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the memory cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredMemoryCache() on all of them, they will use THE SAME memory cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// To avoid this, either don't use TryWithRegisteredMemoryCache() and let FusionCache create one for you (which will be different per cache instance) or use WithMemoryCache() and provide one directly.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedMemoryCache(this IFusionCacheBuilder builder)
	{
		return builder.TryWithRegisteredKeyedMemoryCache(builder.CacheName);
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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (memoryCache is null)
			throw new ArgumentNullException(nameof(memoryCache));

		builder.UseRegisteredMemoryCache = false;
		builder.MemoryCache = memoryCache;
		builder.MemoryCacheFactory = null;
		builder.ThrowIfMissingMemoryCache = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IMemoryCache"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the memory cache, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryCache(this IFusionCacheBuilder builder, Func<IServiceProvider, IMemoryCache> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredMemoryCache = false;
		builder.MemoryCache = null;
		builder.MemoryCacheFactory = factory;
		builder.ThrowIfMissingMemoryCache = true;

		return builder;
	}

	#endregion

	#region MEMORY LOCKER

	/// <summary>
	/// The standard implementation of an <see cref="IFusionCacheMemoryLocker"/> will be used (this is the default behaviour).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithStandardMemoryLocker(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredMemoryLocker = false;
		builder.MemoryLocker = null;
		builder.MemoryLockerFactory = null;
		builder.ThrowIfMissingMemoryLocker = false;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheMemoryLocker"/> service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredMemoryLocker(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredMemoryLocker = true;
		builder.MemoryLocker = null;
		builder.MemoryLockerFactory = null;
		builder.ThrowIfMissingMemoryLocker = true;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheMemoryLocker"/> keyed service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedMemoryLocker(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.WithRegisteredMemoryLocker();
		builder.MemoryLockerServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheMemoryLocker"/> keyed service registered in the DI container (with the CacheName as the serviceKey) and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedMemoryLockerByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.WithRegisteredKeyedMemoryLocker(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheMemoryLocker"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredMemoryLocker(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredMemoryLocker();
		builder.ThrowIfMissingMemoryLocker = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheMemoryLocker"/> keyed service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedMemoryLocker(this IFusionCacheBuilder builder, object? serviceKey)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedMemoryLocker(serviceKey);
		builder.ThrowIfMissingMemoryLocker = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheMemoryLocker"/> keyed service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedMemoryLocker(this IFusionCacheBuilder builder)
	{
		return builder.TryWithRegisteredKeyedMemoryLocker(builder.CacheName);
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheMemoryLocker"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="memoryLocker">The <see cref="IFusionCacheMemoryLocker"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryLocker(this IFusionCacheBuilder builder, IFusionCacheMemoryLocker memoryLocker)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (memoryLocker is null)
			throw new ArgumentNullException(nameof(memoryLocker));

		builder.UseRegisteredMemoryLocker = false;
		builder.MemoryLocker = memoryLocker;
		builder.MemoryLockerFactory = null;
		builder.ThrowIfMissingMemoryLocker = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheMemoryLocker"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the memory locker, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithMemoryLocker(this IFusionCacheBuilder builder, Func<IServiceProvider, IFusionCacheMemoryLocker> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredMemoryLocker = false;
		builder.MemoryLocker = null;
		builder.MemoryLockerFactory = factory;
		builder.ThrowIfMissingMemoryLocker = true;

		return builder;
	}

	#endregion

	#region SERIALIZER

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredSerializer(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredDistributedCache = true;
		builder.Serializer = null;
		builder.SerializerFactory = null;
		builder.ThrowIfMissingSerializer = true;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> keyed service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedSerializer(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.WithRegisteredSerializer();
		builder.SerializerServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> keyed service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedSerializerByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.WithRegisteredKeyedSerializer(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredSerializer(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredSerializer();
		builder.ThrowIfMissingSerializer = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedSerializer(this IFusionCacheBuilder builder, object? serviceKey)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedSerializer(serviceKey);
		builder.ThrowIfMissingSerializer = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IFusionCacheSerializer"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedSerializer(this IFusionCacheBuilder builder)
	{
		return builder.TryWithRegisteredKeyedSerializer(builder.CacheName);
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheSerializer"/> instance to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithSerializer(this IFusionCacheBuilder builder, IFusionCacheSerializer serializer)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (serializer is null)
			throw new ArgumentNullException(nameof(serializer));

		builder.UseRegisteredSerializer = false;
		builder.Serializer = serializer;
		builder.SerializerFactory = null;
		builder.ThrowIfMissingSerializer = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheBackplane"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the serializer, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithSerializer(this IFusionCacheBuilder builder, Func<IServiceProvider, IFusionCacheSerializer> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredSerializer = false;
		builder.Serializer = null;
		builder.SerializerFactory = factory;
		builder.ThrowIfMissingSerializer = true;

		return builder;
	}

	#endregion

	#region DISTRIBUTED CACHE

	/// <summary>
	/// The builder will look for an <see cref="IDistributedCache"/> service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container and use them, and throws if it cannot find them.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is not found in the DI container, or if one is found but no <see cref="IFusionCacheSerializer"/> is found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredDistributedCache().
	/// <br/><br/>
	/// <strong>NOTE:</strong> normally if an <see cref="IDistributedCache"/> is found in the DI container, it will be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredDistributedCache(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredDistributedCache = true;
		builder.DistributedCache = null;
		builder.DistributedCacheFactory = null;
		builder.IgnoreRegisteredMemoryDistributedCache = ignoreMemoryDistributedCache;
		builder.ThrowIfMissingDistributedCache = true;
		builder.ThrowIfMissingSerializer = true;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IDistributedCache"/> keyed service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container and use them, and throws if it cannot find them.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is not found in the DI container, or if one is found but no <see cref="IFusionCacheSerializer"/> is found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredDistributedCache().
	/// <br/><br/>
	/// <strong>NOTE:</strong> normally if an <see cref="IDistributedCache"/> is found in the DI container, it will be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedDistributedCache(this IFusionCacheBuilder builder, object? serviceKey, bool ignoreMemoryDistributedCache = true)
	{
		builder.WithRegisteredDistributedCache(ignoreMemoryDistributedCache);
		builder.DistributedCacheServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IDistributedCache"/> keyed service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container (with the CacheName as the serviceKey) and use them, and throws if it cannot find them.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches and also use WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is not found in the DI container, or if one is found but no <see cref="IFusionCacheSerializer"/> is found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredDistributedCache().
	/// <br/><br/>
	/// <strong>NOTE:</strong> normally if an <see cref="IDistributedCache"/> is found in the DI container, it will be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedDistributedCacheByCacheName(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true)
	{
		return builder.WithRegisteredKeyedDistributedCache(builder.CacheName, ignoreMemoryDistributedCache);
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IDistributedCache"/> service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches by using WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, it can be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, a <see cref="IFusionCacheSerializer"/> would also be needed: when that is not the case, by default an <see cref="InvalidOperationException"/> will be thrown so to avoid surprises at runtime, like thinking that a distributed cache will be used when instead it will not. If you want to avoid this and just have a best-effort approach by ignoring a distributed cache when a serializer is missing, set the <paramref name="throwIfMissingSerializer"/> param to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <param name="throwIfMissingSerializer">Indicates if an exception should be thrown in case a valid <see cref="IFusionCacheSerializer"/> has not been provided: this is useful to avoid thinking of having a usable distributed cache when, in reality, that is not the case since a serializer is needed for it to work and none has been found.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredDistributedCache(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true, bool throwIfMissingSerializer = false)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredDistributedCache(ignoreMemoryDistributedCache);
		builder.ThrowIfMissingDistributedCache = false;
		builder.ThrowIfMissingSerializer = throwIfMissingSerializer;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IDistributedCache"/> keyed service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches by using WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, it can be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, a <see cref="IFusionCacheSerializer"/> would also be needed: when that is not the case, by default an <see cref="InvalidOperationException"/> will be thrown so to avoid surprises at runtime, like thinking that a distributed cache will be used when instead it will not. If you want to avoid this and just have a best-effort approach by ignoring a distributed cache when a serializer is missing, set the <paramref name="throwIfMissingSerializer"/> param to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <param name="throwIfMissingSerializer">Indicates if an exception should be thrown in case a valid <see cref="IFusionCacheSerializer"/> has not been provided: this is useful to avoid thinking of having a usable distributed cache when, in reality, that is not the case since a serializer is needed for it to work and none has been found.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedDistributedCache(this IFusionCacheBuilder builder, object? serviceKey, bool ignoreMemoryDistributedCache = true, bool throwIfMissingSerializer = true)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedDistributedCache(serviceKey, ignoreMemoryDistributedCache);
		builder.ThrowIfMissingDistributedCache = false;
		builder.ThrowIfMissingSerializer = throwIfMissingSerializer;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try to find and use an <see cref="IDistributedCache"/> keyed service (and a corresponding <see cref="IFusionCacheSerializer"/>) registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> normally the distributed cache is registered in the DI container as a SINGLETON. This means that, if you use multiple named caches by using WithRegisteredDistributedCache() on all of them, they will use THE SAME distributed cache and without extra care in creating cache keys YOU MAY HAVE COLLISIONS.
	/// <br/>
	/// One way to avoid collisions is to specify a CacheKeyPrefix by using one of the WithCacheKeyPrefix() methods.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, it can be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, a <see cref="IFusionCacheSerializer"/> would also be needed: when that is not the case, by default an <see cref="InvalidOperationException"/> will be thrown so to avoid surprises at runtime, like thinking that a distributed cache will be used when instead it will not. If you want to avoid this and just have a best-effort approach by ignoring a distributed cache when a serializer is missing, set the <paramref name="throwIfMissingSerializer"/> param to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.</param>
	/// <param name="throwIfMissingSerializer">Indicates if an exception should be thrown in case a valid <see cref="IFusionCacheSerializer"/> has not been provided: this is useful to avoid thinking of having a usable distributed cache when, in reality, that is not the case since a serializer is needed for it to work and none has been found.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedDistributedCache(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true, bool throwIfMissingSerializer = true)
	{
		return builder.TryWithRegisteredKeyedDistributedCache(builder.CacheName, ignoreMemoryDistributedCache, throwIfMissingSerializer);
	}

	/// <summary>
	/// Specify a custom <see cref="IDistributedCache"/> and a custom <see cref="IFusionCacheSerializer"/> instances to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="distributedCache">The <see cref="IDistributedCache"/> instance to use.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use, or <see langword="null"/> to keep the one specified in another call.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDistributedCache(this IFusionCacheBuilder builder, IDistributedCache distributedCache, IFusionCacheSerializer? serializer = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (distributedCache is null)
			throw new ArgumentNullException(nameof(distributedCache));

		builder.UseRegisteredDistributedCache = false;
		builder.DistributedCache = distributedCache;
		builder.DistributedCacheFactory = null;
		builder.ThrowIfMissingDistributedCache = true;
		builder.ThrowIfMissingSerializer = true;
		if (serializer is not null)
		{
			builder.WithSerializer(serializer);
		}

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheBackplane"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the backplane, with access to the <see cref="IServiceProvider"/>.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use, or <see langword="null"/> to keep the one specified in another call.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithDistributedCache(this IFusionCacheBuilder builder, Func<IServiceProvider, IDistributedCache> factory, IFusionCacheSerializer? serializer = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredDistributedCache = false;
		builder.DistributedCache = null;
		builder.DistributedCacheFactory = factory;
		if (serializer is not null)
		{
			builder.Serializer = serializer;
		}
		builder.ThrowIfMissingDistributedCache = true;
		builder.ThrowIfMissingSerializer = true;

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.DistributedCache = null;
		builder.UseRegisteredDistributedCache = false;
		builder.ThrowIfMissingDistributedCache = false;
		builder.ThrowIfMissingSerializer = false;

		return builder;
	}

	#endregion

	#region BACKPLANE

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheBackplane"/> service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a backplane is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredBackplane().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredBackplane(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseRegisteredBackplane = true;
		builder.Backplane = null;
		builder.BackplaneFactory = null;
		builder.ThrowIfMissingBackplane = true;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheBackplane"/> keyed service registered in the DI container and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a backplane is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredBackplane().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedBackplane(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.WithRegisteredBackplane();
		builder.BackplaneServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// The builder will look for an <see cref="IFusionCacheBackplane"/> keyed service registered in the DI container (with the CacheName as the serviceKey) and use it, and throws if it cannot find one.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if a backplane is not found, an <see cref="InvalidOperationException"/> will be thrown. To avoid this and use a best-effort behavior, use TryWithRegisteredBackplane().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithRegisteredKeyedBackplaneByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.WithRegisteredKeyedBackplane(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredBackplane(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredBackplane();
		builder.ThrowIfMissingBackplane = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> keyed service registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedBackplane(this IFusionCacheBuilder builder, object? serviceKey)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.WithRegisteredKeyedBackplane(serviceKey);
		builder.ThrowIfMissingBackplane = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> keyed service registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithRegisteredKeyedBackplane(this IFusionCacheBuilder builder)
	{
		return builder.TryWithRegisteredKeyedBackplane(builder.CacheName);
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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		builder.UseRegisteredBackplane = false;
		builder.Backplane = backplane;
		builder.BackplaneFactory = null;
		builder.ThrowIfMissingBackplane = true;

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheBackplane"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the backplane, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithBackplane(this IFusionCacheBuilder builder, Func<IServiceProvider, IFusionCacheBackplane> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.UseRegisteredBackplane = false;
		builder.Backplane = null;
		builder.BackplaneFactory = factory;
		builder.ThrowIfMissingBackplane = true;

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.Backplane = null;
		builder.BackplaneFactory = null;
		builder.UseRegisteredBackplane = false;
		builder.ThrowIfMissingBackplane = false;

		return builder;
	}

	#endregion

	#region PLUGINS

	/// <summary>
	/// Indicates if the builder should try find and use all available <see cref="IFusionCachePlugin"/> services registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAllRegisteredPlugins(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseAllRegisteredPlugins = true;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use all available <see cref="IFusionCachePlugin"/> keyed services registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAllRegisteredKeyedPlugins(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.PluginsServiceKey = serviceKey;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should try find and use all available <see cref="IFusionCachePlugin"/> keyed services registered in the DI container (with the CacheName as the serviceKey).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAllRegisteredKeyedPlugins(this IFusionCacheBuilder builder)
	{
		return builder.WithAllRegisteredKeyedPlugins(builder.CacheName);
	}

	/// <summary>
	/// Indicates if the builder should NOT try find and use all available <see cref="IFusionCachePlugin"/> services registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutAllRegisteredPlugins(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseAllRegisteredPlugins = false;

		return builder;
	}

	/// <summary>
	/// Indicates if the builder should NOT try find and use all available <see cref="IFusionCachePlugin"/> keyed services registered in the DI container.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutAllRegisteredKeyedPlugins(this IFusionCacheBuilder builder)
	{
		builder.PluginsServiceKey = null;

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (plugin is null)
			throw new ArgumentNullException(nameof(plugin));

		builder.Plugins.Add(plugin);

		return builder;
	}

	/// <summary>
	/// Specify a custom <see cref="IFusionCacheBackplane"/> factory to be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="factory">The factory used to create the backplane, with access to the <see cref="IServiceProvider"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithPlugin(this IFusionCacheBuilder builder, Func<IServiceProvider, IFusionCachePlugin> factory)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		builder.PluginsFactories.Add(factory);

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (plugins is not null)
		{
			if (plugins.Length > 0)
				builder.Plugins.AddRange(plugins);
		}

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.UseAllRegisteredPlugins = false;
		builder.Plugins.Clear();
		builder.PluginsFactories.Clear();

		return builder;
	}

	#endregion

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
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (action is null)
			throw new ArgumentNullException(nameof(action));

		builder.PostSetupAction += action;

		return builder;
	}

	/// <summary>
	/// Indicates that the builder should not use any post-setup actions.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithoutPostSetup(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder.PostSetupAction = null;

		return builder;
	}

	/// <summary>
	/// Tells the builder to try to find and use all the compatible services registered in the DI container, like a distributed cache, a backplane, plugins, etc.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if an <see cref="IDistributedCache"/> is found, it can be used. In some scenarios though, like when using ASP.NET, one is automatically registered of type <see cref="MemoryDistributedCache"/>: that is not a real distributed cache, but just a memory cache masquerading as a distributed one. Since using that would do nothing and is a waste of resources, by default that is ignored. If you want to use it instead, just set the <paramref name="ignoreMemoryDistributedCache"/> to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="ignoreMemoryDistributedCache">Indicates if the distributed cache found in the DI container should be ignored if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder TryWithAutoSetup(this IFusionCacheBuilder builder, bool ignoreMemoryDistributedCache = true)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		builder = builder
			.TryWithRegisteredLogger()
			.TryWithRegisteredMemoryLocker()
			.TryWithRegisteredMemoryCache()
			.TryWithRegisteredDistributedCache(ignoreMemoryDistributedCache, false)
			.TryWithRegisteredBackplane()
			.WithAllRegisteredPlugins()
		;

		builder.ThrowIfMissingLogger = false;

		return builder;
	}

	/// <summary>
	/// Register this FusionCache instance also as a keyed service, so that it can be retrieved with the [FromKeyedServices] attribute usage.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder AsKeyedService(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.Services.AddKeyedSingleton<IFusionCache>(serviceKey, (serviceProvider, key) =>
		{
			if (key is string stringKey && stringKey == FusionCacheOptions.DefaultCacheName)
			{
				return serviceProvider.GetRequiredService<IFusionCache>();
			}

			return serviceProvider.GetRequiredService<IFusionCacheProvider>().GetCache(builder.CacheName);
		});

		return builder;
	}

	/// <summary>
	/// Register this FusionCache instance also as a keyed service, so that it can be retrieved with the [FromKeyedServices] attribute usage.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder AsKeyedServiceByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.AsKeyedService(builder.CacheName);
	}
}
