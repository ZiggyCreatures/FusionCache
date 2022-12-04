using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class FusionCacheSystemTextJsonSerializerServiceCollectionExtensions
{
	/// <summary>
	/// Adds an implementation of <see cref="IFusionCacheSerializer"/> based on the System.Text.Json one.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="options">The <see cref="JsonSerializerOptions"/> to use.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheSystemTextJsonSerializer(this IServiceCollection services, JsonSerializerOptions? options = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheSystemTextJsonSerializer(options));

		return services;
	}
}
