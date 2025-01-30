using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace WebAppTest
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
				.WriteTo.Console()
				.CreateLogger();

			var services = builder.Services;

			services.AddLogging(configure => configure.AddSerilog());

			// PICK A CACHE NAME
			var cacheName = "Bar";

			// MEMORY CACHE (DIRECT REFERENCE, TO SIMULATE A COLD START)
			var memoryCache = new MemoryCache(new MemoryCacheOptions());

			// ADD AND CONFIGURE FUSION CACHE
			services.AddFusionCache(cacheName)
				.WithLogger(sp => sp.GetRequiredService<ILogger<FusionCache>>())
				.WithMemoryCache(memoryCache)
				.WithSerializer(new FusionCacheSystemTextJsonSerializer())
				.WithDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

			// ADD AND CONFIGURE FUSION OUTPUT CACHE
			services.AddFusionOutputCache(options =>
			{
				options.CacheName = cacheName;
			});

			// ADD AND OUTPUT CACHE
			services.AddOutputCache(options =>
			{
				options.AddPolicy("Expire2", builder =>
					builder.Expire(TimeSpan.FromSeconds(2))
				);
				options.AddPolicy("Expire5", builder =>
					builder.Expire(TimeSpan.FromSeconds(5))
				);
				options.AddPolicy("Expire60", builder =>
					builder.Expire(TimeSpan.FromSeconds(60))
				);
			});

			builder.Services.AddControllers();

			var app = builder.Build();

			// Configure the HTTP request pipeline.

			app.UseHttpsRedirection();

			app.UseOutputCache();

			app.UseAuthorization();

			// MVC STYLE
			app.MapControllers();

			// MINIMAL API STYLE
			app
				.MapGet(
					"/minimal/now",
					() => DateTimeOffset.UtcNow
				);

			app
				.MapGet(
					"/minimal/now-cached-2",
					() => DateTimeOffset.UtcNow
				)
				.CacheOutput("Expire2");

			app
				.MapGet(
					"/minimal/now-cached-60",
					() => DateTimeOffset.UtcNow
				)
				.CacheOutput("Expire60");

			app
				.MapGet(
					"/minimal/now-cached-5",
					[OutputCache(PolicyName = "Expire5")] async (context) =>
					{
						await context.Response.WriteAsJsonAsync(DateTimeOffset.UtcNow);
					}
				);

			app
				.MapGet(
					"/clear/l1",
					() => memoryCache.Clear()
				);

			app.Run();
		}
	}
}
