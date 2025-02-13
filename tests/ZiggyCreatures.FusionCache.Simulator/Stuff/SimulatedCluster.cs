namespace ZiggyCreatures.Caching.Fusion.Simulator.Stuff;

public class SimulatedCluster
{
	public List<SimulatedNode> Nodes { get; } = new();
	public int? LastUpdatedNodeIndex { get; set; }
}
