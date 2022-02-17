using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// An abstract class with base plumbing.
	/// </summary>
	public abstract class FusionCacheAbstractEventsHub
	{
		/// <summary>
		/// The <see cref="IFusionCache"/> instance.
		/// </summary>
		protected IFusionCache _cache;

		/// <summary>
		/// The <see cref="FusionCacheOptions"/> instance.
		/// </summary>
		protected readonly FusionCacheOptions _options;

		/// <summary>
		/// The <see cref="ILogger"/> instance.
		/// </summary>
		protected readonly ILogger? _logger;

		/// <summary>
		/// The <see cref="LogLevel"/> for errors during event handling.
		/// </summary>
		protected LogLevel _errorsLogLevel;

		/// <summary>
		/// The execution mode for event handlers.
		/// </summary>
		protected bool _syncExecution;

		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheAbstractEventsHub"/> class.
		/// </summary>
		/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
		/// <param name="options">The <see cref="FusionCacheOptions"/> instance.</param>
		/// <param name="logger">The <see cref="ILogger"/> instance.</param>
		protected FusionCacheAbstractEventsHub(IFusionCache cache, FusionCacheOptions options, ILogger? logger)
		{
			_cache = cache;
			_options = options;
			_logger = logger;

			_errorsLogLevel = _options.EventHandlingErrorsLogLevel;
			_syncExecution = _options.EnableSyncEventHandlersExecution;
		}
	}
}
