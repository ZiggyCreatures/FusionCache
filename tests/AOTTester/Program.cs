using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;

namespace AOTTester;

internal class Program
{
	static async Task Main(string[] args)
	{
		var duration = TimeSpan.FromSeconds(5);

		var cache = new FusionCache(new FusionCacheOptions
		{
			DefaultEntryOptions = {
				EnableAutoClone = true,
				Duration = duration,
				IsFailSafeEnabled = true,
			}
		});
		cache.SetupSerializer(new FusionCacheCysharpMemoryPackSerializer());

		int value;

		value = await cache.GetOrSetAsync<int>("foo", async (_, _) => Random.Shared.Next(1_000));
		Console.WriteLine($"VALUE: {value}");

		value = await cache.GetOrSetAsync<int>("foo", async (_, _) => Random.Shared.Next(1_000));
		Console.WriteLine($"VALUE: {value}");

		Console.WriteLine("WAITING...");
		await Task.Delay(duration);

		var maybeValue = await cache.TryGetAsync<int>("foo");
		Console.WriteLine($"MAYBE VALUE: {maybeValue}");

		maybeValue = await cache.TryGetAsync<int>("foo", options => options.SetAllowStaleOnReadOnly());
		Console.WriteLine($"MAYBE VALUE (STALE): {maybeValue}");
	}
}
