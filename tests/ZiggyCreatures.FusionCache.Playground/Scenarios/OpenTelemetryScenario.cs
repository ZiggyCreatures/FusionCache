using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios
{
	public class LoggingOpenTelemetryListener : EventListener
	{
		private readonly ILogger<LoggingOpenTelemetryListener> _logger;

		public LoggingOpenTelemetryListener(ILogger<LoggingOpenTelemetryListener> logger)
		{
			_logger = logger;
		}
		protected override void OnEventSourceCreated(EventSource eventSource)
		{
			if (eventSource.Name.StartsWith("OpenTelemetry"))
				EnableEvents(eventSource, EventLevel.Error);
		}

		protected override void OnEventWritten(EventWrittenEventArgs eventData)
		{
			_logger.LogWarning("WARN: Message = {Message}, Payload = {Payload}", eventData.Message, eventData.Payload?.Select(p => p?.ToString())?.ToArray()!);
		}
	}

	public class FusionCacheInstrumentationTracesOptions
	{
	}

	public static class OpenTelemetryScenario
	{
		private static readonly string ServiceName = "FusionCachePlayground.OpenTelemetryScenario";
		private static readonly ActivitySource Source = new ActivitySource("FusionCachePlayground.OpenTelemetryScenario");

		private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan FailSafeMaxDuration = TimeSpan.FromSeconds(30);
		private static readonly TimeSpan FailSafeThrottleDuration = TimeSpan.FromSeconds(3);
		private static readonly TimeSpan FactoryTimeout = TimeSpan.FromSeconds(2);
		private static readonly bool UseFailSafe = true;
		private static readonly bool UseDistributedCache = true;
		private static readonly bool UseBackplane = true;

		private static void SetupOtlp(IServiceCollection services)
		{
			services.AddOpenTelemetry().WithTracing(builder =>
			{
				builder
					//.AddAspNetCoreInstrumentation()
					//.AddHttpClientInstrumentation()
					//.AddSource(nameof(OtlpScenario))
					.AddSource("ZiggyCreatures.Caching.Fusion")
					//.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ZiggyCreatures.Caching.Fusion"))
					.AddConsoleExporter()
				//.AddOtlpExporter(o =>
				//{
				//	o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
				//	o.Endpoint = new Uri($"http://localhost:4317");
				//})
				;
			});
		}

		private static readonly ActivitySource Activity = new ActivitySource(nameof(OpenTelemetryScenario));

		public static async Task RunAsync()
		{
			Console.Title = "FusionCache - Open Telemetry";

			Console.OutputEncoding = Encoding.UTF8;

			// SETUP TRACES
			using var tracerProvider = Sdk.CreateTracerProviderBuilder()
				.ConfigureResource(rb => rb
					.AddService(
						ServiceName,
						serviceVersion: "0.1.0",
						serviceInstanceId: Environment.MachineName
					)
				)
				.AddSource("FusionCachePlayground.OpenTelemetryScenario")

				.AddFusionCacheInstrumentation(options =>
				{
					options.IncludeMemoryLevel = true;
					options.IncludeDistributedLevel = true;
					options.IncludeBackplane = true;
				})

				//.AddOtlpExporter()
				//.AddHoneycomb(new HoneycombOptions
				//{
				//	ServiceName = ServiceName,
				//	ApiKey = "*********"
				//})
				//.AddConsoleExporter()
				.Build();

			// SETUP METRICS
			using var meterProvider = Sdk.CreateMeterProviderBuilder()
				.ConfigureResource(rb => rb
					.AddService(
						ServiceName,
						serviceVersion: "0.1.0",
						serviceInstanceId: Environment.MachineName
					)
				)

				.AddFusionCacheInstrumentation(options =>
				{
					options.IncludeMemoryLevel = true;
					options.IncludeDistributedLevel = true;
					options.IncludeBackplane = true;
				})

				//.AddHoneycomb(new HoneycombOptions
				//{
				//	ServiceName = ServiceName,
				//	ApiKey = "*********"
				//})

				//.AddOtlpExporter(o =>
				//{
				//	o.Endpoint = new Uri("https://api.honeycomb.io:443");
				//	o.Headers = $"x-honeycomb-team="*********,x-honeycomb-dataset=fushioncache-metrics";
				//})

				//.AddOtlpExporter((exporterOptions, metricReaderOptions) =>
				//{
				//	exporterOptions.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
				//	exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
				//	metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
				//})

				//.AddConsoleExporter()
				.Build();

			// DI
			var builder = Host.CreateDefaultBuilder();
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<LoggingOpenTelemetryListener>();
			});

			var app = builder.Build();

			var openTelemetryDebugLogger = app.Services.GetRequiredService<LoggingOpenTelemetryListener>();

			//app.Run();

			var cachesCount = 1;
			var caches = new List<IFusionCache>();
			IFusionCache fusionCache;

			IDistributedCache? distributedCache = null;
			if (UseDistributedCache)
			{
				// MEMORY
				//distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

				// CHAOS + MEMORY
				var chaosDistributedCache = new ChaosDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
				chaosDistributedCache.SetAlwaysDelay(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100));

				distributedCache = chaosDistributedCache;
			}

			for (int i = 0; i < cachesCount; i++)
			{
				var name = $"CACHE-OTLP";

				// CACHE OPTIONS
				var options = new FusionCacheOptions
				{
					CacheName = name,
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

				var cache = new FusionCache(options);

				// DISTRIBUTED CACHE
				if (UseDistributedCache && distributedCache is not null)
				{
					var serializer = new FusionCacheNewtonsoftJsonSerializer();
					Console.WriteLine();
					cache.SetupDistributedCache(distributedCache, serializer);
					Console.WriteLine();
				}

				// BACKPLANE
				if (UseBackplane)
				{
					var backplane = new MemoryBackplane(new MemoryBackplaneOptions());

					Console.WriteLine();
					cache.SetupBackplane(backplane);
					Console.WriteLine();
				}

				caches.Add(cache);
			}

			fusionCache = caches[0];

			using (var activity = Source.StartActivity("Top-Level Action (GetOrDefault)"))
			{
				var tmp0 = await fusionCache.GetOrDefaultAsync<int>("foo", 123);

				Console.WriteLine();
				Console.WriteLine($"pre-initial: {tmp0}");
			}

			using (var activity = Source.StartActivity("Top-Level Action (Set + Eager Refresh)"))
			{
				await fusionCache.SetAsync<int>("foo", 42, options => options.SetDuration(TimeSpan.FromSeconds(4)).SetEagerRefresh(0.1f));
				Console.WriteLine("waiting for eager refresh...");
				await Task.Delay(400);
				await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 42; });
				Console.WriteLine("eager refresh running...");
				await Task.Delay(4_000);
			}

			await Task.Delay(1_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (Set)"))
			{
				await fusionCache.SetAsync<int>("foo", 42, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe, TimeSpan.FromMinutes(1)));
				Console.WriteLine();
				Console.WriteLine($"initial: {fusionCache.GetOrDefault<int>("foo")}");
			}

			await Task.Delay(1_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrSet + Timeout)"))
			{
				var tmp1 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 21; }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp1: {tmp1}");
			}

			await Task.Delay(2_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrDefault)"))
			{
				var tmp2 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp2: {tmp2}");
			}

			await Task.Delay(2_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrSet + Timeout + Fail)"))
			{
				var tmp3 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Sloths are cool"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp3: {tmp3}");
			}

			await Task.Delay(2_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrSet + Timeout)"))
			{
				var tmp4 = await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); return 666; }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000, keepTimedOutFactoryResult: false));
				Console.WriteLine();
				Console.WriteLine($"tmp4: {tmp4}");
			}

			await Task.Delay(2_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrDefault)"))
			{
				var tmp5 = await fusionCache.GetOrDefaultAsync<int>("foo", options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
				Console.WriteLine();
				Console.WriteLine($"tmp5: {tmp5}");
			}

			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (Set)"))
			{
				await fusionCache.SetAsync("foo", 123, fusionCache.CreateEntryOptions(entry => entry.SetDurationSec(1).SetFailSafe(UseFailSafe)));
			}

			await Task.Delay(1_500);
			Console.WriteLine();

			for (int i = 0; i < 10; i++)
			{
				using (var activity = Source.StartActivity("Top-Level Action (GetOrSet + Immediate Fail)"))
				{
					await fusionCache.GetOrSetAsync<int>("foo", _ => { throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
				}

				Console.WriteLine();
			}

			using (var activity = Source.StartActivity("Top-Level Action (Set)"))
			{
				await fusionCache.SetAsync("foo", 123, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe));
			}

			await Task.Delay(1_500);
			Console.WriteLine();

			using (var activity = Source.StartActivity("Top-Level Action (GetOrSet + Timeout + Fail)"))
			{
				await fusionCache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(2_000); throw new Exception("Foo"); }, options => options.SetDurationSec(1).SetFailSafe(UseFailSafe).SetFactoryTimeouts(1_000));
			}

			Console.WriteLine();
			await Task.Delay(2_500);

			Console.WriteLine();
			Console.WriteLine("Press any key to exit...");

			_ = Console.ReadKey();

			Console.WriteLine("\n\nTHE END");
		}
	}
}
