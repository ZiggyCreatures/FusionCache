<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :twisted_rightwards_arrows: Cache Levels: Primary and Secondary

There are possible 2 caching levels, transparently handled by FusionCache for you:

- **Primary**: it's a memory cache and is used to have a very fast access to data in memory, with high data locality. You can give FusionCache any implementation of `IMemoryCache` or let FusionCache create one for you
- **Secondary**: is an *optional* distributed cache (any implementation of `IDistributedCache` will work) and, since it's not strictly necessary and it serves the purpose of **easing a cold start** or **sharing data with other nodes**, it is treated differently than the primary one. This means that any potential error happening on this level (remember the [fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing) ?) can be automatically handled by FusionCache to not impact the overall application, all while (optionally) logging any detail of it for further investigation

Everything is handled transparently for you.

Any implementation of the standard `IDistributedCache` interface will work (see below).

On top of this you also need to specify a *serializer* to use, by providing an implementation of the `IFusionCacheSerializer` interface: you can create your own or pick one of the existing (see below).

Basically it boils down to 2 possible ways:

- **1️⃣ MEMORY ONLY:** if you don't setup a 2nd layer, FusionCache will act as a **normal memory cache** (`IMemoryCache`)

- **2️⃣ MEMORY + DISTRIBUTED:** if you also setup a 2nd layer, FusionCache will automatically coordinate the 2 layers (`IMemoryCache` + `IDistributedCache`) gracefully handling all edge cases to get a smooth experience

Of course in both cases you will also have at your disposal the added ability to enable extra features, like [fail-safe](FailSafe.md), [advanced timeouts](Timeouts.md) and so on.

## Packages

There are a variety of already existing `IDistributedCache` implementations available, just pick one:

| Package Name                   | Version |
|--------------------------------|:---------------:|
| [Microsoft.Extensions.Caching.StackExchangeRedis](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis/) <br/> The official Microsoft implementation for Redis | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.StackExchangeRedis.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis/) |
| [Microsoft.Extensions.Caching.SqlServer](https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/) <br/> The official Microsoft implementation for SqlServer | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.SqlServer.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/) |
| [Microsoft.Extensions.Caching.Cosmos](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Cosmos/) <br/> The official Microsoft implementation for Cosmos DB | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Cosmos/) |
| [MongoDbCache](https://www.nuget.org/packages/MongoDbCache/) <br/> An implementation for MongoDB | [![NuGet](https://img.shields.io/nuget/v/MongoDbCache.svg)](https://www.nuget.org/packages/MongoDbCache/) |
| [MarkCBB.Extensions.Caching.MongoDB](https://www.nuget.org/packages/MarkCBB.Extensions.Caching.MongoDB/) <br/> Another implementation for MongoDB | [![NuGet](https://img.shields.io/nuget/v/MarkCBB.Extensions.Caching.MongoDB.svg)](https://www.nuget.org/packages/MarkCBB.Extensions.Caching.MongoDB/) |
| [EnyimMemcachedCore](https://www.nuget.org/packages/EnyimMemcachedCore/) <br/> An implementation for Memcached | [![NuGet](https://img.shields.io/nuget/v/EnyimMemcachedCore.svg)](https://www.nuget.org/packages/EnyimMemcachedCore/) |
| [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) <br/> An in-memory implementation | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Caching.Memory.svg)](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/) |


As for an implementation of `IFusionCacheSerializer`, pick one of these:

| Package Name                   | Version |
|--------------------------------|:---------------:|
| [ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) <br/> A serializer, based on Newtonsoft Json.NET | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) |
| [ZiggyCreatures.FusionCache.Serialization.SystemTextJson](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) <br/> A serializer, based on the new System.Text.Json | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) |


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
