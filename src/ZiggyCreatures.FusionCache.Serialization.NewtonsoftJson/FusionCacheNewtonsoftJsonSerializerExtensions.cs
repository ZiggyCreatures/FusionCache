using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheNewtonsoftJsonSerializerExtensions
{
	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> based on the Newtonsoft JSON.NET one.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="settings">The <see cref="JsonSerializerSettings"/> to use.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheNewtonsoftJsonSerializer(this IServiceCollection services, JsonSerializerSettings? settings = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheNewtonsoftJsonSerializer(settings));

		return services;
	}

	/// <summary>
	/// Adds an <see cref="IFusionCacheSerializer"/> based on the Newtonsoft JSON.NET one.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the serializer to.</param>
	/// <param name="settings">The <see cref="JsonSerializerSettings"/> to use.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithNewtonsoftJsonSerializer(this IFusionCacheBuilder builder, JsonSerializerSettings? settings = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithSerializer(new FusionCacheNewtonsoftJsonSerializer(settings))
		;
	}
}
