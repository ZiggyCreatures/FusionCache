using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents an instance of a builder object to create FusionCache instances.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
/// </summary>
public interface IFusionCacheBuilder
{
	/// <summary>
	/// The name of the FusionCache instance.
	/// </summary>
	string CacheName { get; }

	/// <summary>
	/// The <see cref="IServiceCollection"/> instance to use for the builder when working with the DI container.
	/// </summary>
	public IServiceCollection Services { get; }

	#region LOGGER

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="ILogger{FusionCache}"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredLogger { get; set; }

	/// <summary>
	/// The keyed service key to use for the logger.
	/// </summary>
	public object? LoggerServiceKey { get; set; }

	/// <summary>
	/// A specific <see cref="ILogger{FusionCache}"/> instance to be used.
	/// </summary>
	ILogger<FusionCache>? Logger { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="ILogger{FusionCache}"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, ILogger<FusionCache>>? LoggerFactory { get; set; }

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/> if a logger (an instance of <see cref="ILogger{FusionCache}"/>) is not specified or is not found in the DI container.
	/// </summary>
	bool ThrowIfMissingLogger { get; set; }

	#endregion

	#region OPTIONS

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IOptions{FusionCacheOptions}"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredOptions { get; set; }

	/// <summary>
	/// A custom <see cref="FusionCacheOptions"/> object to be used.
	/// </summary>
	FusionCacheOptions? Options { get; set; }

	/// <summary>
	/// Indicates if the builder should use the specified <see cref="CacheKeyPrefix"/>, overwriting the one in the options as configured.
	/// </summary>
	bool UseCacheKeyPrefix { get; set; }

	/// <summary>
	/// A prefix that will be added to each cache key for each call: it can be useful when working with multiple named caches.
	/// <br/><br/>
	/// <strong>EXAMPLE</strong>: if the CacheKeyPrefix specified is "MyCache:", a later call to cache.GetOrDefault("Product/123") will actually work on the cache key "MyCache:Product/123".
	/// </summary>
	string? CacheKeyPrefix { get; set; }

	/// <summary>
	/// A custom setup logic for the <see cref="FusionCacheOptions"/> object, to allow for fine-grained customization.
	/// </summary>
	Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

	#endregion

	#region DEFAULT ENTRY OPTIONS

	/// <summary>
	/// A custom <see cref="FusionCacheEntryOptions"/> object to be used as the <see cref="FusionCacheOptions.DefaultEntryOptions"/>.
	/// </summary>
	FusionCacheEntryOptions? DefaultEntryOptions { get; set; }

	/// <summary>
	/// A custom setup logic for the <see cref="FusionCacheOptions"/> object, to allow for fine-grained customization.
	/// </summary>
	Action<FusionCacheEntryOptions>? SetupDefaultEntryOptionsAction { get; set; }

	#endregion

	#region MEMORY CACHE

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IMemoryCache"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredMemoryCache { get; set; }

	/// <summary>
	/// A specific <see cref="IMemoryCache"/> instance to be used.
	/// </summary>
	IMemoryCache? MemoryCache { get; set; }

	/// <summary>
	/// The keyed service key to use for the memory cache.
	/// </summary>
	public object? MemoryCacheServiceKey { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="IMemoryCache"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, IMemoryCache>? MemoryCacheFactory { get; set; }

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/> if a memory cache (an instance of <see cref="IMemoryCache"/>) is not specified or is not found in the DI container.
	/// </summary>
	bool ThrowIfMissingMemoryCache { get; set; }

	#endregion

	#region MEMORY LOCKER

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheMemoryLocker"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredMemoryLocker { get; set; }

	/// <summary>
	/// The keyed service key to use for the memory locker.
	/// </summary>
	public object? MemoryLockerServiceKey { get; set; }

	/// <summary>
	/// A specific <see cref="IFusionCacheMemoryLocker"/> instance to be used.
	/// </summary>
	IFusionCacheMemoryLocker? MemoryLocker { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="IFusionCacheMemoryLocker"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, IFusionCacheMemoryLocker>? MemoryLockerFactory { get; set; }

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/> if a memory locker (an instance of <see cref="IFusionCacheMemoryLocker"/>) is not specified or is not found in the DI container.
	/// </summary>
	bool ThrowIfMissingMemoryLocker { get; set; }

	#endregion

	#region SERIALIZER

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheSerializer"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredSerializer { get; set; }

	/// <summary>
	/// The keyed service key to use for the memory serializer.
	/// </summary>
	public object? SerializerServiceKey { get; set; }

	/// <summary>
	/// A specific <see cref="IFusionCacheSerializer"/> instance to be used.
	/// </summary>
	IFusionCacheSerializer? Serializer { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="IFusionCacheSerializer"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, IFusionCacheSerializer>? SerializerFactory { get; set; }

	/// <summary>
	/// When a distributed cache has been specified or found in the DI container, throws an <see cref="InvalidOperationException"/> if a serializer (an instance of <see cref="IFusionCacheSerializer"/>) is not specified or is not found in the DI container, too.
	/// </summary>
	bool ThrowIfMissingSerializer { get; set; }

	#endregion

	#region DISTRIBUTED CACHE

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IDistributedCache"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredDistributedCache { get; set; }

	/// <summary>
	/// The keyed service key to use for the distributed cache.
	/// </summary>
	public object? DistributedCacheServiceKey { get; set; }

	/// <summary>
	/// When trying to find an <see cref="IDistributedCache"/> service registered in the DI container, ignore it if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.
	/// </summary>
	bool IgnoreRegisteredMemoryDistributedCache { get; set; }

	/// <summary>
	/// A specific <see cref="IDistributedCache"/> instance to be used.
	/// </summary>
	IDistributedCache? DistributedCache { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="IDistributedCache"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, IDistributedCache>? DistributedCacheFactory { get; set; }

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/> if a distributed cache (an instance of <see cref="IDistributedCache"/>) is not specified or is not found in the DI container.
	/// </summary>
	bool ThrowIfMissingDistributedCache { get; set; }

	#endregion

	#region BACKPLANE

	/// <summary>
	/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> service registered in the DI container.
	/// </summary>
	bool UseRegisteredBackplane { get; set; }

	/// <summary>
	/// The keyed service key to use for the backplane.
	/// </summary>
	public object? BackplaneServiceKey { get; set; }

	/// <summary>
	/// A specific <see cref="IFusionCacheBackplane"/> instance to be used.
	/// </summary>
	IFusionCacheBackplane? Backplane { get; set; }

	/// <summary>
	/// A factory that creates the <see cref="IFusionCacheBackplane"/> instance to be used.
	/// </summary>
	Func<IServiceProvider, IFusionCacheBackplane>? BackplaneFactory { get; set; }

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/> if a backplane (an instance of <see cref="IFusionCacheBackplane"/>) is not specified or is not found in the DI container.
	/// </summary>
	bool ThrowIfMissingBackplane { get; set; }

	#endregion

	#region PLUGINS

	/// <summary>
	/// Indicates if the builder should try find and use any available <see cref="IFusionCachePlugin"/> services registered in the DI container.
	/// </summary>
	bool UseAllRegisteredPlugins { get; set; }

	/// <summary>
	/// The keyed service key to use for the plugins.
	/// </summary>
	public object? PluginsServiceKey { get; set; }

	/// <summary>
	/// A specific set of <see cref="IFusionCachePlugin"/> instances to be used.
	/// </summary>
	List<IFusionCachePlugin> Plugins { get; }

	/// <summary>
	/// A specific set of <see cref="IFusionCachePlugin"/> factories to be used.
	/// </summary>
	List<Func<IServiceProvider, IFusionCachePlugin>> PluginsFactories { get; }

	#endregion

	/// <summary>
	/// A custom post-setup action, that will be invoked just after the creation of the FusionCache instance, and before returning it to the caller.
	/// <br/><br/>
	/// <strong>NOTE:</strong> it is possible to add actions multiple times, to add multiple post-setup calls one after the other to combine them for a powerful result.
	/// </summary>
	Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }

	/// <summary>
	/// Creates a new FusionCache instance, and set it up based on the configured builder options.
	/// </summary>
	/// <param name="serviceProvider">The needed <see cref="IServiceProvider"/> instance.</param>
	/// <returns>The <see cref="IFusionCache"/> instance created.</returns>
	IFusionCache Build(IServiceProvider serviceProvider);
}
