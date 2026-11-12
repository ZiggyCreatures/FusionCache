# FusionCache Redis Backplane Example

This example demonstrates how to use **FusionCache** with a **Redis backplane** to synchronize cached data across multiple applications running on **.NET Aspire**.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│                      .NET Aspire Orchestrator                  │
│                                                                 │
│  ┌────────────────────┐         ┌────────────────────┐         │
│  │  WebApplication1   │         │  WebApplication2   │         │
│  │  (FusionCache)     │         │  (FusionCache)     │         │
│  │  Cache Key Prefix: │◄───────►│  Cache Key Prefix: │         │
│  │      app1:         │ Backplane│      app2:         │         │
│  └────────────────────┘         └────────────────────┘         │
│           ▲                                 ▲                   │
│           │                                 │                   │
│           └────────────┬────────────────────┘                   │
│                        │ Redis Protocol                         │
│                  ┌─────▼──────┐                                 │
│                  │    Redis   │                                 │
│                  │  Backplane │                                 │
│                  │  (cache-   │                                 │
│                  │   redis)   │                                 │
│                  └────────────┘                                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## How It Works

### 1. **FusionCache Setup**
- Each application configures FusionCache with a **lazy memory factory** (two-level cache)
- The **Redis backplane** intercepts cache invalidations and broadcasts them across all connected instances
- Each app has its own cache key prefix (`app1:` and `app2:`) but shares the `shared:data` key

### 2. **Data Synchronization**
- When **WebApplication1** updates the cache, Redis broadcasts an invalidation message
- **WebApplication2** receives the message and removes the value from its local memory cache
- Next access on **WebApplication2** fetches the fresh value from Redis or regenerates it

### 3. **Aspire Integration**
- `Playground.AppHost` orchestrates the entire solution
- Redis runs as a containerized service managed by Aspire
- Both web applications receive the Redis connection string via service discovery

## Project Structure

```
eaxmple/Playground/
├── Playground.AppHost/
│   ├── AppHost.cs                          # Aspire configuration
│   └── Playground.AppHost.csproj           # References both web apps
│
├── Playground.ServiceDefaults/
│   └── Extensions.cs                       # Shared service configuration
│
├── WebApplication1/
│   ├── Program.cs                          # FusionCache + Redis setup
│   └── WebApplication1.csproj              # References FusionCache & Redis packages
│
├── WebApplication2/
│   ├── Program.cs                          # FusionCache + Redis setup
│   └── WebApplication2.csproj              # References FusionCache & Redis packages
│
└── README.md                                # This file
```

## Running the Example

### Prerequisites
- .NET 10.0 SDK
- Docker (required for Redis in Aspire)

### Start the Solution

```bash
cd eaxmple/Playground/Playground.AppHost
dotnet run
```

This will:
1. Start the Aspire orchestrator dashboard (usually at `http://localhost:15000`)
2. Spin up Redis container
3. Launch WebApplication1 (usually at `https://localhost:7001`)
4. Launch WebApplication2 (usually at `https://localhost:7002`)

## API Endpoints

### WebApplication1

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/data` | GET | Retrieve cached data |
| `/data` | POST | Update cached data (triggers backplane invalidation) |
| `/cache/info` | GET | Show cache configuration |

### WebApplication2

Same endpoints as WebApplication1, running on a different port.

## Example Scenario

### Step 1: Check Cache Info
```bash
# Terminal 1: Check App1 cache configuration
curl https://localhost:7001/cache/info -k

# Output:
# {
#   "appName": "WebApplication1",
#   "message": "FusionCache with Redis Backplane enabled",
#   "cacheKeyPrefix": "app1:",
#   "defaultDuration": "30 seconds"
# }
```

### Step 2: Get Data from App1
```bash
curl https://localhost:7001/data -k

# Output:
# {
#   "appName": "WebApplication1",
#   "timestamp": "2024-06-20T12:00:00Z",
#   "data": "Data from WebApplication1 at 2024-06-20T12:00:00Z"
# }
```

### Step 3: Get Data from App2 (Same Data!)
```bash
curl https://localhost:7002/data -k

# Output:
# {
#   "appName": "WebApplication2",
#   "timestamp": "2024-06-20T12:00:00Z",
#   "data": "Data from WebApplication1 at 2024-06-20T12:00:00Z"
# }
```

Notice how **WebApplication2 returns the same data** that was generated by **WebApplication1**. The backplane has synchronized the cache across both instances.

### Step 4: Update Data from App2
```bash
curl -X POST "https://localhost:7002/data" \
  -H "Content-Type: application/json" \
  -d '"Updated value from App2"' \
  -k

# Output:
# {
#   "appName": "WebApplication2",
#   "timestamp": "2024-06-20T12:01:00Z",
#   "message": "Data updated to: Updated value from App2"
# }
```

### Step 5: Verify Synchronization in App1
```bash
curl https://localhost:7001/data -k

# Output:
# {
#   "appName": "WebApplication1",
#   "timestamp": "2024-06-20T12:01:30Z",
#   "data": "Updated value from App2"
# }
```

The **backplane invalidated the cache in App1**, causing it to fetch the fresh value from Redis!

## Key Configuration Details

### AppHost.cs
```csharp
var redis = builder.AddRedis("cache-redis");
builder.AddProject<Projects.WebApplication1>("webapplication1").WithReference(redis);
builder.AddProject<Projects.WebApplication2>("webapplication2").WithReference(redis);
```
- Adds Redis as a managed service
- Both apps reference the same Redis instance

### Program.cs (Both Apps)
```csharp
builder.Services.AddFusionCache(options =>
{
    options
        .WithDefaultDuration(TimeSpan.FromSeconds(30))
        .WithLazyMemoryFactory();
})
.WithBackplane(
    new RedisBackplane(
        new RedisBackplaneOptions
        {
            Connection = redis,
            CacheKeyPrefix = "app1:" // or "app2:" for App2
        }
    )
);
```
- **Lazy Memory Factory**: Two-level cache (memory + Redis)
- **Redis Backplane**: Handles cross-app invalidation

### SharedDataService Class
```csharp
class SharedDataService
{
    public async Task<string> GetDataAsync()
    {
        return await _cache.GetOrSetAsync("shared:data", ...);
    }

    public async Task SetDataAsync(string value)
    {
        await _cache.SetAsync("shared:data", ...);
    }
}
```
- Both applications use the **same cache key** (`shared:data`)
- `GetOrSetAsync` + backplane = distributed caching pattern
- Updates trigger automatic invalidation broadcasts

## Benefits of This Setup

1. **Reduced Database Load**: Cache hits are served from memory or Redis
2. **Data Consistency**: Backplane invalidations ensure all instances see fresh data
3. **Horizontal Scaling**: Add more application instances - they all stay synchronized
4. **Simple Configuration**: Aspire handles Redis lifecycle; apps just reference it
5. **No Cache Staleness**: When one app updates data, others are notified immediately

## Monitoring

You can watch cache activity in the application logs:

```
[WebApplication1] Cache miss for shared:data, generating data
[WebApplication2] Setting cache value: Updated value from App2
[WebApplication2] Cache miss for shared:data, generating data
```

The logs show exactly when cache hits/misses occur and when backplane synchronization happens.

## Troubleshooting

### Redis Connection Issues
- Ensure Docker is running
- Check that the connection string matches: `<service-name>:<port>` (Aspire resolves this)

### Cache Not Synchronizing
- Verify both apps are using the same `RedisBackplane` instance
- Check Redis is accessible: `redis-cli PING` or use Aspire dashboard

### Stale Data
- Adjust `WithDefaultDuration()` if cached data is too stale
- Set it to `0` to disable caching and always fetch fresh data (for debugging)

## Further Reading

- [FusionCache Documentation](https://github.com/ZiggyCreatures/FusionCache)
- [Redis Backplane Details](https://github.com/ZiggyCreatures/FusionCache/tree/main/src/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis)
- [.NET Aspire Docs](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview)
