namespace ZiggyCreatures.Caching.Fusion.Simulator.Stuff;

public class SimulatedCluster
{
	public List<SimulatedNode> Nodes { get; } = new List<SimulatedNode>();
	public int? LastUpdatedNodeIndex { get; set; }
}
