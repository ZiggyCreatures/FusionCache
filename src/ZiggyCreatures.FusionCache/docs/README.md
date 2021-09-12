# FusionCache

### FusionCache is an easy to use, high performance and robust cache with an optional distributed 2nd layer and some advanced features.

It was born after years of dealing with all sorts of different types of caches: memory caching, distributed caching, http caching, CDNs, browser cache, offline cache, you name it. So I've tried to put togheter these experiences and came up with FusionCache.

It uses a memory cache (any impl of the standard `IMemoryCache` interface) as the **primary** backing store and optionally a distributed, 2nd level cache (any impl of the standard `IDistributedCache` interface) as a **secondary** backing store for better resilience and higher performance, for example in a multi-node scenario or to avoid the typical effects of a cold start (initial empty cache, maybe after a restart).

FusionCache also includes some advanced features like a **fail-safe** mechanism, **cache stampede** prevention, fine grained **soft/hard timeouts** with **background factory completion**, customizable **extensive logging** and more (see below).

## :trophy: Award
On August 2021, FusionCache received the [Google Open Source Peer Bonus Award](https://twitter.com/jodydonetti/status/1422550932433350666) .


## ✔ Features
These are the **key features** of FusionCache:

- **🚀 Cache Stampede prevention**: using the optimized `GetOrSet[Async]` method prevents multiple concurrent factory calls per key, with a guarantee that only 1 factory will be called at the same time for the same key (this avoids overloading the data source when no data is in the cache or when a cache entry expires)
- **🔀 Optional 2nd level**: FusionCache can transparently handle an optional 2nd level cache: anything that implements the standard `IDistributedCache` interface is supported (eg: Redis, MongoDB, SqlServer, etc)
- **💣 Fail-Safe**: enabling the fail-safe mechanism prevents throwing an exception when a factory or a distributed cache call would fail, by reusing an expired entry as a temporary fallback, all transparently and with no additional code required
- **⏱ Soft/Hard timeouts**: advanced timeouts management prevents waiting for too long when calling a factory or the distributed cache. This is done to avoid that such slow calls would hang your application. It is possible to specify both *soft* and *hard* timeouts that will be used depending on whether there's a fallback value to use for the specific call or not
- **🕶 Background factory completion**: when you specify a factory timeout and it actually occurs, the timed-out factory can keep running in the background and, if and when it successfully complete, the cache will be immediately updated with the new value to be used right away
- **⚡ High performance**: FusionCache is optimized to minimize CPU usage and memory allocations to get better performance and lower the cost of your infrastructure all while obtaining a more stable, error resilient application
- **💫 Natively sync/async**: full native support for both the synchronous and asynchronous programming model, with sync/async methods working togheter harmoniously
- **📞 Events**: there's a comprehensive set of events to subscribe to regarding core events inside of a FusioCache instance, both at a high level and at lower levels (memory/distributed layers)
- **🧩 Plugins**: thanks to a plugin subsystem it is possible to extend FusionCache with additional behaviour, like adding support for metrics, statistics, etc...
- **📃 Extensive logging**: comprehensive, structured, detailed and customizable logging via the standard `ILogger` interface (you can use Serilog, NLog, etc)

Also, FusionCache has some other nice **additional features**:

- **Portable**: targets .NET Standard 2.0
- **Null caching**: explicitly supports caching of null values differently than "no value". This creates a less ambiguous usage, and typically leads to better performance because it avoids the classic problem of not being able to differentiate between *"the value was not in the cache, go check the database"* and *"the value was in the cache, and it was `null`"*
- **Distributed cache circuit-breaker**: it is possible to enable a simple circuit-breaker for when a distributed cache becomes temporarily unavailable. This will prevent the distributed cache to be hit with an additional load of requests (that would probably fail anyway) in a problematic moment, so it can gracefully get back on its feet. More advanced scenarios can be covered using a dedicated solution, like **Polly**
- **Dynamic Jittering**: setting `JitterMaxDuration` will add a small randomized extra duration to a cache entry's normal duration. This is useful to prevent variations of the *Cache Stampede problem* in a multi-node scenario
- **Hot Swap**: supports thread-safe changes of the entire distributed cache implementation (add/swap/removal)
- **Code comments**: every property and method is fully documented in code, with useful informations provided via IntelliSense or similar technologies
- **Fully annotated for nullability**: every usage of nullable references has been annotated for a better flow analysis by the compiler

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
services.AddFusionCache(options => {
	options.DefaultEntryOptions = new FusionCacheEntryOptions {
		Duration = TimeSpan.FromMinutes(2),
		Priority = CacheItemPriority.Low
	}
});
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

A complete documentation, including examples and common use cases, is available at the [official repo](https://github.com/jodydonetti/ZiggyCreatures.FusionCache) page on GitHub.


## 🧰 Supported Platforms

FusionCache targets .NET Standard 2.0, so any compatible .NET implementation is fine.

**NOTE**: if you are running on **.NET Framework 4.6.1** and want to use **.NET Standard** packages Microsoft suggests to upgrade to .NET Framework 4.7.2 or higher (see the .NET Standard Documentation) to avoid some known dependency issues.