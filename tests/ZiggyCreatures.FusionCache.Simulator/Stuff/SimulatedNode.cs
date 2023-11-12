namespace ZiggyCreatures.Caching.Fusion.Simulator.Stuff
{
	public class SimulatedNode
	{
		public SimulatedNode(IFusionCache cache)
		{
			Cache = cache;
		}

		public IFusionCache Cache { get; }
		public long? ExpirationTimestampUnixMs { get; set; }
	}
}
