using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProtoBuf.Meta;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
	/// </summary>
	public static class FusionCacheNeueccMessagePackSerializerServiceCollectionExtensions
	{
		/// <summary>
		/// Adds an implementation of <see cref="IFusionCacheSerializer"/> which uses the most used .NET ProtoBuf serializer, by Marc Gravell.
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
	}
}
