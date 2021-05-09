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

			General = new FusionCacheLayerEventsHub(_cache, _options, _logger);
			Memory = new FusionCacheLayerEventsHub(_cache, _options, _logger);
			Distributed = new FusionCacheLayerEventsHub(_cache, _options, _logger);
		}

		public FusionCacheLayerEventsHub General { get; }
		public FusionCacheLayerEventsHub Memory { get; }
		public FusionCacheLayerEventsHub Distributed { get; }
	}
}
