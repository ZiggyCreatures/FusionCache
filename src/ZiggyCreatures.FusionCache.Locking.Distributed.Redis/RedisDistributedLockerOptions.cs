using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;

/// <summary>
/// Represents the options available for the Redis distributed locker.
/// </summary>
public class RedisDistributedLockerOptions
	: IOptions<RedisDistributedLockerOptions>
{
	/// <summary>
	/// Initializes a new instance of the RedisDistributedLockerOptions class with default settings.
	/// </summary>
	public RedisDistributedLockerOptions()
	{
		AbandonTimeout = TimeSpan.FromSeconds(10);
	}

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
	/// A delegate to create the ConnectionMultiplexer instance.
	/// </summary>
	public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; set; }

	/// <summary>
	/// The amount of time after which a distributed lock is considered abandoned: default is 30 seconds.
	/// <br/>
	/// Normally, the distributed lock does auto-extension while held but, in case of failures or crashes, this timeout is used to avoid infinite waits or distributed deadlocks.
	/// </summary>
	public TimeSpan AbandonTimeout { get; internal set; }

	RedisDistributedLockerOptions IOptions<RedisDistributedLockerOptions>.Value
	{
		get { return this; }
	}
}
