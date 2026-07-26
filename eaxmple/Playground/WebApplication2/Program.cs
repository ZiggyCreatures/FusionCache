using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var redisConnectionString = builder.Configuration.GetConnectionString("cache-redis");
var redis = ConnectionMultiplexer.Connect(redisConnectionString ?? "localhost:6379");

builder.Services.AddSingleton(redis);

var serviceBusConnectionString = builder.Configuration.GetConnectionString("cache-servicebus");

builder.Services.AddFusionCache().WithAzureServiceBusBackplane(opt =>
{
	opt.ConnectionString = serviceBusConnectionString;
	opt.TopicName = "fusioncache-playground";
	opt.SubscriptionName = "webApp2-sub";
	opt.IsAdmin = false;
});

builder.Services.AddSingleton<SharedDataService>();

builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new() { Title = "WebApplication2 API", Version = "v1" });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var sharedDataService = app.Services.GetRequiredService<SharedDataService>();

app.MapGet("/data", async () =>
{
    var data = await sharedDataService.GetDataAsync();
    return Results.Ok(new
    {
        AppName = "WebApplication2",
        Timestamp = DateTime.UtcNow,
        Data = data
    });
});

app.MapPost("/data", async (string value) =>
{
    await sharedDataService.SetDataAsync(value);
    return Results.Ok(new
    {
        AppName = "WebApplication2",
        Timestamp = DateTime.UtcNow,
        Message = $"Data updated to: {value}"
    });
});

app.MapGet("/cache/info", () =>
{
    return Results.Ok(new
    {
        AppName = "WebApplication2",
        Message = "FusionCache with Azure Service Bus Backplane enabled",
        CacheKeyPrefix = "app2:",
        DefaultDuration = "30 seconds"
    });
});

app.Run();

class SharedDataService
{
    private const string CacheKey = "shared:data";
    private readonly IFusionCache _cache;
    private readonly ILogger<SharedDataService> _logger;

    public SharedDataService(IFusionCache cache, ILogger<SharedDataService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetDataAsync()
    {
        return await _cache.GetOrSetAsync(
            CacheKey,
            async ct =>
            {
                _logger.LogInformation("[WebApplication2] Cache miss for {CacheKey}, generating data", CacheKey);
                return $"Data from WebApplication2 at {DateTime.UtcNow:O}";
            },
            options => options.SetDuration(TimeSpan.FromSeconds(30))
        );
    }

    public async Task SetDataAsync(string value)
    {
        _logger.LogInformation("[WebApplication2] Setting cache value: {Value}", value);
        await _cache.SetAsync(CacheKey, value, options => options.SetDuration(TimeSpan.FromSeconds(30)));
    }
}
