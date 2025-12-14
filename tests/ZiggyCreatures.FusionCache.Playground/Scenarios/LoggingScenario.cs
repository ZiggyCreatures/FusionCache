using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class VisualTesterExtMethods
{
	public static FusionCacheEntryOptions SetFactoryTimeouts(this FusionCacheEntryOptions options, int? softTimeoutMs = null, int? hardTimeoutMs = null, bool? keepTimedOutFactoryResult = null)
	{
		if (softTimeoutMs is not null)
			options.FactorySoftTimeout = TimeSpan.FromMilliseconds(softTimeoutMs.Value);
		if (hardTimeoutMs is not null)
			options.FactoryHardTimeout = TimeSpan.FromMilliseconds(hardTimeoutMs.Value);
		if (keepTimedOutFactoryResult is not null)
			options.AllowTimedOutFactoryBackgroundCompletion = keepTimedOutFactoryResult.Value;
		return options;
	}
}

public static class LoggingScenario
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan FailSafeMaxDuration = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan FailSafeThrottleDuration = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan FactoryTimeout = TimeSpan.FromSeconds(2);
	private static readonly bool UseFailSafe = true;
	private static readonly bool UseDistributedCache = false;
	private static readonly bool UseBackplane = false;
	private static readonly bool UseLogger = true;
	//private static readonly string? RedisConnection = null;
	private static readonly string? RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,connectTimeout=1000,syncTimeout=500";

	private static void SetupSerilogLogger(IServiceCollection services, LogEventLevel minLevel = LogEventLevel.Verbose)
	{
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Is(minLevel)
			.Enrich.FromLogContext()
			.WriteTo.Console(
				outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}"
			)
			.CreateLogger()
		;

		services.AddLogging(configure => configure.AddSerilog());
	}

	private static void SetupStandardLogger(IServiceCollection services, LogLevel minLevel = LogLevel.Trace)
	{
		services.AddLogging(configure => configure.SetMinimumLevel(minLevel).AddSimpleConsole(options => options.IncludeScopes = true));
	}

	public static async Task RunAsync()
	{
		Console.Title = "FusionCache - Logging";
		Console.OutputEncoding = Encoding.UTF8;

		// DI
		var services = new ServiceCollection();

		SetupSerilogLogger(services);
		//SetupStandardLogger(services);

		var serviceProvider = services.BuildServiceProvider();

		var logger = UseLogger ? serviceProvider.GetService<ILogger<FusionCache>>() : null;

		// CACHE OPTIONS
		var options = new FusionCacheOptions
		{
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = CacheDuration,
				Priority = CacheItemPriority.NeverRemove,

				IsFailSafeEnabled = UseFailSafe,
				FailSafeMaxDuration = FailSafeMaxDuration,
				FailSafeThrottleDuration = FailSafeThrottleDuration,

				FactorySoftTimeout = FactoryTimeout,

				AllowBackgroundDistributedCacheOperations = false,
				AllowBackgroundBackplaneOperations = false
			},
		};

		var cachesCount = 1;
		var caches = new List<IFusionCache>();
		IFusionCache fusionCache;

		for (int i = 0; i < cachesCount; i++)
		{
			fusionCache = new FusionCache(options, logger: logger);

			// DISTRIBUTED CACHE
			if (UseDistributedCache)
			{
				var serializer = new FusionCacheNewtonsoftJsonSerializer();
				IDistributedCache distributedCache;
				if (string.IsNullOrWhiteSpace(RedisConnection) == false)
				{
					distributedCache = new RedisCache(Options.Create(new RedisCacheOptions()
					{
						Configuration = RedisConnection
					}));
				}
				else
				{
					distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
				}

				Console.WriteLine();
				fusionCache.SetupDistributedCache(distributedCache, serializer);
				Console.WriteLine();
			}

			// BACKPLANE
			if (UseBackplane)
			{
				IFusionCacheBackplane backplane;
				if (string.IsNullOrWhiteSpace(RedisConnection) == false)
				{
					backplane = new RedisBackplane(new RedisBackplaneOptions()
					{
						Configuration = RedisConnection,
					});
				}
				else
				{
					backplane = new MemoryBackplane(new MemoryBackplaneOptions());
				}

				Console.WriteLine();
				fusionCache.SetupBackplane(backplane);
				Console.WriteLine();
			}

			caches.Add(fusionCache);
		}

		fusionCache = caches[0];

		var tmp0 = await fusionCache.GetOrDefaultAsync<int>("foo", 123);
		Console.WriteLine();
		Console.WriteLine($"pre-initial: {tmp0}");

		await fusionCache.SetAsync<int>("foo", 42, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe, TimeSpan.FromMinutes(1)));
		Console.WriteLine();
		Console.WriteLine($"initial: {fusionCache.GetOrDefault<int>("foo")}");
		await Task.Delay(1_500);
		Console.WriteLine();

		var tmp1 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 21; }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();
		Console.WriteLine($"tmp1: {tmp1}");
		await Task.Delay(2_500);
		Console.WriteLine();

		var tmp2 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();
		Console.WriteLine($"tmp2: {tmp2}");
		await Task.Delay(2_500);
		Console.WriteLine();

		var tmp3 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Sloths are cool"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();
		Console.WriteLine($"tmp3: {tmp3}");
		await Task.Delay(2_500);
		Console.WriteLine();

		var tmp4 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 666; }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000, keepTimedOutFactoryResult: false));
		Console.WriteLine();
		Console.WriteLine($"tmp4: {tmp4}");
		await Task.Delay(2_500);
		Console.WriteLine();

		var tmp5 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();
		Console.WriteLine($"tmp5: {tmp5}");
		Console.WriteLine();

		await fusionCache.SetAsync("foo", 123, fusionCache.CreateEntryOptions(entry => entry.SetDurationSec(1).SetFailSafe(UseFailSafe)));
		await Task.Delay(1_500);
		Console.WriteLine();

		await fusionCache.GetOrSetAsync<int>("foo", _ => { throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();

		await fusionCache.SetAsync("foo", 123, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe));
		await Task.Delay(1_500);
		Console.WriteLine();

		await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
		Console.WriteLine();
		await Task.Delay(2_500);

		Console.WriteLine();
		Console.WriteLine("Press any key to exit...");

		_ = Console.ReadKey();

		Console.WriteLine("\n\nTHE END");
	}
}
