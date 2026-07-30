# FusionCache Redis Backplane - Flow Diagrams

## Scenario 1: First Request (Cache Miss)

```
WebApplication1: GET /data
├─ FusionCache checks L1 (Memory Cache)
│  └─ NOT FOUND ❌
├─ FusionCache checks L2 (Redis)
│  └─ NOT FOUND ❌
├─ FusionCache executes factory function
│  ├─ Generates: "Data from WebApplication1 at 12:00:00Z"
│  └─ Stores in L1 (Memory) + L2 (Redis)
└─ Returns data to client ✅

📊 Performance: Slowest (factory execution)
🔔 Backplane: No message sent
💾 Cache State:
   App1: L1=cached, L2=cached
   App2: L1=empty,  L2=empty
```

---

## Scenario 2: Subsequent Request in Same App (Cache Hit)

```
WebApplication1: GET /data (second call)
├─ FusionCache checks L1 (Memory Cache)
│  └─ FOUND ✅ (still valid, <30s old)
└─ Returns data immediately ⚡

📊 Performance: Fastest (memory access)
🔔 Backplane: No message sent
💾 Cache State:
   App1: L1=cached ✅, L2=cached ✅
   App2: L1=empty,  L2=empty
```

---

## Scenario 3: Request in Different App (Cache Sharing)

```
WebApplication2: GET /data (App1 already warmed the cache)
├─ FusionCache checks L1 (Memory Cache)
│  └─ NOT FOUND ❌ (not accessed yet)
├─ FusionCache checks L2 (Redis)
│  └─ FOUND ✅ (data stored by App1)
│     Returns: "Data from WebApplication1 at 12:00:00Z"
└─ Also caches in L1 for future hits ⚡

📊 Performance: Fast (Redis access)
🔔 Backplane: No message sent (no update)
💾 Cache State:
   App1: L1=cached ✅, L2=cached ✅
   App2: L1=cached ✅, L2=cached ✅
```

---

## Scenario 4: Update from Different App (Invalidation)

```
WebApplication2: POST /data with "Updated value"
├─ Call: await cache.SetAsync("shared:data", "Updated value")
│
├─ FusionCache stores in L1 (Memory) + L2 (Redis)
│  ├─ L1 Update: "Updated value" ✅
│  └─ L2 Update: "Updated value" ✅
│
├─ FusionCache publishes to Redis Pub/Sub:
│  └─ Message: "Invalidate shared:data"
│     Channel: "FusionCache:shared:data"
│
├─ WebApplication1 subscribes to that channel
│  ├─ Receives: "Invalidate shared:data"
│  ├─ Removes from L1 (Memory Cache) 🗑️
│  └─ L2 (Redis) still has the value
│
└─ Returns to client ✅

📊 Performance: Moderate (Redis write + Pub/Sub broadcast)
🔔 Backplane: Message sent ✅
💾 Cache State DURING MESSAGE:
   App1: L1=empty ❌, L2=updated ✅
   App2: L1=updated ✅, L2=updated ✅

💾 Cache State AFTER MESSAGE (100ms):
   App1: L1=empty ❌, L2=updated ✅
   App2: L1=updated ✅, L2=updated ✅
```

---

## Scenario 5: Next Request After Invalidation

```
WebApplication1: GET /data (after invalidation message received)
├─ FusionCache checks L1 (Memory Cache)
│  └─ NOT FOUND ❌ (invalidated by backplane)
├─ FusionCache checks L2 (Redis)
│  └─ FOUND ✅ "Updated value"
│     Returns immediately without factory execution ⚡
└─ Also caches in L1 for next hit

📊 Performance: Fast (Redis hit, no factory)
🔔 Backplane: No message sent
💾 Cache State:
   App1: L1=updated ✅, L2=updated ✅
   App2: L1=updated ✅, L2=updated ✅
```

---

## Scenario 6: Simultaneous Requests (Race Condition Prevention)

```
App1 & App2: Both request GET /data at SAME TIME
Both detect cache miss

┌─ App1: GetOrSetAsync("shared:data")
│  ├─ L1 miss, L2 miss
│  ├─ Acquires lock 🔒
│  ├─ Executes factory
│  └─ Stores in Redis
│
└─ App2: GetOrSetAsync("shared:data")
   ├─ L1 miss, L2 miss
   ├─ Tries to acquire lock 🔒
   ├─ Waits for lock...
   ├─ App1 releases lock after factory
   ├─ L2 hit now! Uses App1's value ✅
   └─ Returns same value

📊 Result: Both return same data ✅
🔔 Factory executed once (only in App1)
```

---

## Scenario 7: Cache Expiration (30 seconds)

```
Time: 00:00 - WebApplication1: GET /data
├─ Factory executes
├─ Stores with TTL=30s
└─ Cache valid until 00:30

Time: 00:15 - WebApplication1: GET /data
├─ Cache still valid (15s remaining)
├─ L1 hit ⚡
└─ No factory execution

Time: 00:31 - WebApplication1: GET /data
├─ L1: EXPIRED ❌ (>30s old)
├─ L2 (Redis): EXPIRED ❌ (TTL reached)
├─ Factory executes (generates new data)
└─ New cache valid until 01:01

Time: 00:35 - WebApplication2: GET /data
├─ L1: MISS (never cached in this app)
├─ L2: MISS (Redis key expired)
├─ App1's factory already executed at 00:31
├─ App2 executes its own factory
│  └─ Different timestamp!
└─ Returns: "Data from WebApplication2 at 00:35"

📊 Result: Different data in each app after expiration
🎯 Reason: Cache expired in L2, each app generated new value
🔔 Backplane: Only broadcasts if manually invalidated
```

---

## Scenario 8: Manual Cache Clear (RemoveAsync)

```
WebApplication2: await cache.RemoveAsync("shared:data")
├─ Removes from L1 (Memory) 🗑️
├─ Removes from L2 (Redis) 🗑️
└─ Publishes: "Remove shared:data"

WebApplication1 receives broadcast:
├─ Removes from L1 (Memory) 🗑️
├─ Removes from L2 (Redis) 🗑️
└─ Both apps now have cache miss

Next request in either app:
├─ L1: MISS, L2: MISS
├─ Factory executes
└─ Cache regenerated

📊 Result: Forced cache refresh across all apps
🔔 Backplane: Message sent for removal
```

---

## Scenario 9: Network Latency (Delayed Invalidation)

```
WebApplication2: POST /data with new value (12:00:00.000)
├─ Stores immediately in memory ✅
├─ Stores immediately in Redis ✅
└─ Publishes invalidation message

WebApplication1: GET /data (12:00:00.050)
├─ L1 still has old value
│  └─ Invalidation message not yet received
└─ Returns OLD data (50ms race condition)

WebApplication1: GET /data (12:00:00.100)
├─ Invalidation message received ✅
├─ L1 cleared 🗑️
├─ L2 hit with new value ✅
└─ Returns NEW data

📊 Edge Case: Small window where different data returned
⚠️  Note: Redis Pub/Sub is fast (~10ms typical)
💡 Mitigation: Critical data can use short TTL + refresh on startup
```

---

## Scenario 10: Redis Connection Lost

```
WebApplication1: GET /data (Redis unavailable)
├─ Try L1 (Memory Cache)
│  └─ FOUND ✅ or MISS ❌
├─ Try L2 (Redis)
│  └─ CONNECTION ERROR 🔴
├─ Fallback options:
│  ├─ Use stale L1 value if available ✅
│  ├─ Execute factory (slow, no cache benefit) ⚡❌
│  └─ Return error (configured behavior) ❌
└─ Result depends on configuration

Configuration: WithFailSafeMaxDuration(minutes: 5)
├─ If error occurred <5 min ago: use last known good value
└─ If error occurred >5 min ago: execute factory

📊 Resilience: System continues working even without Redis
🔔 Backplane: Messages buffered/queued until Redis recovers
```

---

## Scenario 11: Multiple Cache Keys

```
WebApplication1: Cache three different items
├─ Key 1: "config:app" → "Config from WebApplication1"
├─ Key 2: "users:list" → [User1, User2, User3]
└─ Key 3: "shared:data" → "Common data"

WebApplication2: Shares Key 3
├─ Key 1: "config:app" → L2 miss (different data)
├─ Key 2: "users:list" → L2 miss (different data)
└─ Key 3: "shared:data" → L2 hit ✅

Update in WebApplication2:
├─ SetAsync("shared:data", "new value")
├─ Publishes: "Invalidate shared:data"
└─ WebApplication1 receives: Clears Key 3 from L1

Key 1 & 2 unaffected:
└─ Each app maintains its own cache ✅

📊 Result: Selective cache sharing based on key names
```

---

## Scenario 12: Application Restart

```
WebApplication1 stops and restarts
├─ L1 (Memory Cache): Lost 🗑️
│  └─ New empty in-memory cache
├─ L2 (Redis): Still present ✅
│  └─ All data preserved in Redis
├─ First request: L1 miss → L2 hit ⚡
└─ No factory execution, Redis serves stale data

WebApplication2 (running): Unaffected
├─ Continues serving from L1 cache
├─ After 30s expiration: Fetches from Redis
└─ Gets same data as restarted App1

Graceful degradation:
└─ System continues working ✅
   Restart window: ~5 seconds
   Cache warm-up: First few requests

📊 Redis acts as persistent cache layer
🎯 No data loss during app restart
```

---

## Cache State Transition Diagram

```
                    ┌─────────────────────────┐
                    │  EMPTY (No Cache)       │
                    │ L1: empty  L2: empty    │
                    └────────────┬────────────┘
                                 │
                        GetOrSetAsync()
                        (cache miss)
                                 │
                    ┌────────────▼────────────┐
                    │ POPULATED (From App1)   │
                    │ L1: cached  L2: cached  │
                    └────────────┬────────────┘
                                 │
                        ┌────────┴────────┐
                        │                 │
                    SetAsync()      GetOrSetAsync()
                   (from App2)      (same app)
                        │                 │
                        │                 │ ⚡ L1 hit
                        │                 │ (no change)
                        │                 │
                    ┌───▼─────────────────▼──┐
                    │ INVALIDATED (In App1)   │
                    │ L1: empty  L2: cached   │ ◄── Backplane
                    │ (awaiting broadcast)    │     publishes
                    └───┬────────────────────┘
                        │
        App1 receives backplane message
                        │
                    ┌───▼────────────────────┐
                    │ SYNCHRONIZED (App1)    │
                    │ L1: empty  L2: cached  │
                    └───┬────────────────────┘
                        │
                  GetOrSetAsync()
                        │
                    ┌───▼────────────────────┐
                    │ WARM (Both Apps)       │
                    │ L1: cached L2: cached  │
                    └────────────────────────┘
                        
                    (repeats for each update)
```

---

## Performance Timeline

```
Operation               Time        Network Calls   Cache Level
────────────────────────────────────────────────────────────────
Initial L1 Miss         1-2ms       0               Factory
Initial L2 Hit          5-10ms      1 (Redis read)  L2 Redis
L1 Hit (Cached)         <1ms        0               L1 Memory
SetAsync (Update)       5-15ms      1 (Redis write) L1+L2
Backplane Broadcast     ~10ms       1 (Pub/Sub)     All apps
L2 Hit After Invalid    5-10ms      1 (Redis read)  L2 Redis
Lock Contention         50-100ms    N/A             Lock wait
Expiration Refresh      20-30ms     1 (Factory)     Factory result

Legend:
  Fastest:  L1 Hit <1ms
  Good:     L2 Hit 5-10ms
  Moderate: Redis write/broadcast 10-15ms
  Slow:     Factory execution 20-100ms
```

---

## Summary Table

| Scenario | L1 Hit | L2 Hit | Factory | Backplane | Other App |
|----------|--------|---------|---------|-----------|-----------|
| First request | ❌ | ❌ | ✅ | ❌ | ❌ |
| Same app, again | ✅ | ❌ | ❌ | ❌ | - |
| Different app | ❌ | ✅ | ❌ | ❌ | - |
| Update from other | ❌* | ✅ | ❌ | ✅ | Notified |
| After expiration | ❌ | ❌ | ✅ | ❌ | - |
| Manual clear | ❌ | ❌ | ✅ | ✅ | Notified |
| Network down | ✅ | ❌ | ✅ | ❌ | Buffered |

*L1 cleared by invalidation message from backplane

---

**Note:** Timing values are approximate and depend on:
- Network latency
- Redis configuration
- Factory function complexity
- System load

For production, profile your specific workload!
