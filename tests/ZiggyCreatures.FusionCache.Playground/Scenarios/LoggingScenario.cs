using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios
{
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

		private static void SetupSerilogLogger(IServiceCollection services, LogEventLevel minLevel = LogEventLevel.Verbose)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(minLevel)
				.Enrich.FromLogContext()
				.WriteTo.Console()
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
			using (var fusionCache = new FusionCache(options, logger: logger))
			{
				if (UseDistributedCache)
				{
					// DISTRIBUTED CACHE
					var serializer = new FusionCacheNewtonsoftJsonSerializer();
					var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

					Console.WriteLine();
					fusionCache.SetupDistributedCache(distributedCache, serializer);
					Console.WriteLine();
				}

				if (UseBackplane)
				{
					// BACKPLANE
					var backplane = new MemoryBackplane(new MemoryBackplaneOptions());

					Console.WriteLine();
					fusionCache.SetupBackplane(backplane);
					Console.WriteLine();
				}

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

				Console.WriteLine("\n\nTHE END");
			}
		}
	}
}
