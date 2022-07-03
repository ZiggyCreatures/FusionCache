using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// Represents all the options available for the entire <see cref="IFusionCache"/> instance.
	/// </summary>
	public class FusionCacheOptions
		: IOptions<FusionCacheOptions>
	{
		private string _cacheName;
		private FusionCacheEntryOptions _defaultEntryOptions;

		/// <summary>
		/// Creates a new instance of a <see cref="FusionCacheOptions"/> object.
		/// </summary>
		public FusionCacheOptions()
		{
			_cacheName = "FusionCache";

			_defaultEntryOptions = new FusionCacheEntryOptions();

			// LOG LEVELS
			SerializationErrorsLogLevel = LogLevel.Error;

			DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Warning;
			DistributedCacheErrorsLogLevel = LogLevel.Warning;

			FactorySyntheticTimeoutsLogLevel = LogLevel.Warning;
			FactoryErrorsLogLevel = LogLevel.Warning;

			FailSafeActivationLogLevel = LogLevel.Warning;
			EventHandlingErrorsLogLevel = LogLevel.Warning;

			BackplaneSyntheticTimeoutsLogLevel = LogLevel.Warning;
			BackplaneErrorsLogLevel = LogLevel.Warning;
		}

		/// <summary>
		/// The name of the cache: it can be used for identification, and in a multi-node scenario it is typically shared between nodes to create a logical association.
		/// <br/><br/>
		/// <strong>NOTE:</strong> if you try to set this to a null/whitespace value, that value will be ignored.
		/// </summary>
		public string CacheName
		{
			get { return _cacheName; }
			set
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					//throw new ArgumentNullException(nameof(value), "It is not possible to set the CacheName to null");
					return;
				}

				_cacheName = value;
			}
		}

		/// <summary>
		/// The default <see cref="FusionCacheEntryOptions"/> to use when none will be specified, and as the starting point when duplicating one.
		/// </summary>
		public FusionCacheEntryOptions DefaultEntryOptions
		{
			get { return _defaultEntryOptions; }
			set
			{
				if (value is null)
					throw new ArgumentNullException(nameof(value), "It is not possible to set the DefaultEntryOptions to null");

				_defaultEntryOptions = value;
			}
		}

		/// <summary>
		/// The duration of the circuit-breaker used when working with the distributed cache. Defaults to <see cref="TimeSpan.Zero"/>, which means the circuit-breaker will never be activated.
		/// </summary>
		public TimeSpan DistributedCacheCircuitBreakerDuration { get; set; }

		/// <summary>
		/// <strong>WARNING:</strong> this feature has been removed: please pre-process your cache keys yourself before passing them to FusionCache, they will not be touched in any way anymore.
		/// <br/>
		/// For more info see <a href="https://github.com/jodydonetti/ZiggyCreatures.FusionCache/issues/33">https://github.com/jodydonetti/ZiggyCreatures.FusionCache/issues/33</a> .
		/// <br/><br/>
		/// An optional <see cref="string"/> prefix to prepend to any cache key passed to the cache methods.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("This feature has been removed: please pre-process your cache keys yourself before passing them to FusionCache, they will not be touched in any way anymore. For more info see https://github.com/jodydonetti/ZiggyCreatures.FusionCache/issues/33", true)]
		public string? CacheKeyPrefix { get; set; }

		/// <summary>
		/// Execute event handlers in a sync fashion, waiting for all of them to complete before moving on.
		/// <br/><br/>
		/// <strong>WARNING:</strong> by default this option is NOT enabled, and should remain this way in any normal circumstance unless you really know what you are doing.
		/// </summary>
		public bool EnableSyncEventHandlersExecution { get; set; }

		/// <summary>
		/// Specify the mode in which cache key will be changed for the distributed cache (eg: to specify the wire format version).
		/// </summary>
		public CacheKeyModifierMode DistributedCacheKeyModifierMode { get; set; }

		/// <summary>
		/// The duration of the circuit-breaker used when working with the backplane. Defaults to <see cref="TimeSpan.Zero"/>, which means the circuit-breaker will never be activated.
		/// </summary>
		public TimeSpan BackplaneCircuitBreakerDuration { get; set; }

		/// <summary>
		/// The prefix to use in the backplane channel name: if not specified the <see cref="CacheName"/> will be used.
		/// </summary>
		public string? BackplaneChannelPrefix { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when an error occurs during serialization or deserialization while working with the distributed cache.
		/// </summary>
		public LogLevel SerializationErrorsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a distributed cache operation.
		/// </summary>
		public LogLevel DistributedCacheSyntheticTimeoutsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when any error (except for a synthetic timeout) occurs during a distributed cache operation.
		/// </summary>
		public LogLevel DistributedCacheErrorsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a factory execution.
		/// </summary>
		public LogLevel FactorySyntheticTimeoutsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when any error (except for a synthetic timeout) occurs during a factory execution.
		/// </summary>
		public LogLevel FactoryErrorsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when a fail-safe activation occurs.
		/// </summary>
		public LogLevel FailSafeActivationLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when an error occurs during the handling of an event.
		/// </summary>
		public LogLevel EventHandlingErrorsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a backplane cache operation.
		/// </summary>
		public LogLevel BackplaneSyntheticTimeoutsLogLevel { get; set; }

		/// <summary>
		/// Specify the <see cref="LogLevel"/> to use when an error occurs during a backplane operation.
		/// </summary>
		public LogLevel BackplaneErrorsLogLevel { get; set; }

		FusionCacheOptions IOptions<FusionCacheOptions>.Value
		{
			get { return this; }
		}
	}
}
