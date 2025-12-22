# FusionCache

![FusionCache logo](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/logo-256x256.png)

## FusionCache is an easy to use, fast and robust hybrid cache with advanced resiliency features.

It was born after years of dealing with all sorts of different types of caches: memory caching, distributed caching, http caching, CDNs, browser cache, offline cache, you name it.

So I tried to put together these experiences and came up with FusionCache.

![FusionCache diagram](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/images/diagram.png)

Being a hybrid cache means it can transparently work as either a normal memory cache (L1) or as a multi-level cache (L1+L2), where the distributed 2nd level (L2) can be any implementation of the standard `IDistributedCache` interface: this will get us better cold starts, better horizontal scalability, more resiliency and overall better performance.

FusionCache also includes an optional [backplane](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md) for realtime sync between multiple nodes and advanced resiliency features like [cache stampede](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md) protection, a [fail-safe](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md) mechanism, [soft/hard timeouts](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md), [eager refresh](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/EagerRefresh.md), full observability via [logging](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md) and [OpenTelemetry](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/OpenTelemetry.md), [tagging](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md) and much more.

It's being used in production on real-world projects with huge volumes for years, and is even used by Microsoft itself in its products like [Data API Builder](https://devblogs.microsoft.com/azure-sql/data-api-builder-ga/).

It's also compatible with the new HybridCache from Microsoft, thanks to a [powerful integration](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/MicrosoftHybridCache.md).

## 🏆 Award

On August 2021, FusionCache received the [Google Open Source Peer Bonus Award](https://twitter.com/jodydonetti/status/1422550932433350666): here is the [official blogpost](https://opensource.googleblog.com/2021/09/announcing-latest-open-source-peer-bonus-winners.html).

## 📕 Getting Started

With [🦄 A Gentle Introduction](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AGentleIntroduction.md) you'll get yourself comfortable with the overall concepts.

Want to start using it immediately? There's a [⭐ Quick Start](https://github.com/ZiggyCreatures/FusionCache/blob/main/README.md#-quick-start) for you.

Curious about what you can achieve from start to finish? There's a [👩‍🏫 Step By Step ](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/StepByStep.md) guide.

In search of all the docs? There's a [page](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/README.md) for that, too.

## 🧑‍🏫 Courses ([more](https://dometrain.com/course/getting-started-caching-in-dotnet/?ref=jody-donetti))

If you are interested in all things caching, I published [a course](https://dometrain.com/course/getting-started-caching-in-dotnet/?ref=jody-donetti) on Dometrain:

[![Caching Course on Dometrain](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/images/dometrain-getting-started-cover.png)](https://dometrain.com/course/getting-started-caching-in-dotnet/?ref=jody-donetti)

If you like the FusionCache docs, you may like it too.

But mind you, it's not just about FusionCache but about caching as a whole: we'll go from the very foundations to pretty advanced topics and scenarios. We'll cover performance, robustness, resiliency and we'll see different real-world problems and, most importantly, solutions for them.

I tried condensing 20+ years dealing with caching in one place, all in an approachable way.

## 📺 Talks ([more](https://github.com/jodydonetti/talks))

Are you more into videos?

Along the years I've been lucky enough to be invited to some conferences, shows or podcasts both online and around the world, of course to talk about all things caching and FusionCache.

A good example is when the fine folks at [On .NET](https://learn.microsoft.com/en-us/shows/on-net/) invited me on the show to allow me to mumbling random caching stuff.

[![On .NET Talk](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/images/talks/on-dotnet-small.jpg)](https://github.com/jodydonetti/talks)

You can find most of them, sometimes with the related slides, in the dedicated repo [here](https://github.com/jodydonetti/talks).

## ✔ Features

FusionCache has a lot of features, let's see them grouped together:

#### Resiliency
- [**🛡️ Cache Stampede**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md): automatic protection from the Cache Stampede problem
- [**💣 Fail-Safe**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md): a mechanism to avoids transient failures, by reusing an expired entry as a temporary fallback
- [**↩️ Auto-Recovery**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md): self-healing for the entire cache, all automatically

#### Performance & Scalability
- [**🔀 L1+L2**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md): any implementation of `IDistributedCache` can be used as an optional 2nd level, all transparently
- [**📢 Backplane**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md): in a multi-node scenario, it can notify the other nodes about changes in the cache, so all will be in-sync
- [**⏱ Soft/Hard Timeouts**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md): a slow factory (or distributed cache) will not slow down your application, and no data will be wasted
- [**🦅 Eager Refresh**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/EagerRefresh.md): start a non-blocking background refresh before the expiration occurs
- [**🔂 Conditional Refresh**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md): like HTTP Conditional Requests, but for caching
- [**🚀 Background Distributed Operations**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/BackgroundDistributedOperations.md): distributed operations can easily be executed in the background, safely, for better performance

#### Flexibility
- [**📛 Named Caches**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md): easily work with multiple named caches, even if differently configured
- [**🏷️ Tagging**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md): tags can be associated to entries, to later expire them all at once
- [**🧼 Clear**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Clear.md): clear an entire cache, even with shared L2, cache key prefix, etc
- [**Ⓜ️ Microsoft HybridCache**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/MicrosoftHybridCache.md): can be used as an implementation of the new HybridCache abstraction from Microsoft, all while adding extra features
- [**🧙‍♂️ Adaptive Caching**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md): for when you don't know upfront the entry options (eg: `Duration`), since they depends on the value being cached itself
- [**🔃 Dependency Injection + Builder**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/DependencyInjection.md): native support for Dependency Injection, with a nice fluent interface including a Builder support
- [**♊ Auto-Clone**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoClone.md): be sure that cached values returned can be safely modified
- [**💫 Fully sync/async**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CoreMethods.md): native support for both the synchronous and asynchronous programming model
- [**🧩 Plugins**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Plugins.md): extend FusionCache with additional behavior like adding support for metrics, statistics, etc...

#### Observability
- [**🔭 OpenTelemetry**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/OpenTelemetry.md): native observability support via OpenTelemetry
- [**📜 Logging**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md): comprehensive, structured and customizable, via the standard `ILogger` interface
- [**📞 Events**](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Events.md): a comprehensive set of events, both at a high level and at lower levels (memory/distributed)

## Ⓜ️ Microsoft HybridCache

We've probably all heard about the new kid on the block introduced by Microsoft with .NET 9: `HybridCache`.

So what does it mean for FusionCache? Does one replace the other? Or can they somehow work together?

It's pretty cool actually, so let's [find out](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/MicrosoftHybridCache.md)!

## ⭐ Quick Start

Just install the `ZiggyCreatures.FusionCache` Nuget package:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache
```

Then, let's say we have a method that loads a product from the database:

```csharp
Product GetProductFromDb(int id) {
	// DATABASE CALL HERE
}
```

(This is using the **sync** programming model, but it would be equally valid with the newer **async** one)

Then we create a FusionCache instance:

```csharp
var cache = new FusionCache(new FusionCacheOptions());
```

or, if using [dependency injection](docs/DependencyInjection.md):

```csharp
services.AddFusionCache();
```

Now, to get the product from the cache and, if not there, get it from the database in an optimized way and cache it for `30 sec`:

```csharp
var id = 42;

cache.GetOrSet<Product>(
	$"product:{id}",
	_ => GetProductFromDb(id),
	TimeSpan.FromSeconds(30)
);
```

That's it.

## 🖥️ Simulator

Distributed systems are, in general, quite complex to understand.

When using FusionCache with the [distributed cache](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md), the [backplane](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md) and [auto-recovery](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md) the Simulator can help us **seeing** the whole picture.

[![FusionCache Simulator](https://raw.githubusercontent.com/ZiggyCreatures/FusionCache/main/docs/images/fusioncache-simulator-autorecovery.png)](docs/Simulator.md)

## 🧰 Supported Platforms

FusionCache targets `.NET Standard 2.0` so any compatible .NET implementation is fine: this means `.NET Framework` (the old one), `.NET Core 2+` and `.NET 5/6+` (the new ones), `Mono` 5.4+ and more (see [here](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support) for a complete rundown).

**NOTE**: if you are running on **.NET Framework 4.6.1** and want to use **.NET Standard** packages Microsoft suggests to upgrade to .NET Framework 4.7.2 or higher (see the [.NET Standard Documentation](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support)) to avoid some known dependency issues.

## 🆎 Comparison

There are various alternatives out there with different features, different performance characteristics (cpu/memory) and in general a different set of pros/cons.

A [feature comparison](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Comparison.md) between existing .NET caching solutions may help you choose which one to use.

## 💼 Is it Production Ready :tm: ?

Yes!

FusionCache is being used **in production** on **real world projects** for years, happily handling billions of requests.

Considering that the FusionCache packages have been downloaded more than **46 million times** (thanks everybody!) it may very well be used even more.

Oh, and it is being used in products by Microsoft itself, like [Data API Builder](https://devblogs.microsoft.com/azure-sql/data-api-builder-ga/)!

## 😍 Are you using it?

If you find FusionCache useful please [let me know](https://github.com/ZiggyCreatures/FusionCache/discussions/new?category=show-and-tell&title=I%27m%20using%20FusionCache!&body=%23%23%20Scenario%0ADescribe%20how%20you%20are%20using%20FusionCache:%20commercial%20product,%20oss%20project,%20monolith,%20microservice,%20web,%20mobile%20app,%20CLI,%20etc%0A%0A%23%23%20Liked%0AWhat%20you%20liked,%20features,%20docs%20or%20anything%0A%0A%23%23%20Unliked/Missing%0AThings%20you%20didn%27t%20like%20or%20felt%20was%20missing%0A%0A%23%23%20Closing%20Thoughts%0AAny%20closing%20thoughts), I'm  interested in knowing your use case!

This is the only way for me to know how it is helping people.

## 💰 Support

Nothing to do here.

After years of using a lot of open source stuff for free, this is just me trying to give something back to the community.

Will FusionCache one day switch to a commercial model? Nope, not gonna happen.

Mind you: nothing against other projects making the switch, if done in a proper way, but no thanks not interested. And FWIW I don't even accept donations, which are btw a great thing: that should tell you how much I'm into this for the money.

Again, this is me trying to give something back to the community.

If you really want to talk about money, please consider making  **🩷 a donation to a good cause** of your choosing, and let me know about that.
