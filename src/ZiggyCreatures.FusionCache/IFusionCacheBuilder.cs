using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion
{
	public interface IFusionCacheBuilder
	{
		/// <summary>
		/// Gets the name of the FusionCache instance configured by this builder.
		/// </summary>
		string CacheName { get; }

		bool UseRegisteredLogger { get; set; }
		ILogger<FusionCache>? Logger { get; set; }

		bool UseRegisteredMemoryCache { get; set; }
		IMemoryCache? MemoryCache { get; set; }

		bool UseRegisteredOptions { get; set; }
		Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

		FusionCacheEntryOptions DefaultEntryOptions { get; set; }

		bool UseRegisteredDistributedCache { get; set; }
		bool IgnoreRegisteredMemoryDistributedCache { get; set; }
		bool ThrowIfMissingSerializer { get; set; }
		IDistributedCache? DistributedCache { get; set; }
		IFusionCacheSerializer? Serializer { get; set; }

		bool UseRegisteredBackplane { get; set; }
		IFusionCacheBackplane? Backplane { get; set; }

		bool UseAllRegisteredPlugins { get; set; }
		List<IFusionCachePlugin> Plugins { get; }

		Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }
	}
}
