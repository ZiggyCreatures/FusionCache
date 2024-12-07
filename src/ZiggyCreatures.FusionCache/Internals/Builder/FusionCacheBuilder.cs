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

namespace ZiggyCreatures.Caching.Fusion.Internals.Builder;

internal sealed class FusionCacheBuilder
	: IFusionCacheBuilder
{
	public FusionCacheBuilder(string cacheName, IServiceCollection services)
	{
		CacheName = cacheName;
		Services = services;

		UseRegisteredLogger = true;

		UseRegisteredOptions = true;

		UseRegisteredMemoryLocker = true;

		UseRegisteredSerializer = true;

		IgnoreRegisteredMemoryDistributedCache = true;

		Plugins = [];
		PluginsFactories = [];
	}

	private IFusionCache? _cache;

	public string CacheName { get; }

	public IServiceCollection Services { get; }

	public bool UseRegisteredLogger { get; set; }
	public object? LoggerServiceKey { get; set; }
	public ILogger<FusionCache>? Logger { get; set; }
	public Func<IServiceProvider, ILogger<FusionCache>>? LoggerFactory { get; set; }
	public bool ThrowIfMissingLogger { get; set; }

	public bool UseRegisteredOptions { get; set; }
	public FusionCacheOptions? Options { get; set; }
	public bool UseCacheKeyPrefix { get; set; }
	public string? CacheKeyPrefix { get; set; }
	public Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

	public FusionCacheEntryOptions? DefaultEntryOptions { get; set; }
	public Action<FusionCacheEntryOptions>? SetupDefaultEntryOptionsAction { get; set; }

	public bool UseRegisteredMemoryCache { get; set; }
	public object? MemoryCacheServiceKey { get; set; }
	public IMemoryCache? MemoryCache { get; set; }
	public Func<IServiceProvider, IMemoryCache>? MemoryCacheFactory { get; set; }
	public bool ThrowIfMissingMemoryCache { get; set; }

	public bool UseRegisteredMemoryLocker { get; set; }
	public object? MemoryLockerServiceKey { get; set; }
	public IFusionCacheMemoryLocker? MemoryLocker { get; set; }
	public Func<IServiceProvider, IFusionCacheMemoryLocker>? MemoryLockerFactory { get; set; }
	public bool ThrowIfMissingMemoryLocker { get; set; }

	public bool UseRegisteredSerializer { get; set; }
	public object? SerializerServiceKey { get; set; }
	public IFusionCacheSerializer? Serializer { get; set; }
	public Func<IServiceProvider, IFusionCacheSerializer>? SerializerFactory { get; set; }
	public bool ThrowIfMissingSerializer { get; set; }

	public bool UseRegisteredDistributedCache { get; set; }
	public object? DistributedCacheServiceKey { get; set; }
	public bool IgnoreRegisteredMemoryDistributedCache { get; set; }
	public IDistributedCache? DistributedCache { get; set; }
	public Func<IServiceProvider, IDistributedCache>? DistributedCacheFactory { get; set; }
	public bool ThrowIfMissingDistributedCache { get; set; }

	public bool UseRegisteredBackplane { get; set; }
	public object? BackplaneServiceKey { get; set; }
	public IFusionCacheBackplane? Backplane { get; set; }
	public Func<IServiceProvider, IFusionCacheBackplane>? BackplaneFactory { get; set; }
	public bool ThrowIfMissingBackplane { get; set; }

	public bool UseAllRegisteredPlugins { get; set; }
	public object? PluginsServiceKey { get; set; }
	public List<IFusionCachePlugin> Plugins { get; }
	public List<Func<IServiceProvider, IFusionCachePlugin>> PluginsFactories { get; }

	public Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }

	public IFusionCache Build(IServiceProvider serviceProvider)
	{
		if (serviceProvider is null)
			throw new ArgumentNullException(nameof(serviceProvider));

		if (_cache is not null)
			return _cache;

		// OPTIONS
		FusionCacheOptions? options = null;

		if (UseRegisteredOptions)
		{
			if (CacheName == FusionCacheOptions.DefaultCacheName)
			{
				options = serviceProvider.GetService<IOptions<FusionCacheOptions>>()?.Value;
			}
			else
			{
				options = serviceProvider.GetRequiredService<IOptionsMonitor<FusionCacheOptions>>().Get(CacheName);
			}

			if (options is not null)
			{
				options.CacheName = CacheName;
			}
		}

		if (options is null)
		{
			options = Options;

			if (options is null)
			{
				options = new FusionCacheOptions()
				{
					CacheName = CacheName
				};
			}
		}

		SetupOptionsAction?.Invoke(options);

		// CACHE KEY PREFIX
		if (UseCacheKeyPrefix)
		{
			options.CacheKeyPrefix = CacheKeyPrefix;
		}

		// DEFAULT ENTRY OPTIONS
		if (DefaultEntryOptions is not null)
		{
			options.DefaultEntryOptions = DefaultEntryOptions;
		}

		if (SetupDefaultEntryOptionsAction is not null)
		{
			SetupDefaultEntryOptionsAction?.Invoke(options.DefaultEntryOptions);
		}

		// CHECK INCOHERENT CACHE NAMES
		if (options.CacheName != CacheName)
		{
			throw new InvalidOperationException($"When using dependency injection and/or the builder, the cache name must be specified via the AddFusionCache(\"MyCache\") method.");
		}

		// ENSURE CACHE NAME
		options.CacheName = CacheName;

		// LOGGER
		ILogger<FusionCache>? logger;

		if (UseRegisteredLogger)
		{
			if (LoggerServiceKey is null)
			{
				logger = serviceProvider.GetService<ILogger<FusionCache>>();
			}
			else
			{
				logger = serviceProvider.GetKeyedService<ILogger<FusionCache>>(LoggerServiceKey);
			}
		}
		else if (LoggerFactory is not null)
		{
			logger = LoggerFactory.Invoke(serviceProvider);
		}
		else
		{
			logger = Logger;
		}

		if (logger is null && ThrowIfMissingLogger)
		{
			throw new InvalidOperationException("A logger has not been specified, or found in the DI container.");
		}

		// MEMORY CACHE
		IMemoryCache? memoryCache;

		if (UseRegisteredMemoryCache)
		{
			if (MemoryCacheServiceKey is null)
			{
				memoryCache = serviceProvider.GetService<IMemoryCache>();
			}
			else
			{
				memoryCache = serviceProvider.GetKeyedService<IMemoryCache>(MemoryCacheServiceKey);
			}
		}
		else if (MemoryCacheFactory is not null)
		{
			memoryCache = MemoryCacheFactory.Invoke(serviceProvider);
		}
		else
		{
			memoryCache = MemoryCache;
		}

		if (memoryCache is null && ThrowIfMissingMemoryCache)
		{
			throw new InvalidOperationException("A memory cache has not been specified, or found in the DI container.");
		}

		// MEMORY LOCKER
		IFusionCacheMemoryLocker? memoryLocker = null;

		if (UseRegisteredMemoryLocker)
		{
			if (MemoryLockerServiceKey is null)
			{
				memoryLocker = serviceProvider.GetService<IFusionCacheMemoryLocker>();
			}
			else
			{
				memoryLocker = serviceProvider.GetKeyedService<IFusionCacheMemoryLocker>(MemoryLockerServiceKey);
			}
		}
		else if (MemoryLockerFactory is not null)
		{
			memoryLocker = MemoryLockerFactory.Invoke(serviceProvider);
		}
		else
		{
			memoryLocker = MemoryLocker;
		}

		if (memoryLocker is null && ThrowIfMissingMemoryLocker)
		{
			throw new InvalidOperationException("A memory locker has not been specified, or found in the DI container.");
		}

		// CREATE THE CACHE
		var cache = new FusionCache(options, memoryCache, logger, memoryLocker);

		// SERIALIZER
		IFusionCacheSerializer? serializer = null;

		if (UseRegisteredSerializer)
		{
			if (SerializerServiceKey is null)
			{
				serializer = serviceProvider.GetService<IFusionCacheSerializer>();
			}
			else
			{
				serializer = serviceProvider.GetKeyedService<IFusionCacheSerializer>(SerializerServiceKey);
			}
		}
		else if (SerializerFactory is not null)
		{
			serializer = SerializerFactory.Invoke(serviceProvider);
		}
		else
		{
			serializer = Serializer;
		}

		if (serializer is null && ThrowIfMissingSerializer)
		{
			throw new InvalidOperationException("A serializer has not been specified, or found in the DI container.");
		}

		if (serializer is not null)
		{
			cache.SetupSerializer(serializer);
		}

		// DISTRIBUTED CACHE
		IDistributedCache? distributedCache;

		if (UseRegisteredDistributedCache)
		{
			if (DistributedCacheServiceKey is null)
			{
				distributedCache = serviceProvider.GetService<IDistributedCache>();
			}
			else
			{
				distributedCache = serviceProvider.GetKeyedService<IDistributedCache>(DistributedCacheServiceKey);
			}

			if (IgnoreRegisteredMemoryDistributedCache && distributedCache is MemoryDistributedCache)
			{
				distributedCache = null;
			}
		}
		else if (DistributedCacheFactory is not null)
		{
			distributedCache = DistributedCacheFactory.Invoke(serviceProvider);
		}
		else
		{
			distributedCache = DistributedCache;
		}

		if (distributedCache is null && ThrowIfMissingDistributedCache)
		{
			throw new InvalidOperationException("A distributed cache has not been specified, or found in the DI container.");
		}

		if (distributedCache is not null)
		{
			cache.SetupDistributedCache(distributedCache);
		}

		// BACKPLANE
		IFusionCacheBackplane? backplane;
		if (UseRegisteredBackplane)
		{
			if (BackplaneServiceKey is null)
			{
				backplane = serviceProvider.GetService<IFusionCacheBackplane>();
			}
			else
			{
				backplane = serviceProvider.GetKeyedService<IFusionCacheBackplane>(BackplaneServiceKey);
			}
		}
		else if (BackplaneFactory is not null)
		{
			backplane = BackplaneFactory?.Invoke(serviceProvider);
		}
		else
		{
			backplane = Backplane;
		}

		if (backplane is null && ThrowIfMissingBackplane)
		{
			throw new InvalidOperationException("A backplane has not been specified, or found in the DI container.");
		}

		if (backplane is not null)
		{
			cache.SetupBackplane(backplane);
		}

		// PLUGINS
		List<IFusionCachePlugin> plugins = [];

		if (UseAllRegisteredPlugins)
		{
			plugins.AddRange(serviceProvider.GetServices<IFusionCachePlugin>());
		}

		if (PluginsServiceKey is not null)
		{
			plugins.AddRange(serviceProvider.GetKeyedServices<IFusionCachePlugin>(PluginsServiceKey));
		}

		if (Plugins?.Count > 0)
		{
			plugins.AddRange(Plugins);
		}

		if (PluginsFactories?.Count > 0)
		{
			foreach (var pluginFactory in PluginsFactories)
			{
				var plugin = pluginFactory?.Invoke(serviceProvider);

				if (plugin is not null)
					plugins.Add(plugin);
			}
		}

		if (plugins.Count > 0)
		{
			foreach (var plugin in plugins)
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
		}

		// CUSTOM SETUP ACTION
		PostSetupAction?.Invoke(serviceProvider, cache);

		_cache ??= cache;

		return cache;
	}
}
