using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Events
{

	public class FusionCacheMemoryEventsHub
		: FusionCacheBaseEventsHub
	{

		public FusionCacheMemoryEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
			: base(cache, options, logger)
		{
		}

	}
}
