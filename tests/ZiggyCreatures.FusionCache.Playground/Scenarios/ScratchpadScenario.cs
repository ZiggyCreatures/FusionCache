using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios
{
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
					Priority = CacheItemPriority.NeverRemove,

					//IsFailSafeEnabled = true,
					//FailSafeMaxDuration = TimeSpan.FromMinutes(10),
					//FailSafeThrottleDuration = TimeSpan.FromSeconds(10),

					//FactorySoftTimeout = TimeSpan.FromMilliseconds(100),

					//AllowBackgroundDistributedCacheOperations = false,
					//AllowBackgroundBackplaneOperations = false
				},
			};

			var cache = new FusionCache(options);

			const string Key = "test key";
			const string Value = "test value";

			cache.Set(Key, Value);

			var foo = cache.TryGet<string?>(Key).GetValueOrDefault(null);
		}
	}
}
