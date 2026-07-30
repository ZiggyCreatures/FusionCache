# FusionCache Redis Backplane Example - Changes Summary

## 📋 Overview
This document summarizes all changes made to create a complete, working example of two applications synchronizing cache data through a Redis backplane using .NET Aspire.

## 🔄 Files Modified

### 1. **Playground.AppHost/AppHost.cs**
**Purpose:** Aspire orchestration configuration

**Changes:**
- Added Redis service: `builder.AddRedis("cache-redis")`
- Both web applications now reference the Redis instance via `.WithReference(redis)`
- This ensures both apps connect to the same Redis server

```csharp
var redis = builder.AddRedis("cache-redis");

builder
    .AddProject<Projects.WebApplication1>("webapplication1")
    .WithReference(redis);

builder
    .AddProject<Projects.WebApplication2>("webapplication2")
    .WithReference(redis);
```

---

### 2. **WebApplication1/WebApplication1.csproj**
**Purpose:** Project dependencies for App1

**Changes:**
- Added project reference to FusionCache core
- Added project reference to FusionCache Redis Backplane
- Added NuGet package: `StackExchange.Redis` v2.8.7

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\src\ZiggyCreatures.FusionCache\ZiggyCreatures.FusionCache.csproj" />
  <ProjectReference Include="..\..\..\src\ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis\ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis.csproj" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="StackExchange.Redis" Version="2.8.7" />
</ItemGroup>
```

---

### 3. **WebApplication1/Program.cs**
**Purpose:** Application startup and API endpoints

**Key Additions:**
- Redis connection initialization
- FusionCache registration with lazy memory factory (two-level cache)
- Redis backplane configuration
- `SharedDataService` class for cache operations
- Three API endpoints:
  - `GET /data` - Retrieve cached data
  - `POST /data` - Update cached data (triggers backplane invalidation)
  - `GET /cache/info` - Show cache configuration

**Features:**
- Automatic cache invalidation across apps
- Detailed logging for debugging
- REST API for easy testing

---

### 4. **WebApplication2/WebApplication2.csproj**
**Purpose:** Project dependencies for App2

**Changes:** Identical to WebApplication1.csproj

---

### 5. **WebApplication2/Program.cs**
**Purpose:** Application startup and API endpoints

**Changes:** Identical structure to WebApplication1 but with:
- Unique cache key prefix: `app2:` (instead of `app1:`)
- Same endpoints and `SharedDataService`
- Both apps use the same cache key (`shared:data`) to demonstrate synchronization

---

## 📁 Files Created

### 1. **README.md**
**Purpose:** High-level overview and quick reference guide

**Contents:**
- Architecture diagram
- How FusionCache backplane works
- Project structure
- Running instructions
- Example API scenarios
- Troubleshooting tips

### 2. **SETUP_GUIDE.md**
**Purpose:** Comprehensive setup and testing guide

**Contents:**
- Prerequisites and installation
- Quick start (5 steps)
- Detailed architecture explanation
- Four complete test scenarios
- File structure breakdown
- Advanced concepts
- Troubleshooting guide
- Common patterns
- Resources and next steps

### 3. **WebApplication1/api-demo.http**
**Purpose:** REST client file for testing (works in VS Code REST Client or Bruno)

**Features:**
- Test cache info endpoint
- Test cache hit/miss scenarios
- Test cross-app synchronization
- Ready-to-use requests with examples

### 4. **WebApplication2/api-demo.http**
**Purpose:** Same as above but for WebApplication2

### 5. **CHANGES_SUMMARY.md**
**Purpose:** This file - documents all modifications

---

## 🎯 What Was Implemented

### Two-Level Cache Architecture
```
Request → Memory Cache (L1) → Redis Cache (L2) → Factory Function
          ↑ Invalidated by    ↑ Shared between  ↑ Generates data
          │ Backplane         │ apps            │ on L1/L2 miss
```

### Cache Synchronization Flow
```
WebApplication1: SetAsync("shared:data", "value1")
    ↓
FusionCache stores in memory + Redis
    ↓
Publishes to Redis Pub/Sub: "shared:data was modified"
    ↓
WebApplication2 receives message
    ↓
Removes "shared:data" from its memory cache (L1 invalidation)
    ↓
Next request: Cache miss → Fetch from Redis (L2) ✓
```

### Three API Endpoints Per Application

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/data` | GET | Retrieve cached data (hit or miss) |
| `/data` | POST | Update cache value (invalidates in all apps) |
| `/cache/info` | GET | Display cache configuration |

---

## 🚀 How to Use

### Start the Example
```bash
cd eaxmple/Playground/Playground.AppHost
dotnet run
```

### Access the Applications
- **WebApplication1:** https://localhost:7001
- **WebApplication2:** https://localhost:7002
- **Aspire Dashboard:** http://localhost:15000

### Test Cache Synchronization
1. Call `GET https://localhost:7001/data` → Cache miss, generates data
2. Call `GET https://localhost:7002/data` → Same data! (shared via Redis)
3. Call `POST https://localhost:7002/data` with new value → Updates cache
4. Call `GET https://localhost:7001/data` → New value! (backplane invalidated memory cache)

---

## 🔑 Key Configuration Points

### AppHost Configuration
- Redis service name: `cache-redis`
- Apps referenced Redis: Enables service discovery
- Connection string auto-managed by Aspire

### FusionCache Configuration
- **Cache Duration:** 30 seconds (both L1 and L2)
- **Memory Factory:** Lazy (created on first use)
- **Redis Backplane:** Enabled for invalidation broadcasts
- **Cache Key Prefix:** `app1:` and `app2:` (unique per app)

### SharedDataService
- **Cache Key:** `shared:data` (same in both apps)
- **Factory Function:** Returns app name + timestamp when cache misses
- **Logging:** Detailed logs for debugging

---

## 🧪 Testing Scenarios Included

### Test 1: Basic Cache Hit
✓ Verify same request returns same data quickly

### Test 2: Cache Sharing
✓ Verify App2 sees data generated by App1

### Test 3: Backplane Invalidation
✓ Verify updating in App2 invalidates App1's cache

### Test 4: Cache Duration
✓ Verify cache expires after 30 seconds

Each scenario is documented in `SETUP_GUIDE.md` with curl commands.

---

## 📊 Project Dependencies

### Direct Package References
```
StackExchange.Redis v2.8.7
  ↓
ZiggyCreatures.FusionCache (local)
ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis (local)
  ↓
Microsoft.AspNetCore.* (implicit via Web SDK)
```

### Service Dependencies (Runtime)
```
WebApplication1 ┐
                ├→ Redis (cache-redis)
WebApplication2 ┘
```

---

## 🎓 Learning Outcomes

After running this example, you'll understand:

✅ **FusionCache Basics**
- Two-level caching (memory + distributed)
- GetOrSetAsync pattern
- Cache expiration

✅ **Redis Backplane**
- How invalidations are broadcast
- Pub/Sub messaging pattern
- Cross-application cache synchronization

✅ **.NET Aspire**
- Service orchestration
- Service discovery
- Container management

✅ **Distributed Caching Patterns**
- Cache-aside pattern
- Write-through updates
- Handling cache staleness

---

## 🔧 Next Steps

### To Extend This Example

1. **Add Database Integration**
   ```csharp
   // Fetch data from database in factory
   public async Task<User> GetUserAsync(int id)
   {
       return await _cache.GetOrSetAsync(
           $"user:{id}",
           async ct => await _db.GetUserAsync(id)
       );
   }
   ```

2. **Add Error Handling**
   ```csharp
   options.WithOptions(opt => 
       opt.SetFailSafeMaxDuration(TimeSpan.FromMinutes(5))
   );
   ```

3. **Monitor Cache Performance**
   ```csharp
   options.WithOptions(opt =>
       opt.SetEagerRefreshThreshold(0.8) // Refresh at 80% of TTL
   );
   ```

4. **Add More Cache Keys**
   ```csharp
   private const string CacheKey1 = "shared:data";
   private const string CacheKey2 = "shared:config";
   private const string CacheKey3 = "shared:users";
   ```

5. **Implement Distributed Locks**
   - Use Redis locks to prevent thundering herd
   - See FusionCache documentation for patterns

---

## 📝 Files at a Glance

| File | Status | Purpose |
|------|--------|---------|
| `Playground.AppHost/AppHost.cs` | ✏️ Modified | Aspire orchestration |
| `WebApplication1/WebApplication1.csproj` | ✏️ Modified | Dependencies |
| `WebApplication1/Program.cs` | ✏️ Modified | App setup & endpoints |
| `WebApplication2/WebApplication2.csproj` | ✏️ Modified | Dependencies |
| `WebApplication2/Program.cs` | ✏️ Modified | App setup & endpoints |
| `README.md` | ✨ New | Quick reference |
| `SETUP_GUIDE.md` | ✨ New | Comprehensive guide |
| `CHANGES_SUMMARY.md` | ✨ New | This document |
| `WebApplication1/api-demo.http` | ✨ New | API test requests |
| `WebApplication2/api-demo.http` | ✨ New | API test requests |

---

## ✅ Verification Checklist

Before running, ensure:
- [ ] .NET 10.0 SDK installed (`dotnet --version`)
- [ ] Docker Desktop running (`docker ps`)
- [ ] Redis is not already running on port 6379
- [ ] Port 7001, 7002, 15000 are available
- [ ] Git repository is up to date

After running:
- [ ] Aspire dashboard loads at http://localhost:15000
- [ ] All three services show green (redis, webapplication1, webapplication2)
- [ ] Can access https://localhost:7001/cache/info
- [ ] Can access https://localhost:7002/cache/info
- [ ] Cache synchronization test works (see SETUP_GUIDE.md)

---

## 🆘 Common Issues

**Problem:** Redis connection fails  
**Solution:** Ensure Docker is running: `docker ps`

**Problem:** Port already in use  
**Solution:** Change ports in launchSettings.json or kill existing process

**Problem:** Apps not syncing  
**Solution:** Check logs for backplane errors; verify Redis is healthy

**Problem:** Cache stays stale  
**Solution:** Check TTL setting; adjust `WithDefaultDuration()` if needed

---

## 📚 Additional Resources

- **FusionCache Wiki:** https://github.com/ZiggyCreatures/FusionCache/wiki
- **Redis Pub/Sub:** https://redis.io/docs/interact/pubsub/
- **.NET Aspire Docs:** https://learn.microsoft.com/aspire
- **StackExchange.Redis:** https://stackexchange.github.io/StackExchange.Redis/

---

**Version:** 1.0  
**Date:** 2024-06-20  
**Author:** Claude Code  
**License:** See repository license
