using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory;

/// <summary>
/// Represents the options available for the memory backplane.
/// </summary>
public class MemoryBackplaneOptions
	: IOptions<MemoryBackplaneOptions>
{
	/// <summary>
	/// The logical id used to simulate a connection to a server.
	/// <br/>
	/// It is used to group together multiple instances of a MemoryBackplane and separate them from others, without interfering with other backplanes running concurrently at the same time (mostly useful for testing).
	/// <br/>
	/// Basically it's like a connection string.
	/// </summary>
	public string? ConnectionId { get; set; }

	MemoryBackplaneOptions IOptions<MemoryBackplaneOptions>.Value
	{
		get { return this; }
	}
}
