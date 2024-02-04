<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🔀 Cache Levels: Primary and Secondary

There are 2 caching levels available, transparently handled by FusionCache for you:

- **Primary (Memory)**: it's a memory cache and is used to have a very fast access to data in memory, with high data locality. You can give FusionCache any implementation of `IMemoryCache` or let FusionCache create one for you
- **Secondary (Distributed)**: is an *optional* distributed cache (any implementation of `IDistributedCache` will work) and, since it's not strictly necessary and it serves the purpose of **easing a cold start** or **sharing data with other nodes**, it is treated differently than the primary one. This means that any potential error happening on this level (remember the [fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing) ?) can be automatically handled by FusionCache to not impact the overall application, all while (optionally) logging any detail of it for further investigation

Everything is handled transparently for you.

Any implementation of the standard `IDistributedCache` interface will work (see below).

On top of this you also need to specify a *serializer* to use, by providing an implementation of the `IFusionCacheSerializer` interface: you can create your own or pick one of the existing ones, which natively support formats like Json, MessagePack and Protobuf (see below).

Basically it boils down to 2 possible ways:

- **1️⃣ MEMORY ONLY:** if you don't setup a 2nd level, FusionCache will act as a **normal memory cache** (`IMemoryCache`)

- **2️⃣ MEMORY + DISTRIBUTED:** if you also setup a 2nd level, FusionCache will automatically coordinate the 2 levels (`IMemoryCache` + `IDistributedCache`) gracefully handling all edge cases to get a smooth experience

Of course in both cases you will also have at your disposal the added ability to enable extra features, like [fail-safe](FailSafe.md), [advanced timeouts](Timeouts.md) and so on.

Finally, if needed you can also specify a different `Duration` specific for the distributed cache via the `DistributedCacheDuration` option, so that updates to the distributed cache can be picked up more frequently, in case you don't want to use a [backplane](Backplane.md) for some reason.

## 🗃 Wire Format Versioning

When working with the memory cache, everything is easier: at every run of our apps or services everything starts clean, from scratch, so even if there's a change in the structure of the cache entries used by FusionCache there's no problem.

The distributed cache, instead, is a different beast: when saving a cache entry in there, that data is shared between different instances of the same applications, between different applications altogether and maybe even with different applications that are using a different version of FusionCache.

So when the structure of the cache entries need to change to evolve FusionCache, how can this be managed?

Easy, by using an additional cache key modifier for the distributed cache, so that if and when the version of the cache entry structure changes, there will be no issues serializing or deserializing different versions of the saved data.

In practice this means that, when saving something for the cache key `"foo"`, in reality in the distributed cache it will be saved with the cache key `"v0:foo"`.

This has been planned from the beginning, and is the way to manage changes in the wire format used in the distributed cache between updates: it has been designed in this way specifically to support FusionCache to be updated safely and transparently, without interruptions or problems.

So what happens when there are 2 versions of FusionCache running on the same distributed cache instance, for example when two different apps share the same distributed cache and one is updated and the other is not?

Since the old version will write to the distributed cache with a different cache key than the new version, this will not create conflicts during the update, and it means that we don't need to stop all the apps and services that works on it and wipe all the distributed cache data just to do the upgrade.

At the same time though, if we have different apps and services that use the same distributed cache shared between them, we need to understand that by updating only one app or service and not the others will mean that the ones updated will read/write using the new distributed cache keys, while the non updated ones will keep read/write using the old distributed cache keys.

Again, nothing catastrophic, but something to consider.

## 💾 Disk Cache

In certain situations we may like to have some of the benefits of a 2nd level like better cold starts (when the memory cache is initially empty) but at the same time we don't want to have a separate **actual** distributed cache to handle, or we simply cannot have it: a good example may be a mobile app, where everything should be self contained.

In those situations we may want a distributed cache that is "not really distributed", something like an implementation of `IDistributedCache` that reads and writes directly to one or more local files.

Is this possible?

Yes, totally, and there's a [dedicated page](DiskCache.md) to learn more.

## ↩️ Auto-Recovery

Since the distributed cache is a distributed component (just like the backplane), most of the transient errors that may occur on it are also covered by the Auto-Recovery feature: you can read more on the related [docs page](AutoRecovery.md).

## 📦 Packages

There are a variety of already existing `IDistributedCache` implementations available, just pick one:

| Package Name                   | License | Version |
|--------------------------------|:---------------:|:---------------:|
| [Microsoft.Extensions.Caching.StackExchangeRedis](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis/) <br/> The official Microsoft implementation for Redis | `MIT` | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.StackExchangeRedis.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis/) |
| [Microsoft.Extensions.Caching.SqlServer](https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/) <br/> The official Microsoft implementation for SqlServer | `MIT` | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.SqlServer.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/) |
| [Microsoft.Extensions.Caching.Cosmos](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Cosmos/) <br/> The official Microsoft implementation for Cosmos DB | `MIT` | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Cosmos/) |
| [MongoDbCache](https://www.nuget.org/packages/MongoDbCache/) <br/> An implementation for MongoDB | `MIT` | [![NuGet](https://img.shields.io/nuget/v/MongoDbCache.svg)](https://www.nuget.org/packages/MongoDbCache/) |
| [MarkCBB.Extensions.Caching.MongoDB](https://www.nuget.org/packages/MarkCBB.Extensions.Caching.MongoDB/) <br/> Another implementation for MongoDB | `Apache v2` | [![NuGet](https://img.shields.io/nuget/v/MarkCBB.Extensions.Caching.MongoDB.svg)](https://www.nuget.org/packages/MarkCBB.Extensions.Caching.MongoDB/) |
| [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) <br/> An implementation for Memcached | `Apache v2` | [![NuGet](https://img.shields.io/nuget/v/EnyimMemcachedCore.svg)](https://www.nuget.org/packages/EnyimMemcachedCore/) |
| [NeoSmart.Caching.Sqlite](https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/) <br/> An implementation for SQLite | `MIT` | [![NuGet](https://img.shields.io/nuget/v/NeoSmart.Caching.Sqlite.svg)](https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/) |
| [AWS.AspNetCore.DistributedCacheProvider](https://www.nuget.org/packages/AWS.AspNetCore.DistributedCacheProvider/) <br/> An implementation for AWS DynamoDB | `Apache v2` | [![NuGet](https://img.shields.io/nuget/v/AWS.AspNetCore.DistributedCacheProvider.svg)](https://www.nuget.org/packages/AWS.AspNetCore.DistributedCacheProvider/) |
| [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) <br/> An in-memory implementation | `MIT` | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.Memory.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) |

As for an implementation of `IFusionCacheSerializer`, pick one of these:

| Package Name                   | License | Version |
|--------------------------------|:---------------:|:---------------:|
| [ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) <br/> A serializer, based on Newtonsoft Json.NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) |
| [ZiggyCreatures.FusionCache.Serialization.SystemTextJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) <br/> A serializer, based on the new System.Text.Json | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) |
| [ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack/) <br/> A MessagePack serializer, based on the most used [MessagePack](https://github.com/neuecc/MessagePack-CSharp) serializer on .NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack/) |
| [ZiggyCreatures.FusionCache.Serialization.ProtoBufNet](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet/) <br/> A Protobuf serializer, based on one of the most used [protobuf-net](https://github.com/protobuf-net/protobuf-net) serializer on .NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet/) |
| [ZiggyCreatures.FusionCache.Serialization.CysharpMemoryPack](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.CysharpMemoryPack/) <br/> A serializer based on the uber fast new serializer by Neuecc, [MemoryPack](https://github.com/Cysharp/MemoryPack) | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.CysharpMemoryPack.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.CysharpMemoryPack/) |
| [ZiggyCreatures.FusionCache.Serialization.ServiceStackJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ServiceStackJson/) <br/> A serializer based on the [ServiceStack](https://servicestack.net/) JSON serializer | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.ServiceStackJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ServiceStackJson/) |


### 👩‍💻 Example

As an example let's use FusionCache with [Redis](https://redis.io/) as a distributed cache and [Newtonsoft Json.NET](https://www.newtonsoft.com/json) as the serializer:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache
PM> Install-Package ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson
PM> Install-Package Microsoft.Extensions.Caching.StackExchangeRedis
```

Then, to create and setup the cache manually, we can do this:

```csharp
// INSTANTIATE A REDIS DISTRIBUTED CACHE
var redis = new RedisCache(new RedisCacheOptions() { Configuration = "CONNECTION STRING" });

// INSTANTIATE THE FUSION CACHE SERIALIZER
var serializer = new FusionCacheNewtonsoftJsonSerializer();

// INSTANTIATE FUSION CACHE
var cache = new FusionCache(new FusionCacheOptions());

// SETUP THE DISTRIBUTED 2ND LEVEL
cache.SetupDistributedCache(redis, serializer);
```

If instead we prefer a **DI (Dependency Injection)** approach, we should simply do this:

```csharp
services.AddFusionCache()
    .WithSerializer(
        new FusionCacheNewtonsoftJsonSerializer()
    )
    .WithDistributedCache(
        new RedisCache(new RedisCacheOptions { Configuration = "CONNECTION STRING" })
    )
;
```

Easy peasy.
