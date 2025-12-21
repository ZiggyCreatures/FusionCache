using ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

namespace ZiggyCreatures.Caching.Fusion.Playground;

class Program
{
	static async Task Main(string[] args)
	{
		await MultiNodesNoBackplaneScenario.RunAsync().ConfigureAwait(false);
	}
}
