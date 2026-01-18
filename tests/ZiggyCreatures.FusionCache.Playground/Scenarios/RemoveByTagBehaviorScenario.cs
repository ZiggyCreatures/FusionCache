using System.Text;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class RemoveByTagBehaviorScenario
{
	public static async Task RunAsync()
	{
		Console.Title = "FusionCache - RemoveByTagBehavior";
		Console.OutputEncoding = Encoding.UTF8;

		var builder = Host.CreateDefaultBuilder();

		builder.ConfigureServices(services =>
		{
			services.AddLogging(logging =>
			{
				logging.ClearProviders();
			});
			services.AddFusionCache()
				.WithOptions(options =>
				{
					options.RemoveByTagBehavior = RemoveByTagBehavior.Remove;
				})
				.WithSerializer(new FusionCacheSystemTextJsonSerializer())
				.WithDistributedCache(new RedisCache(Options.Create(new RedisCacheOptions
				{
					Configuration = "localhost:6379"
				})));
		});
		var host = builder.Build();
		var cache = host.Services.GetRequiredService<IFusionCache>();

		for (int i = 0; i < 10; i++)
		{
			await cache.SetAsync($"foo-{i}", $"VALUE-{i}", tags: ["tag-x", "tag-y", "tag-z"]);
		}

		Console.WriteLine("VALUES (NO STALE)");
		for (int i = 0; i < 10; i++)
		{
			var s = await cache.GetOrDefaultAsync<string>($"foo-{i}");

			Console.WriteLine($"- VALUE {i}: {s}");
		}

		await cache.RemoveByTagAsync("tag-x");

		Console.WriteLine();
		Console.WriteLine("VALUES (NO STALE)");
		for (int i = 0; i < 10; i++)
		{
			var s = await cache.GetOrDefaultAsync<string>($"foo-{i}");

			Console.WriteLine($"- VALUE {i}: {s}");
		}

		Console.WriteLine();
		Console.WriteLine("VALUES (ALLOW STALE)");
		for (int i = 0; i < 10; i++)
		{
			var s = await cache.GetOrDefaultAsync<string>($"foo-{i}", options => options.SetAllowStaleOnReadOnly());

			Console.WriteLine($"- VALUE {i}: {s}");
		}
	}
}
