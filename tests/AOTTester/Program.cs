using ZiggyCreatures.Caching.Fusion;

namespace AOTTester;

internal class Program
{
	static async Task Main(string[] args)
	{
		var cache = new FusionCache(new FusionCacheOptions());

		var value = await cache.GetOrSetAsync<int>("foo", async (ctx, ct) => 42);

		Console.WriteLine($"VALUE: {value}");
	}
}
