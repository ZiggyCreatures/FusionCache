# FusionCache Redis Backplane - Complete Setup Guide

## Overview

This example demonstrates **FusionCache** with a **Redis backplane** for distributed cache synchronization across multiple applications. Perfect for understanding how FusionCache handles cache invalidation in microservices architectures.

## What You'll Learn

✅ How to configure FusionCache with Redis backplane  
✅ How cache invalidations are broadcast across applications  
✅ How Aspire orchestrates multi-app solutions with Redis  
✅ Real-world patterns for distributed caching  

## Prerequisites

### Required
- **.NET 10.0 SDK** or later ([Download](https://dotnet.microsoft.com/download/dotnet))
- **Docker Desktop** (for Redis container)

### Recommended
- Visual Studio 2022 or VS Code with C# extensions
- REST client (Bruno, Postman, or use REST Client extension in VS Code)

## Quick Start

### 1. Clone/Navigate to Repository
```bash
cd D:\Programming\0.Practice\Contributions\FusionCache-AboubakrFork
cd eaxmple\Playground
```

### 2. Restore and Build
```bash
# Navigate to AppHost directory
cd Playground.AppHost

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build
```

### 3. Run with Aspire
```bash
# From Playground.AppHost directory
dotnet run
```

**What happens next:**
1. Aspire starts and opens a dashboard (usually `http://localhost:15000`)
2. Redis container is pulled and started
3. WebApplication1 launches (typically `https://localhost:7001`)
4. WebApplication2 launches (typically `https://localhost:7002`)

### 4. Verify It's Running
Open the Aspire dashboard and you should see:
- ✅ `cache-redis` - Redis container (green)
- ✅ `webapplication1` - First web app (green)
- ✅ `webapplication2` - Second web app (green)

## Understanding the Setup

### Architecture Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    .NET Aspire Orchestrator                     │
│                    (Playground.AppHost)                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Application Instance 1        Application Instance 2           │
│  (WebApplication1)             (WebApplication2)               │
│  ┌──────────────────────┐      ┌──────────────────────┐        │
│  │ FusionCache          │      │ FusionCache          │        │
│  │ ┌────────────────┐   │      │ ┌────────────────┐   │        │
│  │ │ Memory Cache   │   │      │ │ Memory Cache   │   │        │
│  │ │ (L1)           │   │      │ │ (L1)           │   │        │
│  │ └────────┬───────┘   │      │ └────────┬───────┘   │        │
│  │          │           │      │          │           │        │
│  │ ┌────────▼───────┐   │      │ ┌────────▼───────┐   │        │
│  │ │ Redis Cache    │   │      │ │ Redis Cache    │   │        │
│  │ │ (L2)           │   │      │ │ (L2)           │   │        │
│  │ └────────┬───────┘   │      │ └────────┬───────┘   │        │
│  │          │           │      │          │           │        │
│  │ ┌────────▼──────────────────────────────▼──────┐   │        │
│  │ │    Redis Backplane (Broadcast Channel)      │   │        │
│  │ └──────────────────────────────────────────────┘   │        │
│  │                  ▲                                  │        │
│  │                  │                                  │        │
│  └──────────────────┼──────────────────────────────────┘        │
│                     │                                           │
│            ┌────────▼────────┐                                  │
│            │  Redis Server   │                                  │
│            │  (cache-redis)  │                                  │
│            │                 │                                  │
│            │ - Data Store    │                                  │
│            │ - Pub/Sub       │                                  │
│            │ - Backplane     │                                  │
│            │   Messages      │                                  │
│            └─────────────────┘                                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Two-Level Cache (L1/L2)

**WebApplication1:**
1. Request comes in for "shared:data"
2. L1 (Memory): Check local memory cache first ⚡ (fastest)
3. L1 Miss: Check L2 (Redis) 🚀 (fast)
4. L2 Miss: Execute factory function (slowest, but cached for next request)

**WebApplication2:**
1. Request comes in for "shared:data"
2. L1 (Memory): Check local memory cache first ⚡
3. **Key Point:** L1 entry was removed by backplane invalidation
4. L1 Miss: Check L2 (Redis) 🚀 (has the value from App1!)
5. Use value from Redis ✅

### Redis Backplane (Pub/Sub)

When **WebApplication1 updates** a cache key:

```
1. SetAsync("shared:data", "new value")
   ↓
2. FusionCache stores in local memory
   ↓
3. FusionCache publishes invalidation message to Redis Pub/Sub
   ↓
4. Redis broadcasts: "shared:data was invalidated"
   ↓
5. WebApplication2 receives message
   ↓
6. WebApplication2 removes "shared:data" from its memory cache
   ↓
7. Next request gets FRESH data from Redis (or factory)
```

## Testing the Backplane

### Test 1: Basic Cache Hit

**Terminal 1:**
```bash
curl -k https://localhost:7001/data
# First request - cache miss, generates data
```

**Terminal 2:**
```bash
curl -k https://localhost:7001/data
# Second request - cache hit from memory (same timestamp)
```

**Expected:** Both requests return the same timestamp.

---

### Test 2: Cache Sharing Between Apps

**Terminal 1:**
```bash
curl -k https://localhost:7001/data
# Generates data at 12:00:00Z
```

**Terminal 2:**
```bash
curl -k https://localhost:7002/data
# Should return the SAME data generated by App1
# Timestamp: 12:00:00Z (not a new timestamp!)
```

**Why?** Both apps share the same Redis instance through the backplane.

---

### Test 3: Backplane Invalidation

**Terminal 1:**
```bash
# App1 has cached data
curl -k https://localhost:7001/data
# Returns: "Data from WebApplication1 at 2024-06-20T12:00:00Z"

# Cache is warm, second request hits memory
curl -k https://localhost:7001/data
```

**Terminal 2:**
```bash
# Update from App2
curl -X POST https://localhost:7002/data \
  -H "Content-Type: application/json" \
  -d '"Updated by App2"' \
  -k
# Returns: "Data updated to: Updated by App2"
```

**Terminal 1 (again):**
```bash
# App1's cache was INVALIDATED by the backplane!
curl -k https://localhost:7001/data
# Returns: "Updated by App2" (fresh value from Redis)
```

**What happened:**
1. ✅ App2 called `SetAsync()` with new value
2. ✅ FusionCache stored it in Redis
3. ✅ FusionCache published invalidation to all subscribers
4. ✅ App1 received the invalidation message
5. ✅ App1 removed the key from memory cache
6. ✅ Next request fetches from Redis ✨

---

### Test 4: Cache Duration

**Setup:**
```bash
# Get data from App1
curl -k https://localhost:7001/data
# Returns: "Data from WebApplication1 at 12:00:00Z"
```

**Wait 31 seconds** (cache duration is 30s)

**Check again:**
```bash
# Cache expired in both L1 and L2!
curl -k https://localhost:7001/data
# Returns: NEW timestamp "Data from WebApplication1 at 12:00:31Z"
```

**Note:** The factory function is executed again because both cache levels expired.

## File Structure Explained

### Playground.AppHost/AppHost.cs
```csharp
var redis = builder.AddRedis("cache-redis");

builder
    .AddProject<Projects.WebApplication1>("webapplication1")
    .WithReference(redis);  // App1 gets Redis connection

builder
    .AddProject<Projects.WebApplication2>("webapplication2")
    .WithReference(redis);  // App2 gets Redis connection
```

**Key Points:**
- `AddRedis()` creates a managed Redis container
- `WithReference()` injects the connection string
- Aspire handles service discovery automatically

### WebApplication1/Program.cs & WebApplication2/Program.cs

```csharp
// 1. Connect to Redis
var redis = ConnectionMultiplexer.Connect(redisConnectionString);

// 2. Add FusionCache with lazy memory factory (two-level cache)
builder.Services.AddFusionCache(options =>
{
    options
        .WithDefaultDuration(TimeSpan.FromSeconds(30))
        .WithLazyMemoryFactory();
})
// 3. Add Redis backplane for invalidation broadcasts
.WithBackplane(
    new RedisBackplane(
        new RedisBackplaneOptions
        {
            Connection = redis,
            CacheKeyPrefix = "app1:"  // Unique per app
        }
    )
);
```

**Configuration Explained:**
- **`WithLazyMemoryFactory()`:** Creates memory cache on first use (two-level)
- **`DefaultDuration`:** 30 seconds - then cache expires
- **`RedisBackplane`:** Enables Pub/Sub for invalidation broadcasts
- **`CacheKeyPrefix`:** Prevents key conflicts between apps

### SharedDataService Class

```csharp
class SharedDataService
{
    // Both apps use THE SAME cache key: "shared:data"
    private const string CacheKey = "shared:data";

    public async Task<string> GetDataAsync()
    {
        // GetOrSetAsync: 
        // - Returns cached value if present
        // - Executes factory and caches result if missing
        // - Other apps are notified of L1 invalidation
        return await _cache.GetOrSetAsync(
            CacheKey,
            async ct => $"Data from {AppName} at {DateTime.UtcNow:O}",
            options => options.SetDuration(TimeSpan.FromSeconds(30))
        );
    }

    public async Task SetDataAsync(string value)
    {
        // SetAsync:
        // - Sets value in memory and Redis
        // - Broadcasts invalidation through backplane
        // - Other apps remove the key from memory
        await _cache.SetAsync(CacheKey, value, 
            options => options.SetDuration(TimeSpan.FromSeconds(30))
        );
    }
}
```

## Advanced Concepts

### Cache Key Prefix Strategy

```
WebApplication1: app1:shared:data
WebApplication2: app2:shared:data
```

Both apps can have their own cache entries, but they both also subscribe to `shared:data` changes through the backplane.

### When Backplane Invalidation Happens

✅ **Happens:**
- `SetAsync()` - Set a value
- `RemoveAsync()` - Remove a key
- `ExpireAsync()` - Expire a key
- `ClearAsync()` - Clear all

❌ **Doesn't Happen:**
- `GetOrSetAsync()` - Only on factory execution failure
- Direct memory cache hits
- Cache expiration (local to each app)

### Performance Implications

```
Cache Scenario           | Speed   | Network Calls
─────────────────────────┼─────────┼──────────────
L1 Hit (Memory)          | ⚡⚡⚡   | None
L2 Hit (Redis)           | ⚡⚡    | 1
Factory Execution        | ⚡     | 1 (to store)
Cross-App Invalidation   | ⚡     | 1 (backplane message)
```

## Troubleshooting

### Problem: Redis Connection Failed
**Error:** `Timeout connecting to cache-redis:6379`

**Solution:**
1. Ensure Docker Desktop is running
2. Check Aspire dashboard - is Redis green?
3. Manually test: `docker ps | grep redis`

### Problem: Apps Not Synchronizing
**Error:** App2 doesn't see the data from App1

**Solution:**
1. Check both apps have the same `CacheKey` value
2. Verify Redis backplane is initialized in both
3. Check logs: should see "Cache miss" on first access
4. Wait - Redis Pub/Sub is fast but not instant (~10ms)

### Problem: Cache Not Expiring
**Symptom:** Data doesn't change even after 30 seconds

**Solution:**
1. Check the duration setting: `WithDefaultDuration(TimeSpan.FromSeconds(30))`
2. Manually clear: Call the endpoint twice rapidly
3. Check logs for expiration messages

### Problem: Service Discovery Issues
**Error:** `System.Net.Http.HttpRequestException: No such host is known`

**Solution:**
1. Ensure you're using the service name from Aspire: `cache-redis`
2. Check `appsettings.json` for correct connection string format
3. Aspire's service discovery converts `cache-redis` → `localhost:6379`

## Running Without Aspire (Advanced)

If you need to run without Aspire, update the connection string:

```csharp
// Replace this:
var redisConnectionString = builder.Configuration.GetConnectionString("cache-redis");

// With this:
var redisConnectionString = "localhost:6379";

// Make sure Redis is running on localhost:6379
```

## Common Patterns

### Pattern 1: Write-Through Cache
```csharp
public async Task UpdateUserAsync(int userId, UserData userData)
{
    // Update database
    await _db.Users.Update(userData);
    
    // Update cache (triggers backplane invalidation)
    await _cache.SetAsync($"user:{userId}", userData);
}
```

### Pattern 2: Cache-Aside
```csharp
public async Task<UserData> GetUserAsync(int userId)
{
    return await _cache.GetOrSetAsync(
        $"user:{userId}",
        async ct => await _db.Users.GetAsync(userId)
    );
}
```

### Pattern 3: Distributed Cache Warming
```csharp
public async Task PrecacheAsync()
{
    foreach (var key in _importantKeys)
    {
        await _cache.SetAsync(key, await _generateValue(key));
    }
    // All apps now have warmed caches thanks to backplane
}
```

## Next Steps

1. ✅ Run the example
2. ✅ Test cache hit/miss patterns
3. ✅ Observe backplane invalidations
4. ✅ Modify the factory function to see how it works
5. ✅ Add more cache keys
6. ✅ Add error handling and logging

## Resources

- **FusionCache GitHub:** https://github.com/ZiggyCreatures/FusionCache
- **Redis Backplane Docs:** https://github.com/ZiggyCreatures/FusionCache/wiki/Backplane
- **.NET Aspire:** https://learn.microsoft.com/en-us/dotnet/aspire/
- **StackExchange.Redis:** https://github.com/StackExchange/StackExchange.Redis

## Questions?

Check the logs! Enable Debug logging to see exactly what's happening:

```json
{
  "Logging": {
    "LogLevel": {
      "ZiggyCreatures.Caching.Fusion": "Debug",
      "StackExchange.Redis": "Information"
    }
  }
}
```

---

Happy caching! 🚀
