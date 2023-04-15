using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace FusionCacheTests.Stuff
{
	internal class NullPlugin
		: IFusionCachePlugin
	{
		public void Start(IFusionCache cache)
		{
			// EMPTY
		}

		public void Stop(IFusionCache cache)
		{
			// EMPTY
		}
	}
}
