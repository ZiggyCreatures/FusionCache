using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class ScratchpadScenario
{
	public static async Task RunAsync()
	{
		Console.Title = "FusionCache - Scratchpad";

		Console.OutputEncoding = Encoding.UTF8;

		// CACHE OPTIONS
		var options = new FusionCacheOptions
		{
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = TimeSpan.FromMinutes(1),
				//Priority = CacheItemPriority.NeverRemove,

				//IsFailSafeEnabled = true,
				//FailSafeMaxDuration = TimeSpan.FromMinutes(10),
				//FailSafeThrottleDuration = TimeSpan.FromSeconds(10),

				//FactorySoftTimeout = TimeSpan.FromMilliseconds(100),

				//AllowBackgroundDistributedCacheOperations = false,
				//AllowBackgroundBackplaneOperations = false
			},
		};

		var cache = new FusionCache(options);
		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var serializer = new FusionCacheNewtonsoftJsonSerializer();
		cache.SetupDistributedCache(distributedCache, serializer);

		const string Key = "test key";
		const string Value = "test value";

		//cache.Set(Key, Value);

		//var foo = cache.TryGet<string?>(Key).GetValueOrDefault(null);

		var foo = cache.GetOrSet(Key, _ => Value);

		cache.Remove(Key);
	}
}
