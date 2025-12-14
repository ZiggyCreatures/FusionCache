using System.Text;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Locking.Redis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class RedisLockerScenario
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
		Console.Title = "FusionCache - Redis Locker";
		Console.OutputEncoding = Encoding.UTF8;

		var services = new ServiceCollection();

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
			.Enrich.FromLogContext()
			.WriteTo.Console()
			.CreateLogger();

		services.AddLogging(configure => configure.AddSerilog());

		var serviceProvider = services.BuildServiceProvider();

		// CACHE OPTIONS
		var options = new FusionCacheOptions
		{
			CacheKeyPrefix = "MyCachePrefix:",
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = TimeSpan.FromSeconds(10),
				MemoryCacheDuration = TimeSpan.FromSeconds(5),

				IsFailSafeEnabled = true,
				FailSafeMaxDuration = TimeSpan.FromMinutes(10),
				FailSafeThrottleDuration = TimeSpan.FromSeconds(5),

				//EagerRefreshThreshold = 0.8f,
				//FactorySoftTimeout = TimeSpan.FromMilliseconds(100),

				AllowBackgroundDistributedCacheOperations = true,
				AllowBackgroundBackplaneOperations = true,

				JitterMaxDuration = TimeSpan.FromSeconds(2),
			},
		};

		var logger = serviceProvider.GetRequiredService<ILogger<FusionCache>>();

		var cache = new FusionCache(
			options
		//, logger: logger
		);

		var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false");

		// DISTRIBUTED CACHE
		var distributedCache = new RedisCache(new RedisCacheOptions
		{
			ConnectionMultiplexerFactory = async () => redis,
		});
		var serializer = new FusionCacheSystemTextJsonSerializer();
		cache.SetupDistributedCache(distributedCache, serializer);

		// BACKPLANE
		var backplane = new RedisBackplane(new RedisBackplaneOptions
		{
			ConnectionMultiplexerFactory = async () => redis,
		});
		cache.SetupBackplane(backplane);

		// DISTRIBUTED LOCKER
		var distributedLocker = new RedisDistributedLocker(new RedisDistributedLockerOptions
		{
			ConnectionMultiplexerFactory = async () => redis,
		});
		cache.SetupDistributedLocker(distributedLocker);

		while (true)
		{
			var result = await cache.GetOrSetAsync<string>(
				"foo",
				async (ctx, ct) =>
				{
					var delaySec = Random.Shared.Next(1, 5);
					var value = $"{_adjectives[Random.Shared.Next(0, _adjectives.Length)]}-{_nouns[Random.Shared.Next(0, _nouns.Length)]}";

					var color = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Cyan;
					Console.WriteLine($"--> FACTORY: EXECUTING (DELAY = {delaySec} sec)...");
					Console.ForegroundColor = color;

					await Task.Delay(TimeSpan.FromSeconds(delaySec));

					color = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Cyan;
					Console.WriteLine($"--> FACTORY: DONE (VALUE = {value})!");
					Console.ForegroundColor = color;

					return value;
				}
			);

			Console.WriteLine($"RESULT: {result}");

			await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 5)));
		}
	}
}
