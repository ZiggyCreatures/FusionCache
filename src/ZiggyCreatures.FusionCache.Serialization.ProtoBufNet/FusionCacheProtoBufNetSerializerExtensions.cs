using Microsoft.Extensions.DependencyInjection.Extensions;
using ProtoBuf.Meta;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheProtoBufNetSerializerExtensions
{
	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> which uses protobuf-net, one of the most used .NET Protobuf serializer, by Marc Gravell.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="model">The <see cref="RuntimeTypeModel"/> to use: if not specified, the default one (<see cref="RuntimeTypeModel.Default"/>) will be used.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheProtoBufNetSerializer(this IServiceCollection services, RuntimeTypeModel? model = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheProtoBufNetSerializer(model));

		return services;
	}

	/// <summary>
	/// Adds an <see cref="IFusionCacheSerializer"/> which uses protobuf-net, one of the most used .NET Protobuf serializer, by Marc Gravell.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the serializer to.</param>
	/// <param name="model">The <see cref="RuntimeTypeModel"/> to use: if not specified, the default one (<see cref="RuntimeTypeModel.Default"/>) will be used.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithProtoBufNetSerializer(this IFusionCacheBuilder builder, RuntimeTypeModel? model = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithSerializer(new FusionCacheProtoBufNetSerializer(model))
		;
	}
}
