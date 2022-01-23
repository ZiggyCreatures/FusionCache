using System;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis
{
	/// <summary>
	/// Represents the options available for the Redis backplane.
	/// </summary>
	public class RedisBackplaneOptions
		: IOptions<RedisBackplaneOptions>
	{
		/// <summary>
		/// The configuration used to connect to Redis.
		/// </summary>
		public string? Configuration { get; set; }

		/// <summary>
		/// The configuration used to connect to Redis.
		/// This is preferred over Configuration.
		/// </summary>
		public ConfigurationOptions? ConfigurationOptions { get; set; }

		/// <summary>
		/// The prefix that will be used to construct the Redis pub/sub channel name.
		/// <br/><br/>
		/// NOTE: if not specified, the <see cref="IFusionCache.CacheName"/> will be used.
		/// </summary>
		public string? ChannelPrefix { get; set; }

		/// <summary>
		/// Enable the (low level) FireAndForget flag of the StackExchange Redis publish method.
		/// <br/><br/>
		/// NOTE: if enabled, the circuit-breaker may not notice errors while sending a notification.
		/// </summary>
		public bool AllowBackgroundOperations { get; set; }

		/// <summary>
		/// The duration of the circuit-breaker used when working with the backplane. Defaults to <see cref="TimeSpan.Zero"/>, which means the circuit-breaker will never be activated.
		/// </summary>
		public TimeSpan CircuitBreakerDuration { get; set; }

		RedisBackplaneOptions IOptions<RedisBackplaneOptions>.Value
		{
			get { return this; }
		}
	}
}
