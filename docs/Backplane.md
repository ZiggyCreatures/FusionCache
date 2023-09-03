<div align="center">

![FusionCache logo](logo-128x128.png)

</div>


# 📢 Backplane

If we are in a scenario with multiple nodes, each with their own local memory cache, we typically also use a distributed cache as a secondary layer (see [here](CacheLevels.md)).

But even when using that, we may find that each memory cache on each node may not be in-sync with the others, because when a value is cached locally it will stay the same until the `Duration` passes and expiration occurs.

Luckily, there's an easy solution to this synchronization problem: use a **backplane**.

<div align="center">

![Extended diagram](images/diagram-extended.png)

</div>

A backplane is like a message bus where change notifications will be published to all other connected nodes each time something happens to a cache entry, all automatically without us having to do anything.

By default, everything is handled transparently for us 🎉

## 👩‍🏫 How it works

As an example, let's look at the flow of a `GetOrSet` operation with 3 nodes (`N1`, `N2`, `N3`):

- `GetOrSet` is called on `N1`
- no data is found in the memory on `N1` or in the distributed cache (or it is expired): call to the database to grab fresh data
- fresh data saved in memory cache on `N1` + distributed cache
- a backplane notification is sent to notify the other nodes
- the notification is received on `N2` and `N3` and they evict the entry from their own respective memory cache
- as soon as a new request for the same cache entry arrives on `N2` or `N3`, the new version is taken from the distributed cache and saved locally on their memory cache
- `N1`, `N2`, `N3` live happily synchronized ever after

As we can see we didn't have to do anything more than usual: everything else is done automatically for us.


## 📩 Notifications: then what?

One detail that may be interesting to know is what happens when a notification is sent.

When we think about it, there are 3 things that could be done after some data changes (at least theoretically):
1. **ACTIVE:** send the change notification, including the updated data
2. **PASSIVE:** send the change notification, and each client will update it immediately
3. **LAZY:** send the change notification, and each client will remove their local version of the data (since it's old now)

The first approach (ACTIVE) is not really good, since it has these problems:
- requires an extra serialization step for the data to be transmitted
- requires a bigger payload for the notification, sent to all clients
- all of this would be useless in case the data is not actually needed on all the nodes (very realistic)

The second approach (PASSIVE) is not good either, since it has these problems:
- every node will request the same data at the same time from the distributed cache, generating a lot of traffic
- all of this would be useless in case the data is not actually needed on all the nodes (very realistic)

The third approach (LAZY) is the sweet spot, since it just says to each node _"hey, this data is changed, evict your local copy"_: at the next request for that data, if and only if it ever arrives, the data will be automatically get from the distributed cache and everything will work normally, thanks to the cache stampede prevention and all the other features.

One final thing to notice is that FusionCache automatically differentiates between a notification for a change in a piece of data (eg: with `Set(...)` call) and a notification for the removal of a piece of data (eg: with a `Remove(...)` call): why is that? Because if something has been removed from the cache, it will effectively be removed on all the other nodes, to avoid returning something that does not exist anymore. On the other hand if a piece of data is changed, the other nodes will simply mark their local cached copies (if any) as expired, so that subsequent calls for the same data may return the old version in case of problems, if fail-safe will be enabled for those calls.


## ↩️ Auto-Recovery

Since the backplane is implemented on top of a distributed component (in general some sort of message bus, like the Redis Pub/Sub feature) sometimes things can go bad: the message bus can restart or become temporarily unavailable, transient network errors may occur or anything else.

In those situations each nodes' local memory caches will become out of sync, since they would've missed some notifications.

Wouldn't it be nice if FusionCache would help us is some way?

Enter **auto-recovery**.

With auto-recovery FusionCache will detect notifications that failed to be sent, put them in a local temporary queue and when later on the backplane will become available again, it will try to send them to all the other nodes, to re-sync them correctly.

Special care has been put into correctly handling some common situations, like:
- if more than one notification is about to be queued for the same cache key, only the last one will be kept since the result of sending 2 notifications for the same cache key back-to-back would be the same
- if a notification is received for a cache key for which there is a queued notification, only the most recent one is kept: if the incoming one is newer, the local one is discarded and the incoming one is processed, otherwise the incoming one is ignored and the local one is sent to the other nodes. This avoids, for example, evicting an entry from a local cache if it has been updated after a change in a remote node, which would be useless
- it is possible to set a limit in how many notifications to keep in the queue via the `BackplaneAutoRecoveryMaxItems` option to avoid consuming too much memory as it will become available again (default value: `null` which means no limits). If a notification is about to be queued but the limit has already been reached, an heuristic is used to remove the notification for the cache entry that will expire sooner (calculated as: instant when the notification has been created + cache entry's `Duration`), to limit as much as possible the impact on the global shared state synchronization
- when a backplane becomes available again, a little amount of time is awaited to avoid small sync issues, to better handle backpressure in an automatic way (configurable via the `BackplaneAutoRecoveryDelay` option)
- when FusionCache sends a pending backplane notification from the auto-recovery queue, it also knows if originally the distributed cache had been updated, and in case it wasn't it automatically re-sync the distributed cache before sending the notification. If it's able to do that, the notification will be sent, otherwise it will retry later on, all automatically

This feature is not implemented **inside** a specific backplane implementation, of which there are multiple, but inside FusionCache itself: this means that it works with any backplane implementation, which is nice.

**ℹ NOTE:** auto-recovery is available since version `0.14.0`, but it's enabled by default only since version `0.17.0`.

## 📦 Packages

Currently there are 2 official packages we can use:

| Package Name                   | Version | Downloads |
|--------------------------------|:---------------:|:---------:|
| [ZiggyCreatures.FusionCache.Backplane.Memory](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.Memory/) <br/> A simple in-memory implementation (typically used only for testing) | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Backplane.Memory.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.Memory/) | ![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache.Backplane.Memory) |
| [ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis/) <br/> A [Redis](https://redis.io/) implementation based on the awesome [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) library | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis/) | ![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis) |

If we are already using a Redis instance as a distributed cache, we just have to point the backplane to the same instance and we'll be good to go (but if we share the same Redis instance with multiple caches, please read [some notes](RedisNotes.md)).


### Example

As an example, we'll use FusionCache with [Redis](https://redis.io/), as both a **distributed cache** and a **backplane**.

To start, just install the Nuget packages:

```PowerShell
# CORE PACKAGE
PM> Install-Package ZiggyCreatures.FusionCache

# SERIALIZER
PM> Install-Package ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson

# DISTRIBUTED CACHE
PM> Install-Package Microsoft.Extensions.Caching.StackExchangeRedis

# BACKPLANE
PM> Install-Package ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis
```

Then, to create and setup the cache manually, do this:

```csharp
// INSTANTIATE FUSION CACHE
var cache = new FusionCache(new FusionCacheOptions());

// INSTANTIATE A REDIS DISTRIBUTED CACHE (IDistributedCache)
var redis = new RedisCache(new RedisCacheOptions() {
    Configuration = "CONNECTION STRING"
});

// INSTANTIATE THE FUSION CACHE SERIALIZER
var serializer = new FusionCacheNewtonsoftJsonSerializer();

// SETUP THE DISTRIBUTED 2ND LAYER
cache.SetupDistributedCache(redis, serializer);

// CREATE THE BACKPLANE
var backplane = new RedisBackplane(new RedisBackplaneOptions() {
    Configuration = "CONNECTION STRING"
});

// SETUP THE BACKPLANE
cache.SetupBackplane(backplane);
```

Of course we can also use the **DI (Dependency Injection)** approach, thanks to the [Builder](DependencyInjection.md) support:

```csharp
services.AddFusionCache()
    .WithSerializer(
        new FusionCacheNewtonsoftJsonSerializer()
    )
    .WithDistributedCache(
        new RedisCache(new RedisCacheOptions { Configuration = "CONNECTION STRING" })
    )
    .WithBackplane(
        new RedisBackplane(new RedisBackplaneOptions { Configuration = "CONNECTION STRING" })
    )
;
```

The most common scenario is probably to use both a distributed cache and a backplane, working together: the former used as a shared state that all nodes can use, and the latter used to notify all the nodes about synchronization events so that every node is perfectly updated.

But is it really necessary to use a distributed cache at all?

Let's find out.


## 🤔 Distributed cache: is it really necessary?

The idea seems like a nice one: in a multi-node scenario we may want to use only memory caches on each node + the backplane for cache synchronization, without having to use a shared distributed cache.

But remember: when using a backplane, FusionCache automatically publish notifications everytime something "changes" in the cache, namely when we directly call `Set` or `Remove` (of course) but also when calling `GetOrSet` AND the factory actually go to the database (or whatever) to get the fresh piece of data.

So, without the distributed cache as a shared state, every notification would end up requiring a new call to the database, again and again, every single time a new request comes in.

Why? Let's look at an example flow of a `GetOrSet` operation with 3 nodes (`N1`, `N2`, `N3`), without a distributed cache:

- `GetOrSet` is called on `N1`
- no data is found in the memory on `N1` (or it is expired): call to the database to grab fresh data
- fresh data saved in memory cache on `N1`
- a backplane notification is sent to notify the other nodes
- the notification is received on `N2` and `N3` and they evict the entry from their own respective memory cache
- a new request for the same cache entry arrives on `N2` or `N3`
- no data is found in the memory on `N2`: call to the database to grab fresh data
- fresh data saved in memory cache on `N2`
- a backplane notification is sent to notify the other nodes
- the notification is received on `N1` and `N3` and they evict the entry from their own respective memory cache
- so `N1` just erased the entry in the memory cache

As we can see this would basically make the entire cache useless.

This is because not having a **shared state** means we don't know when something actually changed, since when we get fresh data from the database it may be changed since the last time, so we need to notify the other nodes, etc going into an infinite loop.

So how can we solve this?


## 🥳 Look ma: no distributed cache!

The solution is to **skip automatic backplane notifications** and publish them only when we want to signal an actual change.

And how can we do this, in practice?

By default notifications are published on the backplane (if any): to skip them we can just set the `SkipBackplaneNotifications` option on the `FusionCacheEntryOptions` object. This means that we can be granular and specify it for every single operation, but as we know this also means that we can set it to `true` in the global `DefaultEntryOptions` once, and not having to skip them every time.

But then, when we **want** to publish a notification, how can we do it? Easy peasy, simply enable them only for that specific operation.

Let's look at a concrete example.


### Example

```csharp
// INITIAL SETUP: SKIP AUTOMATIC NOTIFICATIONS
cache.DefaultEntryOptions.SkipBackplaneNotifications = true;

// [...]

// LATER ON, SUPPOSE WE JUST SAVED THE PRODUCT IN THE DATABASE, SO WE NEED TO UPDATE THE CACHE
cache.Set(
    $"product:{product.Id}",
    product,
    options => options.SetDuration(TimeSpan.FromMinutes(5)).SetSkipBackplaneNotifications(false)
);

// LATER ON, SUPPOSE WE JUST REMOVED THE PRODUCT FROM THE DATABASE, SO WE NEED TO REMOVE IT FROM THE CACHE TOO
cache.Remove(
    $"product:{product.Id}",
    options => options.SetSkipBackplaneNotifications(false)
);
```

## ⚠ External changes: be careful

Just to reiterate, because it's very important: when using the backplane **without** a distributed cache, any change not manually published by us would result in different nodes not being synched.

This means that, if we want to use the backplane without the distributed cache, we should be confident about the fact that **ALL** changes will be notified by us manually.

To better understand what would happen otherwise let's look at an example, again with a couple of `GetOrSet` operations on 3 nodes (`N1`, `N2`, `N3`):

- `GetOrSet` is called on `N1`
- no data is found in the memory on `N1` (or it is expired): call to the database to grab fresh data
- fresh data saved in memory cache on `N1` for `5 min`
- a background job/cron/whatever changes the data in the database
- a new request for the same cache entry arrives on `N2`
- no data is found in the memory on `N2`: call to the database to grab fresh data
- fresh data saved in memory cache on `N2` for `5 min`

Now `N1` and `N2` will have different data cached for `5 min`, see the problem?

So when using a backplane I would **really** suggest using a distributed cache too, otherwise the system may become a little bit too fragile. If, on the other hand, we are comfortable with such a situation, by all means use it.

## Conclusion

As we saw there are basically 2 ways of using a backplane:

- **1️⃣ MEMORY + DISTRIBUTED + BACKPLANE**: probably the most common, where we don't have to do anything, everything just works and it's hard to have inconsistencies between different nodes
- **2️⃣ MEMORY + BACKPLANE (NO DISTRIBUTED)**: probably the less common, where we have to skip automatic notifications in the default entry options, and then we have to manually enable them on a call-by-call basis only when we actually want to notify the other nodes. It's easier to have inconsistencies between different nodes

⚠ So remember: without a distributed cache we should **SKIP** backplane notifications by default, otherwise our system may suffer.
