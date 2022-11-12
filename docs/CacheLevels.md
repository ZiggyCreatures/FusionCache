<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :twisted_rightwards_arrows: Cache Levels: Primary and Secondary

There are 2 caching levels available, transparently handled by FusionCache for you:

- **Primary (Memory)**: it's a memory cache and is used to have a very fast access to data in memory, with high data locality. You can give FusionCache any implementation of `IMemoryCache` or let FusionCache create one for you
- **Secondary (Distributed)**: is an *optional* distributed cache (any implementation of `IDistributedCache` will work) and, since it's not strictly necessary and it serves the purpose of **easing a cold start** or **sharing data with other nodes**, it is treated differently than the primary one. This means that any potential error happening on this level (remember the [fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing) ?) can be automatically handled by FusionCache to not impact the overall application, all while (optionally) logging any detail of it for further investigation

Everything is handled transparently for you.

Any implementation of the standard `IDistributedCache` interface will work (see below).

On top of this you also need to specify a *serializer* to use, by providing an implementation of the `IFusionCacheSerializer` interface: you can create your own or pick one of the existing ones, which natively support formats like Json, MessagePack and Protobuf (see below).

Basically it boils down to 2 possible ways:

- **1️⃣ MEMORY ONLY:** if you don't setup a 2nd layer, FusionCache will act as a **normal memory cache** (`IMemoryCache`)

- **2️⃣ MEMORY + DISTRIBUTED:** if you also setup a 2nd layer, FusionCache will automatically coordinate the 2 layers (`IMemoryCache` + `IDistributedCache`) gracefully handling all edge cases to get a smooth experience

Of course in both cases you will also have at your disposal the added ability to enable extra features, like [fail-safe](FailSafe.md), [advanced timeouts](Timeouts.md) and so on.

Finally, if needed you can also specify a different `Duration` specific for the distributed cache via the `DistributedCacheDuration` option, so that updates to the distributed cache can be picked up more frequently, in case you don't want to use a [backplane](Backplane.md) for some reason.

## Packages

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
| [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) <br/> An in-memory implementation | `MIT` | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.Memory.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) |

As for an implementation of `IFusionCacheSerializer`, pick one of these:

| Package Name                   | License | Version |
|--------------------------------|:---------------:|:---------------:|
| [ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) <br/> A serializer, based on Newtonsoft Json.NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) |
| [ZiggyCreatures.FusionCache.Serialization.SystemTextJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) <br/> A serializer, based on the new System.Text.Json | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) |
| [ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack/) <br/> A MessagePack serializer, based on the most used [MessagePack](https://github.com/neuecc/MessagePack-CSharp) serializer on .NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack/) |
| [ZiggyCreatures.FusionCache.Serialization.ProtoBufNet](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet/) <br/> A Protobuf serializer, based on one of the most used [protobuf-net](https://github.com/protobuf-net/protobuf-net) serializer on .NET | `MIT` | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.ProtoBufNet/) |


## Example

As an example let's use FusionCache with [Redis](https://redis.io/) as a distributed cache and [Newtonsoft Json.NET](https://www.newtonsoft.com/json) as the serializer:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache
PM> Install-Package ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson
PM> Install-Package Microsoft.Extensions.Caching.StackExchangeRedis
```

Then, to create and setup the cache manually, do this:

```csharp
// INSTANTIATE A REDIS DISTRIBUTED CACHE
var redis = new RedisCache(new RedisCacheOptions() { Configuration = "CONNECTION STRING" });

// INSTANTIATE THE FUSION CACHE SERIALIZER
var serializer = new FusionCacheNewtonsoftJsonSerializer();

// INSTANTIATE FUSION CACHE
var cache = new FusionCache(new FusionCacheOptions());

// SETUP THE DISTRIBUTED 2ND LAYER
cache.SetupDistributedCache(redis, serializer);
```

If instead you prefer a **DI (Dependency Injection)** approach you can do this:

```csharp
// REGISTER REDIS AS A DISTRIBUTED CACHE
services.AddStackExchangeRedisCache(options => {
    options.Configuration = "CONNECTION STRING";
});

// REGISTER THE FUSION CACHE SERIALIZER
services.AddFusionCacheNewtonsoftJsonSerializer();

// REGISTER FUSION CACHE
services.AddFusionCache();
```

and FusionCache will automatically discover the registered `IDistributedCache` implementation and, if there's also a valid implementation of `IFusionCacheSerializer`, it picks up both and starts using them.

## 🙋‍♀️ What about a disk cache?

In certain situations we may like to have some of the benefits of a 2nd level like better cold starts (when the memory cache is initially empty) but at the same time we don't want to have a separate **actual** distributed cache to handle, or we simply cannot have it. A good example of that may be a mobile app, where everything should be self contained.

In those situations we may want a distributed cache that is "not really distributed", something like an implementation of `IDistributedCache` that reads and writes directly to one or more local files: makes sense, right?

Yes, kinda, but there is more to that.

We should also think about the details, about all the things it should handle for a real-real world usage:
- have the ability to read and write data in a **persistent** way to local files (so the cached data will survive restarts)
- ability to prevent **data corruption** when writing to disk
- support some form of **compression**, to avoid wasting too much space on disk
- support **concurrent** access without deadlocks, starvations and whatnot
- be **fast** and **resource optimized**, so to consume as little cpu cycles and memory as possible
- and probably something more that I'm forgetting

That's a lot to do... but wait a sec, isn't that exactly what a **database** is?

Yes, yes it is!

### An actual database? Are you kidding me?

Of course I'm not suggesting to install (and manage) a local MySql/SqlServer/PostgreSQL instance or something, that would be hard to do in most cases, impossible in others and frankly overkill.

So, what should we use?

### Sqlite to the rescue!

If case you didn't know it yet, [Sqlite](https://www.sqlite.org/) is an incredible piece of software:
- it's one of the highest quality software [ever produced](https://www.i-programmer.info/news/84-database/15609-in-praise-of-sqlite.html)
- it's used in production on [billions of devices](https://www.sqlite.org/mostdeployed.html), with a higher instance count than all the other database engines, combined
- it's [fully tested](https://www.sqlite.org/testing.html), with millions of test cases, 100% test coverage, fuzz tests and more, way more (the link is a good read, I suggest to take a look at it)
- it's very robust and fully [transactional](https://www.sqlite.org/hirely.html), no worries about [data corruption](https://www.sqlite.org/transactional.html)
- it's fast, like [really really fast](https://www.sqlite.org/fasterthanfs.html). Like, 35% faster than direct file I/O!
- has a very [small footprint](https://www.sqlite.org/footprint.html)
- the [license](https://www.sqlite.org/copyright.html) is as free and open as it can get

Ok so Sqlite is the best, how can we use it as the 2nd level?

### Ok but how?

Luckily someone in the community created an implementation of `IDistributedCache` based on Sqlite, and released it as the [NeoSmart.Caching.Sqlite](https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/) Nuget package (GitHub repo [here](https://github.com/neosmart/AspSqliteCache)).

The package:
- supports both the sync and async models natively, meaning it's not doing async-over-sync or vice versa, but a real double impl (like FusionCache does) which is very nice and will use the underlying system resources best
- uses a [pooling mechanism](https://github.com/neosmart/AspSqliteCache/blob/master/SqliteCache/DbCommandPool.cs) which means the memory allocation will be lower since they reuse existing objects instead of creating new ones every time and consequently, because of that, less cpu usage in the long run because less pressure on the GC (Garbage Collector)
- supports `CancellationToken`s, meaning that it will gracefully handle cancellations in case it's needed, like for example a mobile app pause/shutdown events or similar

So, we simply use that package as an impl of `IDistributedCache` and we are good to go!

Oh, and give that repo a star ⭐ and share it!
