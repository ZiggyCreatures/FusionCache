using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Plugins.StackExchangeRedisBackplane
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

		RedisBackplaneOptions IOptions<RedisBackplaneOptions>.Value
		{
			get { return this; }
		}
	}
}
