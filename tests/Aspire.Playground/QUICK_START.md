# Quick Start - FusionCache Redis Backplane Example

## ⚡ 5-Minute Setup

### Step 1: Prerequisites Check (30 seconds)
```bash
# Check .NET SDK
dotnet --version
# Expected: 10.0.x or higher

# Check Docker
docker --version
# Expected: Docker version 20.x or higher

# Ensure Docker is running
docker ps
# Should show running containers (may be empty)
```

### Step 2: Navigate to Project (10 seconds)
```bash
cd D:\Programming\0.Practice\Contributions\FusionCache-AboubakrFork\eaxmple\Playground\Playground.AppHost
```

### Step 3: Run Aspire (2 minutes)
```bash
dotnet run
```

**Wait for output:**
```
Building...
Starting services...
Aspire dashboard available at http://localhost:15000
```

### Step 4: Open Aspire Dashboard (10 seconds)
- Open browser: http://localhost:15000
- You should see:
  - ✅ cache-redis (green)
  - ✅ webapplication1 (green)
  - ✅ webapplication2 (green)

### Step 5: Test Cache Synchronization (1 minute)

**Option A: Using PowerShell**
```powershell
# Get data from App1
Invoke-RestMethod -Uri "https://localhost:7001/data" -SkipCertificateCheck

# Get data from App2 (should be same!)
Invoke-RestMethod -Uri "https://localhost:7002/data" -SkipCertificateCheck

# Update from App2
Invoke-RestMethod -Uri "https://localhost:7002/data" `
  -Method Post `
  -Body '"Updated data"' `
  -ContentType "application/json" `
  -SkipCertificateCheck

# Check App1 (should have new data!)
Invoke-RestMethod -Uri "https://localhost:7001/data" -SkipCertificateCheck
```

**Option B: Using curl**
```bash
# Get data from App1
curl -k https://localhost:7001/data

# Get data from App2
curl -k https://localhost:7002/data

# Update from App2
curl -X POST https://localhost:7002/data \
  -H "Content-Type: application/json" \
  -d '"Updated data"' \
  -k

# Check App1
curl -k https://localhost:7001/data
```

**Option C: Using REST Client in VS Code**
1. Open `WebApplication1/api-demo.http`
2. Run the requests in order
3. Observe cache behavior

---

## 📝 What You Should See

### Console Output
```
[Information] WebApplication1: Cache miss for shared:data, generating data
[Information] WebApplication2: Cache miss for shared:data, generating data
[Information] WebApplication2: Setting cache value: Updated data
```

### API Responses

**Get from App1:**
```json
{
  "appName": "WebApplication1",
  "timestamp": "2024-06-20T12:00:00Z",
  "data": "Data from WebApplication1 at 2024-06-20T12:00:00Z"
}
```

**Get from App2 (same data!):**
```json
{
  "appName": "WebApplication2",
  "timestamp": "2024-06-20T12:00:05Z",
  "data": "Data from WebApplication1 at 2024-06-20T12:00:00Z"
}
```

**After Update from App2:**
```json
{
  "appName": "WebApplication1",
  "timestamp": "2024-06-20T12:00:10Z",
  "data": "Updated data"
}
```

---

## ✅ Success Criteria

Your setup is working correctly if:

- [ ] Aspire dashboard shows all three services green
- [ ] Both apps return the same data timestamp on first access
- [ ] Updating in App2 changes the data in App1
- [ ] No errors in the console logs
- [ ] You see "Cache miss" logged only once until cache expires

---

## 🔍 Debugging

### Check Redis is Running
```bash
# In another terminal
docker ps | findstr redis
# Should show: fusioncache_cache-redis
```

### Check Logs for Errors
Look at the Aspire dashboard console output for:
```
[Error] Redis connection failed
[Error] Backplane initialization failed
```

### Verify Connections
```powershell
# Check App1 is accessible
Invoke-RestMethod -Uri "https://localhost:7001/cache/info" -SkipCertificateCheck

# Check App2 is accessible
Invoke-RestMethod -Uri "https://localhost:7002/cache/info" -SkipCertificateCheck
```

---

## 📚 What Happened

1. ✅ **Aspire** orchestrated Redis, App1, and App2
2. ✅ **FusionCache** set up two-level caching in each app
3. ✅ **Redis Backplane** connected both apps to the same Redis instance
4. ✅ **Cache Key** `shared:data` is shared between both apps
5. ✅ **Invalidation** broadcast when App2 updated the cache
6. ✅ **Synchronization** App1's memory cache was cleared automatically

---

## 🎯 Next Steps

### Learn More
- Read `README.md` for architecture overview
- Read `SETUP_GUIDE.md` for detailed explanation
- Check `CHANGES_SUMMARY.md` for what was implemented

### Advanced Testing
```bash
# Wait 31 seconds for cache to expire
# Then get data - should generate new timestamp
curl -k https://localhost:7001/data

# Watch logs - should see "Cache miss"
```

### Extend the Example
1. Add database integration
2. Add error handling
3. Add monitoring
4. Add more cache keys
5. Add distributed locks

---

## ⚠️ Troubleshooting

### "Timeout connecting to cache-redis"
**Fix:** Ensure Docker is running
```bash
docker ps
docker start # if stopped
```

### "Port 7001 already in use"
**Fix:** Kill existing process or change port in launchSettings.json

### "Apps not synchronizing"
**Fix:** 
1. Check both apps use same `AddRedis()` reference
2. Wait a moment - Redis Pub/Sub has slight latency
3. Check logs for connection errors

### "Cache stays stale"
**Fix:** Check TTL - adjust `WithDefaultDuration()` or manually clear

---

## 🆘 Need Help?

1. **Check logs** - Aspire dashboard shows detailed output
2. **Review `SETUP_GUIDE.md`** - Has troubleshooting section
3. **Run tests manually** - Use `api-demo.http` files
4. **Inspect Redis** - Use `docker exec cache-redis redis-cli keys '*'`

---

## 🎉 You're Done!

You now have a working example of:
- ✅ FusionCache with two-level caching
- ✅ Redis backplane for cache synchronization
- ✅ Aspire orchestration of multiple services
- ✅ Distributed caching pattern

**Total time: ~5 minutes**

Next, explore the code and understand how cache invalidation works!

---

**Tips:**
- Keep Aspire dashboard open to see logs in real-time
- Use the REST Client extension in VS Code for easy testing
- Check Docker stats: `docker stats cache-redis`
- Monitor Redis keys: `docker exec cache-redis redis-cli monitor`
