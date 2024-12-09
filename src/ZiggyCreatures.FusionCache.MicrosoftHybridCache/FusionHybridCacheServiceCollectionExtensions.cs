using Microsoft.Extensions.Caching.Hybrid;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache as a HybridCache implementation in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionHybridCacheServiceCollectionExtensions
{
	/// <summary>
	/// Register this FusionCache instance also as a <see cref="HybridCache"/> service, so that it can be used even when you need to depend on Microsoft's own hybrid cache abstraction.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns></returns>
	public static IFusionCacheBuilder AsHybridCache(this IFusionCacheBuilder builder)
	{
		builder.Services.AddSingleton<HybridCache>(sp =>
		{
			return new FusionHybridCache(builder.Build(sp));
		});

		return builder;
	}

	/// <summary>
	/// Register this FusionCache instance also as a keyed <see cref="HybridCache"/> service, so that it can be used even when you need to depend on Microsoft's own hybrid cache abstraction, and even with the [FromKeyedServices] attribute usage.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <param name="serviceKey">The keyed service key to use.</param>
	/// <returns></returns>
	public static IFusionCacheBuilder AsKeyedHybridCache(this IFusionCacheBuilder builder, object? serviceKey)
	{
		builder.Services.AddKeyedSingleton<HybridCache>(serviceKey, (sp, _) =>
		{
			return new FusionHybridCache(builder.Build(sp));
		});

		return builder;
	}

	/// <summary>
	/// Register this FusionCache instance also as a keyed <see cref="HybridCache"/> service (with the CacheName as the serviceKey), so that it can be used even when you need to depend on Microsoft's own hybrid cache abstraction, and even with the [FromKeyedServices] attribute usage.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services"/>
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to act upon.</param>
	/// <returns></returns>
	public static IFusionCacheBuilder AsKeyedHybridCacheByCacheName(this IFusionCacheBuilder builder)
	{
		return builder.AsKeyedHybridCache(builder.CacheName);
	}
}
