using Microsoft.Extensions.Caching.Hybrid;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache as a HybridCache implementation in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionHybridCacheServiceCollectionExtensions
{
	public static IFusionCacheBuilder AsHybridCache(this IFusionCacheBuilder builder)
	{
		builder.Services.AddSingleton<HybridCache>(sp =>
		{
			return new FusionHybridCache(builder.Build(sp));
		});

		return builder;
	}

	public static IFusionCacheBuilder AsKeyedHybridCache(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.Services.AddKeyedSingleton<HybridCache>(serviceKey, (sp, _) =>
		{
			return new FusionHybridCache(builder.Build(sp));
		});

		return builder;
	}

	public static IFusionCacheBuilder AsKeyedHybridCacheByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.AsKeyedHybridCache(builder.CacheName);
	}
}
