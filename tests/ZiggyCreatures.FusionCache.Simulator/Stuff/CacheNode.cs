namespace ZiggyCreatures.Caching.Fusion.Simulator.Stuff
{
	public class CacheNode
	{
		public CacheNode(IFusionCache cache)
		{
			Cache = cache;
		}

		public IFusionCache Cache { get; }
		public long? ExpirationTimestampUnixMs { get; set; }
	}
}
