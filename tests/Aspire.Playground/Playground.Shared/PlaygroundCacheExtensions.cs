using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Playground.Shared;

public sealed record PlaygroundCacheOptions(
    string AppName,
    string ServiceBusSubscriptionName,
    string CacheKeyPrefix,
    TimeSpan Duration
);

public static class PlaygroundCacheExtensions
{
    private const string TopicName = "fusioncache-playground";

    public static IServiceCollection AddPlaygroundCache(
        this IServiceCollection services,
        PlaygroundCacheOptions options,
        string? redisConnectionString,
        string? serviceBusConnectionString)
    {
        services
            .AddFusionCache()
            .WithDistributedCache(new RedisCache(new RedisCacheOptions
            {
                Configuration = redisConnectionString ?? "localhost:6379"
            }))
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithAzureServiceBusBackplane(backplaneOptions =>
            {
                backplaneOptions.ConnectionString = serviceBusConnectionString;
                backplaneOptions.TopicName = TopicName;
                backplaneOptions.SubscriptionName = options.ServiceBusSubscriptionName;
                backplaneOptions.IsAdmin = false;
            });

        services.AddSingleton(options);
        services.AddSingleton<SharedDataService>();

        return services;
    }

    public static WebApplication MapPlaygroundCacheEndpoints(this WebApplication app)
    {
        app.MapGet("/data", async (SharedDataService sharedDataService, PlaygroundCacheOptions options) =>
        {
            var data = await sharedDataService.GetDataAsync();
            return Results.Ok(new
            {
                options.AppName,
                Timestamp = DateTime.UtcNow,
                Data = !data.HasValue ? "nothing ": data.Value
            });
        });

        app.MapPost("/data", async (string value, SharedDataService sharedDataService, PlaygroundCacheOptions options) =>
        {
            await sharedDataService.SetDataAsync(value);
            return Results.Ok(new
            {
                options.AppName,
                Timestamp = DateTime.UtcNow,
                Message = $"Data updated to: {value}"
            });
        });

        app.MapGet("/cache/info", (PlaygroundCacheOptions options) => Results.Ok(new
        {
            options.AppName,
            Message = "FusionCache with Redis L2 and Azure Service Bus Backplane enabled",
            options.CacheKeyPrefix,
            DefaultDuration = options.Duration.ToString()
        }));

        return app;
    }
}

public sealed class SharedDataService
{
    private const string CacheKey = "shared:data";
    private readonly IFusionCache _cache;
    private readonly ILogger<SharedDataService> _logger;
    private readonly PlaygroundCacheOptions _options;

    public SharedDataService(
        IFusionCache cache,
        ILogger<SharedDataService> logger,
        PlaygroundCacheOptions options)
    {
        _cache = cache;
        _logger = logger;
        _options = options;
    }

    public ValueTask<MaybeValue<string>> GetDataAsync()
    {
        return _cache.TryGetAsync<string>(
            CacheKey
        );
    }

    public ValueTask SetDataAsync(string value)
    {
        _logger.LogInformation("[{AppName}] Setting cache value: {Value}", _options.AppName, value);
        return _cache.SetAsync(CacheKey, value, entryOptions => entryOptions.SetDuration(_options.Duration));
    }
}
