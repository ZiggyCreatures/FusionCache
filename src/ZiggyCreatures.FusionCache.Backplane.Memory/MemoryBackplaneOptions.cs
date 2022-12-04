using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory;

/// <summary>
/// Represents the options available for the memory backplane.
/// </summary>
public class MemoryBackplaneOptions
	: IOptions<MemoryBackplaneOptions>
{
	MemoryBackplaneOptions IOptions<MemoryBackplaneOptions>.Value
	{
		get { return this; }
	}
}
