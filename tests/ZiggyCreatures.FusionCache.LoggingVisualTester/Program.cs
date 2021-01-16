using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.LoggingVisualTester
{

	public static class ExtMethods
	{
		public static FusionCacheEntryOptions SetFactoryTimeouts(this FusionCacheEntryOptions options, int? softTimeoutMs = null, int? hardTimeoutMs = null, bool? keepTimedOutFactoryResult = null)
		{
			if (softTimeoutMs is object)
				options.FactorySoftTimeout = TimeSpan.FromMilliseconds(softTimeoutMs.Value);
			if (hardTimeoutMs is object)
				options.FactoryHardTimeout = TimeSpan.FromMilliseconds(hardTimeoutMs.Value);
			if (keepTimedOutFactoryResult is object)
				options.AllowTimedOutFactoryBackgroundCompletion = keepTimedOutFactoryResult.Value;
			return options;
		}
	}

	class Program
	{
		static void SetupSerilogLogger(IServiceCollection services, LogEventLevel minLevel = LogEventLevel.Verbose)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(minLevel)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.CreateLogger()
			;

			services.AddLogging(configure => configure.AddSerilog());
		}

		static void SetupStandardLogger(IServiceCollection services, LogLevel minLevel = LogLevel.Trace)
		{
			services.AddLogging(configure => configure.SetMinimumLevel(minLevel).AddConsole(options => options.IncludeScopes = true));
		}

		async static Task Main(string[] args)
		{
			var cacheDurationSec = 5;
			var failSafeMaxDurationSec = 30;
			var failSafeThrottleDurationSec = 3;
			var factoryTimeoutMs = 2_000;
			var useFailSafe = true;
			var useDistributedCache = true;

			Console.OutputEncoding = Encoding.UTF8;

			// DI
			var services = new ServiceCollection();

			SetupSerilogLogger(services);
			//SetupStandardLogger(services);

			var serviceProvider = services.BuildServiceProvider();

			var logger = serviceProvider.GetService<ILogger<FusionCache>>();

			// CACHE OPTIONS
			var options = new FusionCacheOptions
			{
				CacheKeyPrefix = "dev:",
				DefaultEntryOptions = new FusionCacheEntryOptions
				{
					Duration = TimeSpan.FromSeconds(cacheDurationSec),
					Priority = CacheItemPriority.NeverRemove,

					IsFailSafeEnabled = useFailSafe,
					FailSafeMaxDuration = TimeSpan.FromSeconds(failSafeMaxDurationSec),
					FailSafeThrottleDuration = TimeSpan.FromSeconds(failSafeThrottleDurationSec),

					FactorySoftTimeout = TimeSpan.FromMilliseconds(factoryTimeoutMs),

					AllowBackgroundDistributedCacheOperations = true
				},
			};
			using (var fusionCache = new FusionCache(options, logger: logger))
			{
				if (useDistributedCache)
				{
					// DISTRIBUTED CACHE
					var serializer = new FusionCacheNewtonsoftJsonSerializer();
					var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

					Console.WriteLine();
					fusionCache.SetupDistributedCache(distributedCache, serializer);
					Console.WriteLine();
				}

				var tmp0 = await fusionCache.GetOrDefaultAsync<int>("foo", 123);
				Console.WriteLine();
				Console.WriteLine($"pre-initial: {tmp0}");

				await fusionCache.SetAsync<int>("foo", 42, options => options.SetDurationSec(1).SetFailSafe(useFailSafe, TimeSpan.FromMinutes(1)));
				Console.WriteLine();
				Console.WriteLine($"initial: {fusionCache.GetOrDefault<int>("foo")}");
				await Task.Delay(1_500);
				Console.WriteLine();

				var tmp1 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 21; }, options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp1: {tmp1}");
				await Task.Delay(2_500);
				Console.WriteLine();

				var tmp2 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp2: {tmp2}");
				await Task.Delay(2_500);
				Console.WriteLine();

				var tmp3 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Sloths are cool"); }, options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp3: {tmp3}");
				await Task.Delay(2_500);
				Console.WriteLine();

				var tmp4 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 666; }, options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000, keepTimedOutFactoryResult: false));
				Console.WriteLine();
				Console.WriteLine($"tmp4: {tmp4}");
				await Task.Delay(2_500);
				Console.WriteLine();

				var tmp5 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp5: {tmp5}");
				Console.WriteLine();

				await fusionCache.SetAsync("foo", 123, fusionCache.CreateEntryOptions(entry => entry.SetDurationSec(1).SetFailSafe(useFailSafe)));
				await Task.Delay(1_500);
				Console.WriteLine();

				await fusionCache.GetOrSetAsync<int>("foo", _ => { throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();

				await fusionCache.SetAsync("foo", 123, options => options.SetDurationSec(1).SetFailSafe(useFailSafe));
				await Task.Delay(1_500);
				Console.WriteLine();

				await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(useFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				await Task.Delay(2_500);

				Console.WriteLine("THE END");
			}
		}
	}
}
