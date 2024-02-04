using System;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.FusionCache.OpenTelemetry;

namespace OpenTelemetry.Metrics
{
	/// <summary>
	/// Contains extension methods to <see cref="MeterProviderBuilder"/> for enabling FusionCache metrics instrumentation.
	/// </summary>
	public static class MeterProviderBuilderExtensions
	{
		/// <summary>
		/// Enables metrics instrumentation for FusionCache.
		/// </summary>
		/// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
		/// <param name="configure">Callback action for configuring the available options.</param>
		/// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
		public static MeterProviderBuilder AddFusionCacheInstrumentation(this MeterProviderBuilder builder, Action<FusionCacheMetricsInstrumentationOptions>? configure = null)
		{
			if (builder is null)
				throw new ArgumentNullException(nameof(builder));

			var options = new FusionCacheMetricsInstrumentationOptions();
			configure?.Invoke(options);

			builder.AddMeter(FusionCacheDiagnostics.MeterName);
			if (options.IncludeMemoryLevel)
				builder.AddMeter(FusionCacheDiagnostics.MeterNameMemoryLevel);
			if (options.IncludeDistributedLevel)
				builder.AddMeter(FusionCacheDiagnostics.MeterNameDistributedLevel);
			if (options.IncludeBackplane)
				builder.AddMeter(FusionCacheDiagnostics.MeterNameBackplane);

			return builder;
		}
	}
}
