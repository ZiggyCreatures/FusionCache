using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheEventsHub
	{
		private IFusionCache _cache;
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;

		public FusionCacheEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		{
			_cache = cache;
			_options = options;
			_logger = logger;

			General = new FusionCacheBaseEvents(_cache, _options, _logger);
			Memory = new FusionCacheBaseEvents(_cache, _options, _logger);
			Distributed = new FusionCacheDistributedEvents(_cache, _options, _logger);
		}

		public FusionCacheBaseEvents General { get; }
		public FusionCacheBaseEvents Memory { get; }
		public FusionCacheDistributedEvents Distributed { get; }
	}
}
