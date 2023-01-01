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
	public static IFusionCacheBuilder WithDefaultEntryOptions(this IFusionCacheBuilder b, FusionCacheEntryOptions options)
	{
		b.DefaultEntryOptions = options;

		return b;
	}

	public static IFusionCacheBuilder WithDefaultEntryOptions(this IFusionCacheBuilder b, Action<FusionCacheEntryOptions> optionsSetup)
	{
		b.DefaultEntryOptions = new FusionCacheEntryOptions();
		optionsSetup?.Invoke(b.DefaultEntryOptions);

		return b;
	}

	public static IFusionCacheBuilder WithRegisteredMemoryCache(this IFusionCacheBuilder b)
	{
		b.UseRegisteredMemoryCache = true;

		return b;
	}

	public static IFusionCacheBuilder WithMemoryCache(this IFusionCacheBuilder b, IMemoryCache memoryCache)
	{
		b.UseRegisteredMemoryCache = false;
		b.MemoryCache = memoryCache;

		return b;
	}

	public static IFusionCacheBuilder WithRegisteredDistributedCache(this IFusionCacheBuilder b, bool ignoreMemoryDistributedCache = true)
	{
		b.UseRegisteredDistributedCache = true;
		b.IgnoreRegisteredMemoryDistributedCache = ignoreMemoryDistributedCache;

		return b;
	}

	public static IFusionCacheBuilder WithDistributedCache(this IFusionCacheBuilder b, IDistributedCache distributedCache, IFusionCacheSerializer serializer)
	{
		b.UseRegisteredDistributedCache = false;
		b.DistributedCache = distributedCache;
		b.Serializer = serializer;

		return b;
	}

	public static IFusionCacheBuilder WithoutDistributedCache(this IFusionCacheBuilder b)
	{
		b.UseRegisteredDistributedCache = false;
		b.DistributedCache = null;
		b.Serializer = null;

		return b;
	}

	public static IFusionCacheBuilder WithRegisteredBackplane(this IFusionCacheBuilder b)
	{
		b.UseRegisteredBackplane = true;

		return b;
	}

	public static IFusionCacheBuilder WithBackplane(this IFusionCacheBuilder b, IFusionCacheBackplane backplane)
	{
		b.UseRegisteredBackplane = false;
		b.Backplane = backplane;

		return b;
	}

	public static IFusionCacheBuilder WithoutBackplane(this IFusionCacheBuilder b)
	{
		b.UseRegisteredBackplane = false;
		b.Backplane = null;

		return b;
	}

	public static IFusionCacheBuilder WithAllRegisteredPlugins(this IFusionCacheBuilder b)
	{
		b.UseAllRegisteredPlugins = true;

		return b;
	}

	public static IFusionCacheBuilder WithPlugin(this IFusionCacheBuilder b, IFusionCachePlugin plugin)
	{
		b.UseAllRegisteredPlugins = false;
		b.Plugins.Add(plugin);

		return b;
	}

	public static IFusionCacheBuilder WithPlugins(this IFusionCacheBuilder b, params IFusionCachePlugin[] plugins)
	{
		b.UseAllRegisteredPlugins = false;
		b.Plugins.AddRange(plugins);

		return b;
	}

	public static IFusionCacheBuilder WithoutPlugins(this IFusionCacheBuilder b)
	{
		b.UseAllRegisteredPlugins = false;
		b.Plugins.Clear();

		return b;
	}

	public static IFusionCacheBuilder WithAllRegisteredComponents(this IFusionCacheBuilder b, bool ignoreMemoryDistributedCache = true)
	{
		return b
			.WithRegisteredDistributedCache(ignoreMemoryDistributedCache)
			.WithRegisteredBackplane()
			.WithAllRegisteredPlugins()
		;
	}

	public static IFusionCacheBuilder WithPostSetup(this IFusionCacheBuilder b, Action<IServiceProvider, IFusionCache> action)
	{
		b.PostSetupAction += action;

		return b;
	}
}
