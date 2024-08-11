namespace ZiggyCreatures.FusionCache.OpenTelemetry
{
	/// <summary>
	/// Represents the options available for the traces instrumentation of FusionCache.
	/// </summary>
	public class FusionCacheTracesInstrumentationOptions
	{
		/// <summary>
		/// Include traces for the memory level. (default: <see langword="false"/>)
		/// </summary>
		public bool IncludeMemoryLevel { get; set; } = false;

		/// <summary>
		/// Include traces for the distributed level. (default: <see langword="true"/>)
		/// </summary>
		public bool IncludeDistributedLevel { get; set; } = true;

		/// <summary>
		/// Include traces for the backplane. (default: <see langword="false"/>)
		/// </summary>
		public bool IncludeBackplane { get; set; } = false;
	}
}
