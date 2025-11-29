using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;

namespace AOTTester;

internal class Program
{
	static async Task Main(string[] args)
	{
		var cache = new FusionCache(new FusionCacheOptions
		{
			DefaultEntryOptions = {
				EnableAutoClone=true
			}
		});
		cache.SetupSerializer(new FusionCacheCysharpMemoryPackSerializer());

		var value = await cache.GetOrSetAsync<int>("foo", async (ctx, ct) => 42);

		Console.WriteLine($"VALUE: {value}");
	}
}
