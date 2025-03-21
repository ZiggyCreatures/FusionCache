using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheServiceStackJsonSerializerExtensions
{
	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheServiceStackJsonSerializer(this IServiceCollection services)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheServiceStackJsonSerializer());

		return services;
	}

	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the serializer to.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithServiceStackJsonSerializer(this IFusionCacheBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithSerializer(new FusionCacheServiceStackJsonSerializer())
		;
	}
}
