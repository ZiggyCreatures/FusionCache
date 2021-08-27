﻿<div align="center">

![FusionCache logo](docs/logo-256x256.png)

</div>

# FusionCache

<div align="center">

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=flat&logo=twitter)](https://twitter.com/intent/tweet?hashtags=fusioncache,caching,cache,dotnet,oss,csharp&text=🚀+FusionCache:+a+new+cache+with+an+optional+2nd+layer+and+some+advanced+features&url=https%3A%2F%2Fgithub.com%2Fjodydonetti%2FZiggyCreatures.FusionCache&via=jodydonetti)

</div>

### FusionCache is an easy to use, high performance and robust cache with an optional distributed 2nd layer and some advanced features.

It was born after years of dealing with all sorts of different types of caches: memory caching, distributed caching, http caching, CDNs, browser cache, offline cache, you name it. So I've tried to put togheter these experiences and came up with FusionCache.

It uses a memory cache (any impl of the standard `IMemoryCache` interface) as the **primary** backing store and optionally a distributed, 2nd level cache (any impl of the standard `IDistributedCache` interface) as a **secondary** backing store for better resilience and higher performance, for example in a multi-node scenario or to avoid the typical effects of a cold start (initial empty cache, maybe after a restart).

<div style="text-align:center;">

![FusionCache diagram](docs/images/diagram.png)

</div>

FusionCache also includes some advanced features like a **fail-safe** mechanism, concurrent **factory calls optimization** for the same cache key, fine grained **soft/hard timeouts** with **background factory completion**, customizable **extensive logging** and more (see below).

If you want to get yourself **comfortable with the overall concepts** there's [:unicorn: A Gentle Introduction](docs/AGentleIntroduction.md) available.

If you want to see what you can achieve **from start to finish** with FusionCache, there's a [:trophy: Step By Step ](docs/StepByStep.md) guide.

If instead you want to start using it **immediately** there's a [:star: Quick Start](#star-quick-start) for you.

<div align="center">
	
![Google Award](docs/google-award-128x128.png)

</div>

On August 2021, FusionCache received the [Google Open Source Peer Bonus Award](https://twitter.com/jodydonetti/status/1422550932433350666).

## :heavy_check_mark: Features
These are the **key features** of FusionCache:

- **:rocket: Optimized factory calls**: using the optimized `GetOrSet[Async]` method prevents multiple concurrent factory calls per key, with a guarantee that only 1 factory will be called at the same time for the same key. This avoids overloading the data source when no data is in the cache or when a cache entry expires ([read more](docs/FactoryOptimization.md))
- **:twisted_rightwards_arrows: Optional 2nd level**: FusionCache can transparently handle an optional 2nd level cache: anything that implements the standard `IDistributedCache` interface is supported like Redis, Memcached, MongoDB, SqlServer, etc ([read more](docs/CacheLevels.md))
- **:bomb: Fail-Safe**: enabling the fail-safe mechanism prevents throwing an exception when a factory or a distributed cache call would fail, by reusing an expired entry as a temporary fallback, all transparently and with no additional code required ([read more](docs/FailSafe.md))
- **:stopwatch: Soft/Hard timeouts**: advanced timeouts management prevents waiting for too long when calling a factory or the distributed cache. This is done to avoid that such slow calls would hang your application. It is possible to specify both *soft* and *hard* timeouts that will be used depending on whether there's a fallback value to use for the specific call or not ([read more](docs/Timeouts.md))
- **:dark_sunglasses: Background factory completion**: when you specify a factory timeout and it actually occurs, the timed-out factory can keep running in the background and, if and when it successfully complete, the cache will be immediately updated with the new value to be used right away ([read more](docs/Timeouts.md))
- **:zap: High performance**: FusionCache is optimized to minimize CPU usage and memory allocations to get better performance and lower the cost of your infrastructure all while obtaining a more stable, error resilient application
- **:dizzy: Natively sync/async**: full native support for both the synchronous and asynchronous programming model, with sync/async methods working togheter harmoniously ([read more](docs/CoreMethods.md))
- **:telephone_receiver: Events**: there's a comprehensive set of events to subscribe to regarding core events inside of a FusionCache instance, both at a high level and at lower levels (memory/distributed layers) ([read more](docs/Events.md))
- **:jigsaw: Plugins**: thanks to a plugin subsystem it is possible to extend FusionCache with additional behaviour, like adding support for metrics, statistics, etc... ([read more](docs/Plugins.md))
- **:page_with_curl: Extensive logging**: comprehensive, structured, detailed and customizable logging via the standard `ILogger<T>` interface (you can use Serilog, NLog, etc)

Also, FusionCache has some other nice **additional features**:

- **Portable**: targets .NET Standard 2.0
- **Null caching**: explicitly supports caching of null values differently than "no value". This creates a less ambiguous usage, and typically leads to better performance because it avoids the classic problem of not being able to differentiate between *"the value was not in the cache, go check the database"* and *"the value was in the cache, and it was `null`"*
- **Distributed cache circuit-breaker**: it is possible to enable a simple circuit-breaker for when a distributed cache becomes temporarily unavailable. This will prevent the distributed cache to be hit with an additional load of requests (that would probably fail anyway) in a problematic moment, so it can gracefully get back on its feet. More advanced scenarios can be covered using a dedicated solution, like <a href="https://github.com/App-vNext/Polly">Polly</a>
- **Dynamic Jittering**: setting `JitterMaxDuration` will add a small randomized extra duration to a cache entry's normal duration. This is useful to prevent variations of the <a href="https://en.wikipedia.org/wiki/Cache_stampede">Cache Stampede problem</a> in a multi-node scenario
- **Hot Swap**: supports thread-safe changes of the entire distributed cache implementation (add/swap/removal)
- **Code comments**: every property and method is fully documented in code, with useful informations provided via IntelliSense or similar technologies
- **Fully annotated for nullability**: every usage of nullable references has been annotated for a better flow analysis by the compiler

## :package: Distribution

Available packages on Nuget:

| Package Name                   | Release (NuGet) | Downloads |
|--------------------------------|:---------------:|:---------:|
| `ZiggyCreatures.FusionCache`         | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache/) | ![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache) |
| `ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson`         | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson/) | ![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson) |
| `ZiggyCreatures.FusionCache.Serialization.SystemTextJson`         | [![NuGet](https://img.shields.io/nuget/v/ZiggyCreatures.FusionCache.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/ZiggyCreatures.FusionCache.Serialization.SystemTextJson/) | ![Nuget](https://img.shields.io/nuget/dt/ZiggyCreatures.FusionCache.Serialization.SystemTextJson) |

## :star: Quick Start

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

:bulb: This is using the **sync** programming model, but it would be equally valid with the newer **async** one for even better performance.

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

That's it :tada:

<details>
	<summary>Want a little bit more 😏 ?</summary>

Now, imagine we want to do the same, but also:
- set the **priority** of the cache item to `High` (mainly used in the underlying memory cache)
- enable **fail-safe** for `2 hours`, to allow an expired value to be used again in case of problems with the database ([read more](docs/FailSafe.md))
- set a factory **soft timeout** of `100 ms`, to avoid too slow factories crumbling your application when there's a fallback value readily available ([read more](docs/Timeouts.md))
- set a factory **hard timeout** of `2 sec`, so that, even if there is no fallback value to use, you will not wait undefinitely but instead an exception will be thrown to let you handle it however you want ([read more](docs/Timeouts.md))

To do all of that we simply have to change the last line (reformatted for better readability):

```csharp
cache.GetOrSet<Product>(
	$"product:{id}",
	_ => GetProductFromDb(id),
	// THIS IS WHERE THE MAGIC HAPPENS
	options => options
		.SetDuration(TimeSpan.FromSeconds(30))
		.SetPriority(CacheItemPriority.High)
		.SetFailSafe(true, TimeSpan.FromHours(2))
		.SetFactoryTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2))
);
```

Basically, on top of specifying the *cache key* and the *factory*, instead of specifying just a *duration* as a `TimeSpan` we specify a `FusionCacheEntryOptions` object - which contains all the options needed to control the behaviour of FusionCache during each operation - in the form of a lambda that automatically duplicates the default entry options defined before (to copy all our  defaults) while giving us a chance to modify it as we like for this specific call.

Now let's say we really like these set of options (*priority*, *fail-safe* and *factory timeouts*) and we want them to be the overall defaults, while keeping the ability to change something on a per-call basis (like the *duration*).

To do that we simply **move** the customization of the entry options to the `DefaultEntryOptions` in the snippet where we created the FusionCache instance, to something like this:

```csharp
var cache = new FusionCache(new FusionCacheOptions() {
	DefaultEntryOptions = new FusionCacheEntryOptions()
		.SetDuration(TimeSpan.FromMinutes(2))
		.SetPriority(CacheItemPriority.High)
		.SetFailSafe(true, TimeSpan.FromHours(2))
		.SetFactoryTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2))
});
```

Now these options will serve as the **cache-wide default**, usable in every method call as a "starting point".

Then, we just change our method call to simply this:

```csharp
var id = 42;

cache.GetOrSet<Product>(
	$"product:{id}",
	_ => GetProductFromDb(id),
	options => options.SetDuration(TimeSpan.FromSeconds(30))
);
```

The `DefaultEntryOptions` we did set before will be duplicated and only the duration will be changed for this call.
</details>

## :book: Documentation

The documentation is available in the :open_file_folder: [docs](docs/README.md) folder, with:

- [**:unicorn: A Gentle Introduction**](docs/AGentleIntroduction.md): what you need to know first
- [**:twisted_rightwards_arrows: Cache Levels**](docs/CacheLevels.md): a bried description of the 2 available caching levels and how to setup them
- [**:rocket: Factory Call Optimization**](docs/FactoryOptimization.md): no more overloads during a cold start or after an expiration
- [**:bomb: Fail-Safe**](docs/FailSafe.md): an explanation of how the fail-safe mechanism works
- [**:stopwatch: Timeouts**](docs/Timeouts.md): the various types of timeouts at your disposal (calling a factory, using the distributed cache, etc)
- [**:level_slider: Options**](docs/Options.md): everything about the available options, both cache-wide and per-call
- [**:joystick: Core Methods**](docs/CoreMethods.md): what you need to know about the core methods available
- [**:telephone_receiver: Events**](docs/Events.md): the events hub and how to use it
- [**:jigsaw: Plugins**](docs/Plugins.md): how to create and use plugins


## **:trophy: Step By Step**
If you are in for a ride you can read a complete [step by step example](docs/StepByStep.md) of why a cache is useful, why FusionCache could be even more so, how to apply most of the options available and what **results** you can expect to obtain.

## :ab: Comparison

There are various alternatives out there with different features, different performance characteristics (cpu/memory) and in general a different set of pros/cons.

A [feature comparison](docs/Comparison.md) between existing .NET caching solutions may help you choose which one to use.

## 🧰 Supported Platforms

FusionCache targets .NET Standard 2.0, so any compatible .NET implementation is fine.

**NOTE**: if you are running on **.NET Framework 4.6.1** and want to use **.NET Standard** packages Microsoft suggests to upgrade to .NET Framework 4.7.2 or higher (see the [.NET Standard Documentation](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support)) to avoid some known dependency issues.

## 🖼 Logo

The logo is an [original creation](https://dribbble.com/shots/14854206-FusionCache-logo) and is a [sloth](https://en.wikipedia.org/wiki/Sloth) because, you know, speed :sloth:.

## 💰 Funding / Support

Nothing to do here.

After years of using a lot of open source stuff for free, this is just me trying to give something back to the community.
<br/>
If you find FusionCache useful please just [**:envelope: drop me a line**](https://twitter.com/jodydonetti), I would be interested in knowing about your usage.

And if you really want to talk about money, please consider making  **:heart: a donation to a good cause** of your choosing, and maybe let me know about that.

## 💼 Is it Production Ready :tm: ?
Even though the current version is `0.X` for an excess of caution, FusionCache is already used **in production** on multiple **real world projects** happily handling millions of requests per day (at least these are the projects I'm aware of).

And again, if you are using it please [**:envelope: drop me a line**](https://twitter.com/jodydonetti), I'd like to know!
