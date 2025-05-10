using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

public static class ScratchpadScenario
{
	public static async Task RunAsync()
	{
		Console.Title = "FusionCache - Scratchpad";

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

		var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FusionCache>>();

		var cache = new FusionCache(options, logger: logger);

		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var serializer = new FusionCacheNewtonsoftJsonSerializer();
		cache.SetupDistributedCache(distributedCache, serializer);

		var backplane = new MemoryBackplane(new MemoryBackplaneOptions());
		cache.SetupBackplane(backplane);

		const string Key1 = "test-key-1";
		const string Key2 = "test-key-2";
		const string Value = "test value";

		logger.LogInformation("INFO -> INTERNAL STRINGS: {InternalStrings}", string.Join(',', options.InternalStrings.GetAll()));

		await Task.Delay(250);

		logger.LogInformation("----------");

		await cache.SetAsync(Key1, Value);

		logger.LogInformation("----------");

		var foo2 = await cache.GetOrSetAsync<string>(Key2, async (ctx, ct) =>
		{
			logger.LogInformation("INFO -> KEY: {Key} - ORIGINAL KEY: {OriginalKey}", ctx.Key, ctx.OriginalKey);

			return Value;
		});

		logger.LogInformation("----------");

		var bar1 = await cache.TryGetAsync<string>(Key1);

		logger.LogInformation("----------");

		var bar2 = await cache.TryGetAsync<string>(Key2);

		logger.LogInformation("----------");

		await cache.RemoveAsync(Key1);

		logger.LogInformation("----------");

		var baz = await cache.GetOrDefaultAsync<string>(Key1);
	}
}
