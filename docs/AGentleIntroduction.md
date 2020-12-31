<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :unicorn: A Gentle Introduction

FusionCache is an easy to use, high performance and robust **multi-level cache** with some advanced features.

It uses a memory cache (any impl of the standard `IMemoryCache` interface) as the **primary** backing store and optionally a distributed, 2nd level cache (any impl of the standard `IDistributedCache` interface) as a **secondary** backing store for better resilience and higher performance, for example in a multi-node scenario or to avoid the typical effects of a cold start (initial empty cache, maybe after a restart).

FusionCache includes some advanced features like **fail-safe**, concurrent **factory calls optimization** for the same cache key, fine grained **soft/hard timeouts**, **background factory completion**, customizable **extensive logging** and more (see below).

<div style="text-align:center;">

![FusionCache diagram](images/diagram.png)

</div>

## :twisted_rightwards_arrows: Cache Levels: Primary and Secondary

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

## :house_with_garden: Feels Like Home

FusionCache tries to feel like a native part of .NET by adhering to the naming conventions of the standard *memory* and *distributed* cache components:

|                               | Memory Cache                | Distributed Cache              | Fusion Cache              |
| ---:                          | :---:                       | :---:                          | :---:                     |
| **Cache Interface**           | `IMemoryCache`              | `IDistributedCache`            | `IFusionCache`            |
| **Cache Implementation**      | `MemoryCache`               | `[Various]Cache`               | `FusionCache`             |
| **Cache Options**             | `MemoryCacheOptions`        | `[Various]CacheOptions`        | `FusionCacheOptions`      |
| **Cache Entry Options**       | `MemoryCacheEntryOptions`   | `DistributedCacheEntryOptions` | `FusionCacheEntryOptions` |

If you've ever used one of those you'll feel at home with FusionCache.

## :rocket: Factory

A factory is just a function that you specify when using the main `GetOrSet[Async]` method: basically it's the way you specify **what to do to get a value** when it is not in the cache.

Here's an example:

```csharp
var id = 42;

var product = cache.GetOrSet<Product>(
    $"product:{id}",
    _ => GetProductFromDb(id), // THIS IS THE FACTORY
    options => options.SetDuration(TimeSpan.FromMinutes(1))
);
```

FusionCache will search for the value in the cache (*memory* and maybe *distributed*) and, if nothing is there, will call the factory to obtain the value: it then saves it into the cache with the specified options, and returns it to the caller, all transparently.

Special care is put into calling just one factory per key, concurrently: this means that if 10 concurrent requests for the same key arrive at the same time and the data is not there, **only one factory** will be called, and the result will be stored and shared with all callers right away.

This ensures that the data source (let's say a database) will not be overloaded with multiple requests for the same piece of data at the same time.

## :bomb: Fail-Safe

Sometimes things can go wrong, and calling a factory for an expired cache entry can lead to exceptions because the database or the network is temporarily down: normally in this case the exception will cause an error page in your website, a failure status code in your api or something like that.

By enabling the fail-safe mechanism you can simply tell FusionCache to ignore those errors and **temporarily use the expired cache entry**: your website or service will remain online, and your users would not notice anything.

You can read more [**here**](FailSafe.md), or enjoy the complete [**step by step**](StepByStep.md) guide.

## :stopwatch: Timeouts

Sometimes your data source (database, webservice, etc) is overloaded, the network is congested or something else is happening, and the end result is a **long wait** for a fresh piece of data.

Wouldn't it be nice if there could be a way to simply let FusionCache temporarily reuse an expired cache entry if the factory is taking too long?

Enter **soft/hard timeouts**.

You can specify a **soft timeout** to be used if there's an expired cache entry to use as a fallback, and a **hard timeout** to be used in any case, no matter what: in this last case an exception will be thrown and you will have to handle it yourself, but in some cases that would be more preferable than a very slow response.

In both cases it is possible (and enabled *by default*, so you don't have to do anything) to let the timed-out factory keep running in the background, and update the cached value as soon as it finishes, so you get the best of both worlds: a **fast response** and **fresh data** as soon as possible.

You can read more [**here**](Timeouts.md), or enjoy the complete [**step by step**](StepByStep.md) guide.

## :level_slider: Options

There are 2 kinds of options:
 
 - `FusionCacheOptions`: cache-wide options, related to the entire FusionCache instance
 - `FusionCacheEntryOptions`: per-entry options, related to each call/entry

You can read more [**here**](Options.md), or enjoy the complete [**step by step**](StepByStep.md) guide.

## :joystick: Core Methods

At a high level there are `5` core methods, all available in both a **sync** and **async** versions, all of which work transparently on both the memory cache and the distributed cache (if any):

- `Set[Async]`: puts a **value** in the cache for the specified **key** using the specified **options**. If something is already there, it will be overwritten
- `Remove[Async]`: removes the **value** in the cache for the specified **key**
- `GetOrSet[Async]`: the most important one, it gets the **value** in the cache for the specified **key** and, if nothing is there, calls the **factory** to obtain a **value** that will be **set** in the cache and then **returned**
- `GetOrDefault[Async]`: gets the **value** in the cache for the specified **key** and, if nothing is there, returns the **default value**
- `TryGet[Async]`: tries to get the **value** in the cache for the specified **key** and returns a `TryGetResult` object containing a `bool` indicating if it was there and the `value` found, or the **default value** if it was not there. Also, the `TryGetResult` type implicitly converts to `bool`, so you can even use it in a statement like `if (cache.TryGet(...))` . Please note that it's not possible to use the classic `out` parameter to set a value because .NET does not allow it on `async` methods (and for good reasons)

Each of these methods has some **overloads** for a better ease of use, see below.

#### :bulb: Why no Get?

You may be wondering why the quite common `Get` method is missing.

It is because its behaviour normally corresponds to FusionCache's `GetOrDefault` method above, but with 2 problems:

1) it is not explicit about what happens when no data is in the cache: will it return some default value? Will it throw an exception? Taking a hint from .NET's `Nullable<T>` type (like `Nullable<int>` or `int?`), it is better to be explicit, so the `GetOrDefault` name has been preferred

2) it makes impossible to determine if something is in the cache or not. If for example we would do something like `cache.Get<Product>("foo")` and it returns `null`, does it mean that nothing was in the cache (so better go check the database) or that `null` was in the cache (so we already checked the database, the product was not there, and we should not check the database again)?

By being explicit and having 2 methods (`GetOrDefault` and `TryGet`) we remove any doubt a developer may have and solve the above issues.

## :recycle: Common overloads

Every core method that needs a set of options (`FusionCacheEntryOptions`) for how to behave has different overloads to let you specify these options, for better ease of use.

You can choose between passing:

- **None**: you don't pass anything, so the global `DefaultEntryOptions` will be used (also saves some memory allocations)
- **Direct**: you directly pass a `FusionCacheEntryOptions` object. This gives you total control over each option, but you have to instantiate it yourself and **does not copy** the global `DefaultEntryOptions`
- **Setup action**: you pass a `lambda` that receives a duplicate of the `DefaultEntryOptions` so you start from there and modify it as you like (there's also a set of *fluent methods* to do that easily)
- **Duration**: you simply pass a `TimeSpan` value for the duration. This is the same as the previous one (start from the global default + lambda) but for the common scenario of when you only want to change the duration

## :dizzy: Natively Sync and Async

Everything is natively available for both the **sync** and **async** programming models.

Any operation works seamlessly with any other, even if one is **sync** and the other is **async**: an example is multiple concurrent factory calls for the same cache key, some of them **sync** while others **async**, all coordinated togheter at the same time with no problems and a guarantee that only one will be executed at the same time.

## :page_with_curl: Logging
FusionCache can log extensively to help you pinpoint any possible problem in your production environment.

It uses the standard `ILogger<T>` interface and a structured logging approach so it fits well in the .NET ecosystem, allowing you to use any implementation you want that is compatible with it (Serilog, NLog, etc).

Since logging can have an impact on performance, a great care has been put into not having to pay that price if not necessary, and in paying as little as you want when you actually need it.

FusionCache lets you customize which `LogLevel` to use for any of the main events (see the `FusionCacheOptions` details [here](Options.md#fusioncacheoptions)) and this, combined with the standard ability to set a minimum `LogLevel` per category (see [here](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging#configure-logging)), can greatly reduce the volume of logged events to obtain less background noise while investigating a problem you may have.