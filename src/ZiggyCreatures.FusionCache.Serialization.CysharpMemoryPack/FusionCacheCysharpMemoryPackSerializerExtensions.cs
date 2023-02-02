using System;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheCysharpMemoryPackSerializerExtensions
{
	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> which uses Cysharp's MemoryPack serializer.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="options">The <see cref="MemoryPackSerializerOptions"/> to use.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheCysharpMemoryPackSerializer(this IServiceCollection services, MemoryPackSerializerOptions? options = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheCysharpMemoryPackSerializer(options));

		return services;
	}

	/// <summary>
	/// Adds an <see cref="IFusionCacheSerializer"/> based on the famous Neuecc's MessagePack one.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the serializer to.</param>
	/// <param name="options">The <see cref="MemoryPackSerializerOptions"/> to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithCysharpMemoryPackSerializer(this IFusionCacheBuilder builder, MemoryPackSerializerOptions? options = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithSerializer(new FusionCacheCysharpMemoryPackSerializer(options))
		;
	}
}
