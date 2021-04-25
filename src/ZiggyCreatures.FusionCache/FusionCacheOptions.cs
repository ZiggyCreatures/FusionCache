using System;
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

		private FusionCacheEntryOptions _defaultEntryOptions;

		/// <summary>
		/// Creates a new instance of a <see cref="FusionCacheOptions"/> object.
		/// </summary>
		public FusionCacheOptions()
		{
			_defaultEntryOptions = new FusionCacheEntryOptions();

			// LOG LEVELS
			SerializationErrorsLogLevel = LogLevel.Error;
			DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Warning;
			DistributedCacheErrorsLogLevel = LogLevel.Warning;
			FactorySyntheticTimeoutsLogLevel = LogLevel.Warning;
			FactoryErrorsLogLevel = LogLevel.Warning;
			FailSafeActivationLogLevel = LogLevel.Warning;
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
		/// An optional <see cref="string"/> prefix to prepend to any cache key passed to the cache methods.
		/// </summary>
		public string? CacheKeyPrefix { get; set; }

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

		FusionCacheOptions IOptions<FusionCacheOptions>.Value
		{
			get { return this; }
		}

	}
}