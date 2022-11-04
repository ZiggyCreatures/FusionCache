using System;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
	/// </summary>
	public static class FusionCacheNeueccMessagePackSerializerServiceCollectionExtensions
	{
		/// <summary>
		/// Adds an implementation of <see cref="IFusionCacheSerializer"/> based on the famous Neuecc's MessagePack one.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
		/// <param name="options">The <see cref="MessagePackSerializerOptions"/> to use: if not specified, the contract-less (<see cref="ContractlessStandardResolver"/>) options will be used.</param>
		/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
		public static IServiceCollection AddFusionCacheNeueccMessagePackSerializer(this IServiceCollection services, MessagePackSerializerOptions? options = null)
		{
			if (services is null)
				throw new ArgumentNullException(nameof(services));

			services.TryAddSingleton<IFusionCacheSerializer>(_ => new FusionCacheNeueccMessagePackSerializer(options));

			return services;
		}
	}
}
