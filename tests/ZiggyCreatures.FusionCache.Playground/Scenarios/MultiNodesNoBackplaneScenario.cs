using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class MultiNodesNoBackplaneScenario
{
	private static string[] _adjectives = [
		"shy",
		"cool",
		"chill",
		"fierce",
		"wayward",
		"unlikely",
		"dangerous",
		"aggressive",
		"apocalyptic",
		"longitudinal",
		"compassionate",
	];
	private static string[] _nouns = [
		"cow",
		"taco",
		"sloth",
		"potato",
		"narwhal",
		"cordycep",
	];

	public static async Task RunAsync()
	{
		Console.Title = "FusionCache - MultiNodes No Backplane";
		Console.OutputEncoding = Encoding.UTF8;

		var services = new ServiceCollection();

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Is(Serilog.Events.LogEventLevel.Verbose)
			.Enrich.FromLogContext()
			.WriteTo.Console()
			.CreateLogger();

		services.AddLogging(configure => configure.AddSerilog());

		var serviceProvider = services.BuildServiceProvider();

		var logger = serviceProvider.GetRequiredService<ILogger<FusionCache>>();

		// CONFIG
		var cachesCount = 4;
		var entriesCount = 3;
		var useDistributedCache = true;
		var useBackplane = false;
		//TimeSpan? memoryCacheDuration = null;
		TimeSpan? memoryCacheDuration = TimeSpan.FromSeconds(3);
		//TimeSpan? memoryCacheDuration = useBackplane
		//	? null
		//	: TimeSpan.FromSeconds(3);

		// DISTRIBUTED CACHE
		var distributedCache = new MemoryDistributedCache(Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

		// BACKPLANE
		var backplaneConnectionId = Guid.NewGuid().ToString();

		// SETUP CACHES
		IFusionCache[] caches = new IFusionCache[cachesCount];
		for (int i = 0; i < caches.Length; i++)
		{
			var cache = new FusionCache(
				new FusionCacheOptions
				{
					CacheKeyPrefix = "MyCachePrefix:",
					DefaultEntryOptions = new FusionCacheEntryOptions
					{
						Duration = TimeSpan.FromSeconds(30),
						MemoryCacheDuration = memoryCacheDuration
					},
					TagsDefaultEntryOptions = {
						MemoryCacheDuration = memoryCacheDuration
					}
				}
			//, logger: logger
			);

			// DISTRIBUTED CACHE
			if (useDistributedCache)
			{
				var serializer = new FusionCacheSystemTextJsonSerializer();
				cache.SetupDistributedCache(distributedCache, serializer);
			}

			// BACKPLANE
			if (useBackplane)
			{
				var backplane = new MemoryBackplane(new MemoryBackplaneOptions
				{
					ConnectionId = backplaneConnectionId
				});
				cache.SetupBackplane(backplane);
			}

			caches[i] = cache;
		}




		//// TEMP
		//int foo_2;
		//await caches[0].SetAsync($"foo-1", 1, tags: ["all", "tag-foo"]);
		//await caches[0].SetAsync($"foo-2", 2, tags: ["all", "tag-foo"]);
		//await caches[0].SetAsync($"bar-1", 10, tags: ["all", "tag-bar"]);

		//logger.LogInformation("-- GET");
		//foo_2 = await caches[1].GetOrDefaultAsync<int>("foo-2");

		//logger.LogInformation($"FOO-2: {foo_2}");
		//await Task.Delay(TimeSpan.FromSeconds(2));

		//logger.LogInformation("-- REMOVE BY TAG");
		//await caches[0].RemoveByTagAsync("tag-foo");

		//logger.LogInformation("-- WAIT 5S");
		//await Task.Delay(TimeSpan.FromSeconds(5));

		//logger.LogInformation("-- GET");
		//foo_2 = await caches[1].GetOrDefaultAsync<int>("foo-2");
		//logger.LogInformation($"FOO-2: {foo_2}");

		//logger.LogInformation("-- SET");
		//await caches[1].SetAsync($"foo-2", 2, tags: ["all", "tag-foo"]);

		//logger.LogInformation("-- GET");
		//foo_2 = await caches[1].GetOrDefaultAsync<int>("foo-2");
		//logger.LogInformation($"FOO-2: {foo_2}");

		//logger.LogInformation("-- REMOVE BY TAG");
		//await caches[0].RemoveByTagAsync("tag-foo");

		//logger.LogInformation("-- WAIT 5S");
		//await Task.Delay(TimeSpan.FromSeconds(5));

		//logger.LogInformation("-- GET");
		//foo_2 = await caches[1].GetOrDefaultAsync<int>("foo-2");
		//logger.LogInformation($"FOO-2: {foo_2}");

		//return;




		// DISPLAY
		_ = Task.Run(async () =>
		{
			while (true)
			{
				Console.Clear();

				// FOO
				for (int i = 1; i <= entriesCount; i++)
				{
					Console.WriteLine($"FOO-{i}");
					for (int j = 1; j < caches.Length; j++)
					{
						var v = await caches[j].GetOrDefaultAsync<string>($"foo-{i}");
						Console.WriteLine($"- NODE {j}: {v}");
					}
				}

				Console.WriteLine();

				// BAR
				for (int i = 1; i <= entriesCount; i++)
				{
					Console.WriteLine($"BAR-{i}");
					for (int j = 1; j < caches.Length; j++)
					{
						var v = await caches[j].GetOrDefaultAsync<string>($"bar-{i}");
						Console.WriteLine($"- NODE {j}: {v}");
					}
				}


				await Task.Delay(TimeSpan.FromSeconds(1));
			}
		});

		// INPUTS
		while (true)
		{
			var key = Console.ReadKey(true).KeyChar;

			if (key == 'f')
			{
				await caches[0].RemoveByTagAsync("tag-foo");
			}
			else if (key == 'b')
			{
				await caches[0].RemoveByTagAsync("tag-bar");
			}
			else if (key == 'a')
			{
				await caches[0].RemoveByTagAsync("all");
			}
			else if (int.TryParse(key.ToString(), out var digit))
			{
				if (digit < caches.Length)
				{
					for (int i = 1; i <= entriesCount; i++)
					{
						var v1 = $"{_adjectives[Random.Shared.Next(0, _adjectives.Length)]}-{_nouns[Random.Shared.Next(0, _nouns.Length)]}";
						await caches[digit].SetAsync($"foo-{i}", v1, tags: ["all", "tag-foo"]);

						var v2 = $"{_adjectives[Random.Shared.Next(0, _adjectives.Length)]}-{_nouns[Random.Shared.Next(0, _nouns.Length)]}";
						await caches[digit].SetAsync($"bar-{i}", v2, tags: ["all", "tag-bar"]);
					}
				}
			}
		}
	}
}
