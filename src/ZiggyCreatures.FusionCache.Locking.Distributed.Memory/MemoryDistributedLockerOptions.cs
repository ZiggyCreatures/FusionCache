using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Locking.Distributed.Memory;

/// <summary>
/// Represents the options available for the in-memory distributed locker.
/// </summary>
public class MemoryDistributedLockerOptions
	: IOptions<MemoryDistributedLockerOptions>
{
	/// <summary>
	/// Initializes a new instance of the RedisDistributedLockerOptions class with default settings.
	/// </summary>
	public MemoryDistributedLockerOptions()
	{
		Size = 210;
	}

	/// <summary>
	/// The size of the pool used internally for the 1st level locking strategy.
	/// </summary>
	public int Size { get; set; }

	MemoryDistributedLockerOptions IOptions<MemoryDistributedLockerOptions>.Value
	{
		get { return this; }
	}
}
