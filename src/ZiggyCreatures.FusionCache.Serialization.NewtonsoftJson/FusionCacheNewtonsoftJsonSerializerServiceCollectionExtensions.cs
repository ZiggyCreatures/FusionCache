using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using System;
using ZiggyCreatures.FusionCaching.Serialization;
using ZiggyCreatures.FusionCaching.Serialization.NewtonsoftJson;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for setting up fusion cache related services in an <see cref="IServiceCollection" />.
	/// </summary>
	public static class FusionCacheNewtonsoftJsonSerializerServiceCollectionExtensions
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
	}
}