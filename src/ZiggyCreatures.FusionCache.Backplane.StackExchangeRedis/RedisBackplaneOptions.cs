using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

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
	/// The configuration used to connect to Redis.
	/// </summary>
	public bool VerifyReceivedClientsCountAfterPublish { get; set; } = false;

	RedisBackplaneOptions IOptions<RedisBackplaneOptions>.Value
	{
		get { return this; }
	}
}
