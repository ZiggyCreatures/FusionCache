using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion
{
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
		/// Indicates if the builder should try find and use an <see cref="ILogger{FusionCache}"/> service registered in the DI container.
		/// </summary>
		bool UseRegisteredLogger { get; set; }

		/// <summary>
		/// A specific <see cref="ILogger{FusionCache}"/> instance to be used.
		/// </summary>
		ILogger<FusionCache>? Logger { get; set; }

		/// <summary>
		/// Indicates if the builder should try find and use an <see cref="IMemoryCache"/> service registered in the DI container.
		/// </summary>
		bool UseRegisteredMemoryCache { get; set; }

		/// <summary>
		/// A specific <see cref="IMemoryCache"/> instance to be used.
		/// </summary>
		IMemoryCache? MemoryCache { get; set; }

		/// <summary>
		/// Indicates if the builder should try find and use an <see cref="IOptions{FusionCacheOptions}"/> service registered in the DI container.
		/// </summary>
		bool UseRegisteredOptions { get; set; }

		/// <summary>
		/// A custom setup logic for the <see cref="FusionCacheOptions"/> object, to allow for fine-grained customization.
		/// </summary>
		Action<FusionCacheOptions>? SetupOptionsAction { get; set; }

		/// <summary>
		/// A custom <see cref="FusionCacheEntryOptions"/> object to be used as the <see cref="FusionCacheOptions.DefaultEntryOptions"/>.
		/// </summary>
		FusionCacheEntryOptions DefaultEntryOptions { get; set; }

		/// <summary>
		/// Indicates if the builder should try find and use an <see cref="IDistributedCache"/> service registered in the DI container.
		/// </summary>
		bool UseRegisteredDistributedCache { get; set; }

		/// <summary>
		/// When trying to find an <see cref="IDistributedCache"/> service registered in the DI container, ignore it if it is of type <see cref="MemoryDistributedCache"/>, since that is not really a distributed cache and it's automatically registered by ASP.NET MVC without control from the user.
		/// </summary>
		bool IgnoreRegisteredMemoryDistributedCache { get; set; }

		/// <summary>
		/// If an <see cref="IDistributedCache"/> service is about to be used, but a valid <see cref="IFusionCacheSerializer"/> has not been provided, throw an exception: this is useful to avoid being convinced of having a distributed cache when, in reality, that is not the case since a serializer is needed for it to work.
		/// </summary>
		bool ThrowIfMissingSerializer { get; set; }

		/// <summary>
		/// A specific <see cref="IDistributedCache"/> instance to be used.
		/// </summary>
		IDistributedCache? DistributedCache { get; set; }

		/// <summary>
		/// A specific <see cref="IFusionCacheSerializer"/> instance to be used.
		/// </summary>
		IFusionCacheSerializer? Serializer { get; set; }

		/// <summary>
		/// Indicates if the builder should try find and use an <see cref="IFusionCacheBackplane"/> service registered in the DI container.
		/// </summary>
		bool UseRegisteredBackplane { get; set; }

		/// <summary>
		/// A specific <see cref="IFusionCacheBackplane"/> instance to be used.
		/// </summary>
		IFusionCacheBackplane? Backplane { get; set; }

		/// <summary>
		/// Indicates if the builder should try find and use any available <see cref="IFusionCachePlugin"/> services registered in the DI container.
		/// </summary>
		bool UseAllRegisteredPlugins { get; set; }

		/// <summary>
		/// A specific set of <see cref="IFusionCachePlugin"/> instances to be used.
		/// </summary>
		List<IFusionCachePlugin> Plugins { get; }

		/// <summary>
		/// A custom post-setup action, that will be invoked just after the creation of the FusionCache instance, and before returning it to the caller.
		/// <br/><br/>
		/// <strong>NOTE:</strong> it is possible to add actions multiple times, to add multiple post-setup calls one after the other to combine them for a powerful result.
		/// </summary>
		Action<IServiceProvider, IFusionCache>? PostSetupAction { get; set; }
	}
}
