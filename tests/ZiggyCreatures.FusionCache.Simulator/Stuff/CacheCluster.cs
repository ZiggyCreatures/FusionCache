namespace ZiggyCreatures.Caching.Fusion.Simulator.Stuff
{
	public class CacheCluster
	{
		public List<CacheNode> Nodes { get; } = new List<CacheNode>();
		public int? LastUpdatedNodeIndex { get; set; }
	}
}
