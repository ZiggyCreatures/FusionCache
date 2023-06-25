using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Reactors;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Builder
{
	internal class FusionCacheBuilder
		: IFusionCacheBuilder
	{
		public FusionCacheBuilder(string cacheName)
		{
			CacheName = cacheName;

			UseRegisteredLogger = true;

			UseRegisteredOptions = true;

			UseRegisteredReactor = true;

			UseRegisteredSerializer = true;

			IgnoreRegisteredMemoryDistributedCache = true;

			Plugins = new List<IFusionCachePlugin>();
			PluginsFactories = new List<Func<IServiceProvider, IFusionCachePlugin>>();
		}

		public string CacheName { get; }

		public bool UseRegisteredLogger { get; set; }
		public ILogger<FusionCache>? Logger { get; set; }
		public Func<IServiceProvider, ILogger<FusionCache>>? LoggerFactory { get; set; }
		public bool ThrowIfMissingLogger { get; set; }

		public bool UseRegisteredMemoryCache { get; set; }
		public IMemoryCache? MemoryCache { get; set; }
		public Func<IServiceProvider, IMemoryCache>? MemoryCacheFactory { get; set; }
		public bool ThrowIfMissingMemoryCache { get; set; }

		private bool UseRegisteredReactor { get; set; }

		public bool UseRegisteredOptions { get; set; }
		public FusionCacheOptions? Options { get; set; }
		public bool UseCacheKeyPrefix { get; set; }
		public string? CacheKeyPrefix { get; set; }
		public Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

		public FusionCacheEntryOptions? DefaultEntryOptions { get; set; }
		public Action<FusionCacheEntryOptions>? SetupDefaultEntryOptionsAction { get; set; }

		public bool UseRegisteredSerializer { get; set; }
		public IFusionCacheSerializer? Serializer { get; set; }
		public Func<IServiceProvider, IFusionCacheSerializer>? SerializerFactory { get; set; }
		public bool ThrowIfMissingSerializer { get; set; }

		public bool UseRegisteredDistributedCache { get; set; }
		public bool IgnoreRegisteredMemoryDistributedCache { get; set; }
		public IDistributedCache? DistributedCache { get; set; }
		public Func<IServiceProvider, IDistributedCache>? DistributedCacheFactory { get; set; }
		public bool ThrowIfMissingDistributedCache { get; set; }

		public bool UseRegisteredBackplane { get; set; }
		public IFusionCacheBackplane? Backplane { get; set; }
		public Func<IServiceProvider, IFusionCacheBackplane>? BackplaneFactory { get; set; }
		public bool ThrowIfMissingBackplane { get; set; }

		public bool UseAllRegisteredPlugins { get; set; }
		public List<IFusionCachePlugin> Plugins { get; }
		public List<Func<IServiceProvider, IFusionCachePlugin>> PluginsFactories { get; }

		public Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }

		public IFusionCache Build(IServiceProvider serviceProvider)
		{
			if (serviceProvider is null)
				throw new ArgumentNullException(nameof(serviceProvider));

			// OPTIONS
			FusionCacheOptions? options = null;

			if (UseRegisteredOptions)
			{
				options = serviceProvider.GetRequiredService<IOptionsMonitor<FusionCacheOptions>>().Get(CacheName);
			}

			if (options is null)
			{
				options = Options;
			}

			if (options is null)
			{
				options = new FusionCacheOptions();
			}

			// ENSURE CACHE NAME
			options.CacheName = CacheName;

			// CACHE KEY PREFIX
			if (UseCacheKeyPrefix)
			{
				options.CacheKeyPrefix = CacheKeyPrefix;
			}

			if (SetupOptionsAction is not null)
			{
				SetupOptionsAction?.Invoke(options);
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

			// LOGGER
			ILogger<FusionCache>? logger;

			if (UseRegisteredLogger)
			{
				logger = serviceProvider.GetService<ILogger<FusionCache>>();
			}
			else if (LoggerFactory is not null)
			{
				logger = LoggerFactory?.Invoke(serviceProvider);
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
				memoryCache = serviceProvider.GetService<IMemoryCache>();
			}
			else if (MemoryCacheFactory is not null)
			{
				memoryCache = MemoryCacheFactory?.Invoke(serviceProvider);
			}
			else
			{
				memoryCache = MemoryCache;
			}

			if (memoryCache is null && ThrowIfMissingMemoryCache)
			{
				throw new InvalidOperationException("A memory cache has not been specified, or found in the DI container.");
			}

			// REACTOR
			IFusionCacheReactor? reactor = null;

			if (UseRegisteredReactor)
			{
				reactor = serviceProvider.GetService<IFusionCacheReactor>();
			}

			// CREATE THE CACHE
			var cache = new FusionCache(options, memoryCache, logger, reactor);

			// DISTRIBUTED CACHE
			IDistributedCache? distributedCache;
			if (UseRegisteredDistributedCache)
			{
				distributedCache = serviceProvider.GetService<IDistributedCache>();
				if (IgnoreRegisteredMemoryDistributedCache && distributedCache is MemoryDistributedCache)
				{
					distributedCache = null;
				}
			}
			else if (DistributedCacheFactory is not null)
			{
				distributedCache = DistributedCacheFactory?.Invoke(serviceProvider);
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
				IFusionCacheSerializer? serializer;
				if (UseRegisteredSerializer)
				{
					serializer = serviceProvider.GetService<IFusionCacheSerializer>();
				}
				else if (SerializerFactory is not null)
				{
					serializer = SerializerFactory?.Invoke(serviceProvider);
				}
				else
				{
					serializer = Serializer;
				}

				if (serializer is not null)
				{
					cache.SetupDistributedCache(distributedCache, serializer);
				}
				else
				{
					if (logger?.IsEnabled(LogLevel.Warning) ?? false)
						logger.Log(LogLevel.Warning, "FUSION: a usable implementation of IDistributedCache was found (CACHE={DistributedCacheType}) but no implementation of IFusionCacheSerializer was found, so the distributed cache subsystem has not been set up", distributedCache.GetType().FullName);

					if (ThrowIfMissingSerializer)
					{
						throw new InvalidOperationException($"A distributed cache was about to be used ({distributedCache.GetType().FullName}) but no implementation of IFusionCacheSerializer has been specified or found, so the distributed cache subsystem has not been set up");
					}
				}
			}

			// BACKPLANE
			IFusionCacheBackplane? backplane;
			if (UseRegisteredBackplane)
			{
				backplane = serviceProvider.GetService<IFusionCacheBackplane>();
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
			List<IFusionCachePlugin> plugins = new List<IFusionCachePlugin>();

			if (UseAllRegisteredPlugins)
			{
				plugins.AddRange(serviceProvider.GetServices<IFusionCachePlugin>());
			}

			if (Plugins?.Any() == true)
			{
				plugins.AddRange(Plugins);
			}

			if (PluginsFactories?.Any() == true)
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
			if (PostSetupAction is not null)
			{
				PostSetupAction?.Invoke(serviceProvider, cache);
			}

			return cache;
		}
	}
}
