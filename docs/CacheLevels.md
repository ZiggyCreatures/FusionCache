<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :twisted_rightwards_arrows: Cache Levels: Primary and Secondary

There are 2 caching levels, transparently handled by FusionCache for you.

These are:
- **Primary**: it's a memory cache, is always there and is used to have a very fast access to data in memory, with high data locality. You can give FusionCache any implementation of `IMemoryCache` or let FusionCache create one for you
- **Secondary**: is an *optional* distributed cache (any implementation of `IDistributedCache` will work) and, since it's not strictly necessary and it serves the purpose of **easing a cold start** or **coordinating with other nodes**, it is treated differently than the primary one. This means that any potential error happening on this level (remember the [fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing) ?) can be automatically handled by FusionCache to not impact the overall application, all while (optionally) logging any detail of it for further investigation

Everything is handled transparently for you.

Any implementation of the standard `IDistributedCache` interface will work, and you also need to specify a *serializer* by providing an implementation of the `IFusionCacheSerializer` interface.

You can create your own serializer or pick one of the existing (eg: based on `Newtonsoft Json.NET` or `System.Text.Json`, available in various packages on nuget).

For example to use FusionCache with [Redis](https://redis.io/) as a distributed cache and [Newtonsoft Json.NET](https://www.newtonsoft.com/json) as the serializer you should add the related packages:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache
PM> Install-Package ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson
PM> Install-Package Microsoft.Extensions.Caching.StackExchangeRedis
```

Then, to create and setup the cache manually, do this:

```csharp
// INSTANTIATE REDIS AS A DISTRIBUTED CACHE
var redis = new RedisCache(new RedisCacheOptions() { Configuration = "YOUR CONNECTION STRING HERE" });

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
    options.Configuration = "YOUR CONNECTION STRING HERE";
});

// REGISTER THE FUSION CACHE SERIALIZER
services.AddFusionCacheNewtonsoftJsonSerializer();

// REGISTER FUSION CACHE
services.AddFusionCache();
```

and FusionCache will automatically discover the registered `IDistributedCache` implementation and, if there's also a valid implementation of `IFusionCacheSerializer`, it picks up both and starts using them.

## Long Story Short

Basically it boils down to this:

- **1️⃣ MEMORY ONLY:** if you don't setup a 2nd layer, FusionCache will act as a **normal memory cache** (`IMemoryCache`)

- **2️⃣ MEMORY + DISTRIBUTED:** if you also setup a 2nd layer, FusionCache will automatically coordinate the 2 layers (`IMemoryCache` + `IDistributedCache`) gracefully handling all edge cases to get a smooth experience

Of course in both cases you will also have at your disposal the added ability to enable extra features, like **fail-safe**, **advanced timeouts**, etc.