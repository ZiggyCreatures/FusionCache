<div align="center">

![FusionCache logo](logo-128x128.png)

</div>


# 📢 Backplane

If you are in a scenario with multiple nodes, each with their own local memory cache, you typically also use a distributed cache as a secondary layer (see [here](CacheLevels.md)).

Even using that, you may find that each memory cache may not be necessarily in-sync with the others, because when a value is cached locally it will stay the same until the `Duration` passes and expiration occurs.

To avoid this and have everything always synchronized you can use a **backplane**, a shared message bus where change notifications will be automatically sent to all other connected nodes each time a value changes in the cache, without you having to do anything.

Everything is handled transparently for you.

<div align="center">

![Extended diagram](images/diagram-extended.png)

</div>

Currently there are 2 official packages you can use:

- [**Memory**](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.Memory/): this is a simple in-memory implementation (typically used only for testing)
- [**Redis**](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis/): this is the real deal, and is based on the awesome [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) library and the [pub/sub](https://redis.io/topics/pubsub) feature of Redis itself. If you are already using a Redis instance as a distributed cache, you just have to point the backplane to the same instance and you'll be good to go (but if you share the same Redis instance with multiple caches, please read below)


## Example

As an example, we'll use FusionCache with [Redis](https://redis.io/), as both a distributed cache and a backplane.

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
// INSTANTIATE A REDIS DISTRIBUTED CACHE (IDistributedCache)
var redis = new RedisCache(new RedisCacheOptions() {
    Configuration = "YOUR CONNECTION STRING HERE"
});

// INSTANTIATE THE FUSION CACHE SERIALIZER
var serializer = new FusionCacheNewtonsoftJsonSerializer();

// INSTANTIATE FUSION CACHE
var cache = new FusionCache(new FusionCacheOptions());

// SETUP THE DISTRIBUTED 2ND LAYER
cache.SetupDistributedCache(redis, serializer);

// CREATE THE BACKPLANE
var backplane = new RedisBackplane(new RedisBackplaneOptions() {
    Configuration = "YOUR CONNECTION STRING HERE"
});

// SETUP THE BACKPLANE
cache.SetupBackplane(backplane);
```

If instead you prefer a **DI (Dependency Injection)** approach you can do this:

```csharp
// REGISTER REDIS AS A DISTRIBUTED CACHE
services.AddStackExchangeRedisCache(options => {
    options.Configuration = "YOUR CONNECTION STRING HERE";
});

// REGISTER THE FUSION CACHE SERIALIZER
services.AddFusionCacheNewtonsoftJsonSerializer();

// REGISTER THE FUSION CACHE BACKPLANE
services.AddFusionCacheStackExchangeRedisBackplane(options => {
    options.Configuration = "YOUR CONNECTION STRING HERE";
});

// REGISTER FUSION CACHE
services.AddFusionCache();
```

and FusionCache will automatically discover the **distributed cache** and the **backplane** and immediately starts using them.


## Redis: a couple of details

<div align="center">

![Redis logo](images/redis-logo.png)

</div>

Redis has a couple of specific design and implementation details that you should be aware of:

- **multiple databases**: Redis has the concept of multiple databases. A single Redis instance can (almost, see below) completely separate data in different isolated database. This is the feature you should use if you want a single Redis instance to manage data about completely different caches, so that the same cache key can be used without collisions. An example may be 2 logical caches, one for your CMS website and one for your Consumer website, where the cache entry for the key "user/123" may contain different values and they should not confilct with one another because they are from very different logical domains (see [here](https://stackexchange.github.io/StackExchange.Redis/Configuration.html))

- **pub/sub scoping**: even though the cache entries in different databases inside the same Redis instance are completely isolated, the same cannot be said about the pub/sub messages. For whatever design decision they are received by any connected client on the same **channel** (a Redis concept to isolate different kind of messages). In theory you should specify different channel names for different logical caches sharing the same Redis instance (when you use more than one), but this is already taken care of for you thanks to the use of the `CacheName` option in the `FusionCacheOptions` object, because by default the channel name uses the cache name as a **prefix**. Having said that, if instead you are using a single Redis instance for the same deployed app twice in different environments (like dev/test), you will probably have the same cache name and, so, the same channel name. In that case you can specify a different channel prefix via the `ChannelPrefix` option in the `FusionCacheOptions` object

- **notifications sender**: in Redis, when a message is sent on a channel, all connected clients will receive it, **including the sender itself**. This would normally be a problem because setting an entry in the cache would evict that entry in all nodes including the one that just set it and that would be a waste or, even worse, a problem. But FusionCache automatically handles this by using a globally unique instance identifier (the `IFusionCache.InstanceId` property) so that the sender will automatically ignore its own notifications, without you having to do anything


## Only memory cache + backplane, but no distributed cache?

This idea seems like a nice one: in a multi-node scenario we may would like to use only memory caches on each node + the backplane for cache synchronization, without having to use a shared distributed cache.

<s>Well, [not so fast](https://github.com/jodydonetti/ZiggyCreatures.FusionCache/issues/36).</s>

Actually, yes!

**TODO:** finish this and explain how to disable automatic backplane publish on set/remove + manual publish, etc...