# FusionCache

![FusionCache logo](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/logo-256x256.png)

| 🙋‍♂️ Updating from before `v0.20.0` ? please [read here](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Update_v0_20_0.md). |
|:-------|

### FusionCache is an easy to use, high performance and robust cache with an optional distributed 2nd layer and some advanced features.

It was born after years of dealing with all sorts of different types of caches: memory caching, distributed caching, http caching, CDNs, browser cache, offline cache, you name it. So I've tried to put together these experiences and came up with FusionCache.

It uses a memory cache (any impl of the standard `IMemoryCache` interface) as the **primary** backing store and optionally a distributed, 2nd level cache (any impl of the standard `IDistributedCache` interface) as a **secondary** backing store for better resilience and higher performance, for example in a multi-node scenario or to avoid the typical effects of a cold start (initial empty cache, maybe after a restart).

Optionally, it can also use a **backplane**: in a multi-node scenario this will send notifications to the other nodes to keep each node's memory cache perfectly synchronized, without any additional work.

FusionCache also includes some advanced features like a **fail-safe** mechanism, **cache stampede** prevention, fine grained **soft/hard timeouts** with **background factory completion**, customizable **extensive logging** and more (see below).

![FusionCache diagram](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/images/diagram.png)

## :trophy: Award
On August 2021, FusionCache received the [Google Open Source Peer Bonus Award](https://twitter.com/jodydonetti/status/1422550932433350666). Here is the [official blogpost](https://opensource.googleblog.com/2021/09/announcing-latest-open-source-peer-bonus-winners.html).


## ✔ Features
These are the **key features** of FusionCache:

- [**🚀 Cache Stampede prevention**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md): using the optimized `GetOrSet[Async]` method prevents multiple concurrent factory calls per key, with a guarantee that only 1 factory will be called at the same time for the same key (this avoids overloading the data source when no data is in the cache or when a cache entry expires)
- [**🔀 Optional 2nd level**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md): FusionCache can transparently handle an optional 2nd level cache: anything that implements the standard `IDistributedCache` interface is supported (eg: Redis, MongoDB, SqlServer, etc)
- [**📢 Backplane**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md): when using a distributed cache as a 2nd layer in a multi-node scenario, you can also enable a backplane to immediately notify the other nodes about changes in the cache, to keep everything synchronized without having to do anything
- [**💣 Fail-Safe**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md): enabling the fail-safe mechanism prevents throwing an exception when a factory or a distributed cache call would fail, by reusing an expired entry as a temporary fallback, all transparently and with no additional code required
- [**⏱ Soft/Hard timeouts**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md): advanced timeouts management prevents waiting for too long when calling a factory or the distributed cache, to avoid hanging your application. It is possible to specify both *soft* and *hard* timeouts, and thanks to automatic background completion no data will be wasted
- [**🧙‍♂️ Adaptive Caching**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md): there are times when you don't know upfront what the cache duration for a piece of data should be, maybe because it depends on the object being cached itself. Adaptive caching solves this elegantly
- [**📛 Named Caches**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md): FusionCache can easily work with multiple named caches, even if differently configured
- [**⚡ High performance**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/StepByStep.md): FusionCache is optimized to minimize CPU usage and memory allocations to get better performance and lower the cost of your infrastructure all while obtaining a more stable, error resilient application
- [**💫 Natively sync/async**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CoreMethods.md): full native support for both the synchronous and asynchronous programming model, with sync/async methods working together harmoniously
- [**📞 Events**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Events.md): there's a comprehensive set of events to subscribe to regarding core events inside of a FusionCache instance, both at a high level and at lower levels (memory/distributed layers)
- [**🧩 Plugins**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Plugins.md): thanks to a plugin subsystem it is possible to extend FusionCache with additional behavior, like adding support for metrics, statistics, etc...
- [**📜 Logging**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md): comprehensive, structured, detailed and customizable logging via the standard `ILogger` interface (you can use Serilog, NLog, etc)
- [**🔃 Dependency Injection**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md): how to work with FusionCache + DI in .NET


## ⭐ Quick Start

FusionCache can be installed via the nuget UI (search for the `ZiggyCreatures.FusionCache` package) or via the nuget package manager console:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache
```

As an example, imagine having a method that retrieves a product from your database:

```csharp
Product GetProductFromDb(int id) {
	// YOUR DATABASE CALL HERE
}
```

💡 This is using the **sync** programming model, but it would be equally valid with the newer **async** one for even better performance.

To start using FusionCache the first thing is create a cache instance:

```csharp
var cache = new FusionCache(new FusionCacheOptions());
```

If instead you are using **DI (Dependency Injection)** use this:

```csharp
services.AddFusionCache();
```

We can also specify some global options, like a default `FusionCacheEntryOptions` object to serve as a default for each call we'll make, with a duration of `2 minutes` and a `Low` priority:

```csharp
var cache = new FusionCache(new FusionCacheOptions() {
	DefaultEntryOptions = new FusionCacheEntryOptions {
		Duration = TimeSpan.FromMinutes(2),
		Priority = CacheItemPriority.Low
	}
});
```

Or, using DI, like this:

```csharp
services.AddFusionCache()
	.WithDefaultEntryOptions(new FusionCacheEntryOptions {
		Duration = TimeSpan.FromMinutes(2),
		Priority = CacheItemPriority.Low
	})
;
```

Now, to get the product from the cache and, if not there, get it from the database in an optimized way and cache it for `30 sec` simply do this:

```csharp
var id = 42;

cache.GetOrSet<Product>(
	$"product:{id}",
	_ => GetProductFromDb(id),
	TimeSpan.FromSeconds(30)
);
```

That's it 🎉


## 📖 Documentation

A complete documentation, including examples and common use cases, is available at the [official repo](https://github.com/ZiggyCreatures/FusionCache) page on GitHub.


## 🧰 Supported Platforms

FusionCache targets `.NET Standard 2.0` so any compatible .NET implementation is fine: this means `.NET Framework` (the old one), `.NET Core 2+` and `.NET 5/6+` (the new ones), `Mono` 5.4+ and more (see [here](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support) for a complete rundown).

**NOTE**: if you are running on **.NET Framework 4.6.1** and want to use **.NET Standard** packages Microsoft suggests to upgrade to .NET Framework 4.7.2 or higher (see the [.NET Standard Documentation](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support)) to avoid some known dependency issues.