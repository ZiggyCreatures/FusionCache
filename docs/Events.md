<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 📞 Events

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to add handlers for FusionCache events, both at high-level meaning about the cache as a whole, and at low-level meaning about the inner components like the memory level, the distributed cache level, the backplane and so on. |

FusionCache has a comprehensive set of events you can subscribe to, so you can be notified of core events when they happen.

They cover both high-level things related to the FusionCache instance as a whole such as cache hits/misses, fail-safe activations or factory timeouts but also more lower level things related to each specific level (memory/distributed) such as evictions in the memory cache, serialization/deserialization errors or cache hits/misses, but specific for each specific level.

Each event is modeled with native .NET `event`s so if you know how to use them you'll feel at home subscribing and unsubscribing to them.

They are grouped into "hubs":

- **general**: useful for high level events related to the FusionCache instance as a whole. It is accessible via the `cache.Events` object
- **memory level**: useful for the lower level memory level. It is accessible via the `cache.Events.Memory` object
- **distributed level**: useful for the lower level distributed level (if any). It is accessible via the `cache.Events.Distributed` object

An event handler is a simple .NET function you can express in the usual ways (lambda, etc...).

For example to subscribe to all events of type cache MISS you can do this:

```csharp
// SUBSCRIBE TO CACHE MISS EVENTS
cache.Events.Miss += (s, e) => {
    // REACT TO THE EVENT HERE, WITH THE RELATED CACHE KEY AVAILABLE VIA e.Key
};
```

It is good practice to unsubscribe from events when you don't need it anymore. To do that simply do `-=` as is normal practice with .NET events:

```csharp
// DECLARE THE EVENT HANDLER AS A VARIABLE YOU CAN REFERE TO
EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => {
    // REACT TO THE EVENT HERE, WITH THE RELATED CACHE KEY AVAILABLE VIA e.Key
};

// SUBSCRIBE TO CACHE MISS EVENTS
cache.Events.Miss += onMiss;

// [...]
// LATER ON, WHEN EVERYTHING IS DONE
// [...]

// UNSUBSCRIBE FROM CACHE MISS EVENTS
cache.Events.Miss -= onMiss;

```

All events follow this pattern, and some of them have **specific event args** with data specific to each event: for example the cache HIT event contains the cache key just like the cache MISS event but it also includes a `bool IsStale` flag that indicates if the cache hit has been for a fresh piece of data or for a stale one (for example after a fail-safe activation).

Here's a non comprehensive list of the available events:

- **Hit**: when a value was in the cache (there's also a flag to indicate if the data was stale or not)
- **Miss**: when a value was not in the cache
- **Remove**: when an entry has been removed
- **Eviction**: when an eviction occurred, along with the reason (only for the memory level)
- **FailSafeActivation**: when the fail-safe mechanism kicked in

There are more, and you easily discover them with code completion by just typing `cache.Events.` or `cache.Events.Memory` / `cache.Events.Distributed` in your code editor.


## ⚙️ On high-level and low-level events

One thing to consider is that subscribing to high level events (via `cache.Events`) should give you the expected results, whereas using directly events from lower levels (via `cache.Events.Memory` or `cache.Events.Distributed`) may surprise you, at first.

Let's make a practical example.

A single high level `GetOrSet` method call may result in one of these two set of HIT/MISS/SET events:

- **🟢 1 HIT**: if found
- **🔴 1 MISS + 🔵 1 SET**: if not found, the factory is called and the returned value is then set into the cache

But if you subscribe to the lower **memory level** events you may fall into one of these situations:

- **🟢 1 HIT**: if immediately found
- **🔴 1 MISS + 🟢 1 HIT**: if not immediately found, a lock is acquired to call the factory in an optimized way, then another cache read is done and this time the entry is found (because another thread set it while acquiring the lock)
- **🔴🔴 2 MISS + 🔵 1 SET**: as above, but even with the 2nd read the entry was not found, so the factory is called and the returned value is then set into the cache

As you can see lower level events may delve into the internals of how FusionCache **currently** works.

Also note that newer locking implementations may be possible in the future (I'm actually trying them out) so lower level events may even change one day.


## 👷 Safe execution
Since an event handler is a normal piece of code that FusionCache runs at a certain point in time, with no special care taken a bad event handler may generate errors or slow everything down.

Thankfully FusionCache takes this into consideration and executes the event handlers in a safe way: each handler is run separately and is guarded against unhandled exceptions (and in case one is thrown you'll find that in the log for later detective work).

Also, by default, the event handlers are run in a background thread so not to slow down FusionCache and, in turn, your application.

| :warning: WARNING |
|:------------------|
| In case you really **really** want to execute event handlers synchronously you can do that by setting the `FusionCacheOptions.EnableSyncEventHandlersExecution` option to `true`. <br/> <br/> But again, you should really **really** **REALLY** avoid that. |