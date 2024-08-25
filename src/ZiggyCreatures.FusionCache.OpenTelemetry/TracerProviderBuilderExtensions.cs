using System;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.OpenTelemetry;

namespace OpenTelemetry.Trace
{
	/// <summary>
	/// Contains extension methods to <see cref="TracerProviderBuilder"/> for enabling FusionCache traces instrumentation.
	/// </summary>
	public static class TracerProviderBuilderExtensions
	{
		/// <summary>
		/// Enable traces instrumentation for FusionCache.
		/// </summary>
		/// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
		/// <param name="configure">Callback action for configuring the available options.</param>
		/// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
		public static TracerProviderBuilder AddFusionCacheInstrumentation(this TracerProviderBuilder builder, Action<FusionCacheTracesInstrumentationOptions>? configure = null)
		{
			if (builder is null)
				throw new ArgumentNullException(nameof(builder));

			var options = new FusionCacheTracesInstrumentationOptions();
			configure?.Invoke(options);

			builder.AddSource(FusionCacheDiagnostics.ActivitySourceName);
			if (options.IncludeMemoryLevel)
				builder.AddSource(FusionCacheDiagnostics.ActivitySourceNameMemoryLevel);
			if (options.IncludeDistributedLevel)
				builder.AddSource(FusionCacheDiagnostics.ActivitySourceNameDistributedLevel);
			if (options.IncludeBackplane)
				builder.AddSource(FusionCacheDiagnostics.ActivitySourceNameBackplane);

			return builder;
		}
	}
}
