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

			UseRegisteredOptions = true;
			UseRegisteredLogger = true;
			UseRegisteredMemoryCache = false;
			UseRegisteredReactor = true;
			UseRegisteredDistributedCache = false;
			UseRegisteredSerializer = true;
			IgnoreRegisteredMemoryDistributedCache = true;
			UseRegisteredBackplane = false;
			UseAllRegisteredPlugins = false;
			Plugins = new List<IFusionCachePlugin>();
		}

		public string CacheName { get; }

		public bool UseRegisteredLogger { get; set; }
		public ILogger<FusionCache>? Logger { get; set; }

		public bool UseRegisteredMemoryCache { get; set; }
		public IMemoryCache? MemoryCache { get; set; }

		private bool UseRegisteredReactor { get; set; }

		public bool UseRegisteredOptions { get; set; }
		public FusionCacheOptions? Options { get; set; }
		public Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

		public FusionCacheEntryOptions? DefaultEntryOptions { get; set; }
		public Action<FusionCacheEntryOptions>? SetupDefaultEntryOptionsAction { get; set; }

		public bool UseRegisteredSerializer { get; set; }
		public bool ThrowIfMissingSerializer { get; set; }
		public IFusionCacheSerializer? Serializer { get; set; }

		public bool UseRegisteredDistributedCache { get; set; }
		public bool IgnoreRegisteredMemoryDistributedCache { get; set; }
		public IDistributedCache? DistributedCache { get; set; }

		public bool UseRegisteredBackplane { get; set; }
		public IFusionCacheBackplane? Backplane { get; set; }

		public bool UseAllRegisteredPlugins { get; set; }
		public List<IFusionCachePlugin> Plugins { get; }

		public Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }

		public IFusionCache Build(IServiceProvider serviceProvider)
		{
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
			ILogger<FusionCache>? logger = null;
			if (UseRegisteredLogger)
			{
				logger = serviceProvider.GetService<ILogger<FusionCache>>();
			}
			else
			{
				logger = Logger;
			}

			// MEMORY CACHE
			IMemoryCache? memoryCache;

			if (UseRegisteredMemoryCache)
			{
				memoryCache = serviceProvider.GetService<IMemoryCache>();
			}
			else
			{
				memoryCache = MemoryCache;
			}

			// REACTOR
			IFusionCacheReactor? reactor = null;

			if (UseRegisteredReactor)
			{
				reactor = serviceProvider.GetService<IFusionCacheReactor>();
			}

			// CREATE CACHE
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
			else
			{
				distributedCache = DistributedCache;
			}

			if (distributedCache is not null)
			{
				IFusionCacheSerializer? serializer;
				if (UseRegisteredSerializer)
				{
					serializer = serviceProvider.GetService<IFusionCacheSerializer>();
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
						logger.LogWarning("FUSION: a usable implementation of IDistributedCache was found (CACHE={DistributedCacheType}) but no implementation of IFusionCacheSerializer was found, so the distributed cache subsystem has not been set up", distributedCache.GetType().FullName);

					if (ThrowIfMissingSerializer)
					{
						throw new InvalidOperationException($"A distributed cache was about to be used ({distributedCache.GetType().FullName}) but no implementation of IFusionCacheSerializer has been specified or found, so the distributed cache subsystem has not been set up");
					}
				}
			}

			// BACKPLANE
			if (UseRegisteredBackplane)
			{
				var backplane = serviceProvider.GetService<IFusionCacheBackplane>();

				if (backplane is not null)
				{
					cache.SetupBackplane(backplane);
				}
			}

			// PLUGINS
			List<IFusionCachePlugin>? plugins = null;

			if (UseAllRegisteredPlugins)
			{
				plugins = serviceProvider.GetServices<IFusionCachePlugin>()?.ToList();
			}

			if (Plugins?.Any() == true)
			{
				if (plugins is null)
					plugins = new List<IFusionCachePlugin>();

				plugins.AddRange(Plugins);
			}

			if (plugins?.Any() == true)
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
