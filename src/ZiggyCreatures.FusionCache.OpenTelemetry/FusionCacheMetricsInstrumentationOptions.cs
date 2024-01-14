namespace ZiggyCreatures.FusionCache.OpenTelemetry
{
	/// <summary>
	/// Represents the options available for the metrics instrumentation of FusionCache.
	/// </summary>
	public class FusionCacheMetricsInstrumentationOptions
	{
		/// <summary>
		/// Include metrics for the memory level. (default: <see langword="false"/>)
		/// </summary>
		public bool IncludeMemoryLevel { get; set; } = false;

		/// <summary>
		/// Include metrics for the distributed level. (default: <see langword="false"/>)
		/// </summary>
		public bool IncludeDistributedLevel { get; set; } = false;

		/// <summary>
		/// Include metrics for the backplane. (default: <see langword="false"/>)
		/// </summary>
		public bool IncludeBackplane { get; set; } = false;
	}
}
