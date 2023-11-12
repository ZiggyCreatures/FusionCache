using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace FusionCacheTests.Stuff;

internal class SimplePlugin
	: IFusionCachePlugin
{
	public SimplePlugin(string name)
	{
		Name = name;
	}

	public string Name { get; }
	public bool IsRunning { get; private set; }

	public void Start(IFusionCache cache)
	{
		IsRunning = true;
	}

	public void Stop(IFusionCache cache)
	{
		IsRunning = false;
	}
}
