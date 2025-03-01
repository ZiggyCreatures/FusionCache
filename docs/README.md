﻿<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 📕 Documentation

Sometimes topics can be explained a little bit more, and the following docs can help you with that.

## [**🦄 A Gentle Introduction**](AGentleIntroduction.md)

Make yourself comfortable with FusionCache.

## [**👩‍🏫 Step By Step**](StepByStep.md)

A complete step by step example of why a cache is useful, why FusionCache could be even more so, how to apply most of the options available and what results you can expect to obtain.

## [**🆎 Comparison**](Comparison.md)

A feature comparison between existing .NET caching solutions, to  help you choose which one to use.

## [**🧬 Diagrams**](Diagrams.md)

Sometimes it's nice to be able to visualize the internal flow of a system, even more so for such a complex beast as an hybrid cache like FusionCache.

So, diagrams!

## [**🎚️ Options**](Options.md)

How global and entry options work in FusionCache, how to use them better and things to know.

## ✔️ Features

FusionCache has a lot of features, let's see them grouped together:

### Resiliency

- [**🛡️ Cache Stampede**](CacheStampede.md): automatic protection from the Cache Stampede problem
- [**💣 Fail-Safe**](FailSafe.md): a mechanism to avoids transient failures, by reusing an expired entry as a temporary fallback
- [**↩️ Auto-Recovery**](AutoRecovery.md): self-healing for the entire cache, all automatically

### Performance & Scalability

- [**🔀 L1+L2**](CacheLevels.md): any implementation of `IDistributedCache` can be used as an optional 2nd level, all transparently
- [**📢 Backplane**](Backplane.md): in a multi-node scenario, it can notify the other nodes about changes in the cache, so all will be in-sync
- [**⏱ Soft/Hard Timeouts**](Timeouts.md): a slow factory (or distributed cache) will not slow down your application, and no data will be wasted
- [**🦅 Eager Refresh**](EagerRefresh.md): start a non-blocking background refresh before the expiration occurs
- [**🔂 Conditional Refresh**](ConditionalRefresh.md): like HTTP Conditional Requests, but for caching
- [**🚀 Background Distributed Operations**](BackgroundDistributedOperations.md): distributed operations can easily be executed in the background, safely, for better performance

### Flexibility

- [**📛 Named Caches**](NamedCaches.md): easily work with multiple named caches, even if differently configured
- [**🏷️ Tagging**](Tagging.md): tags can be associated to entries, to later expire them all at once
- [**🧼 Clear**](Clear.md): clear an entire cache, even with shared L2, cache key prefix, etc
- [**Ⓜ️ Microsoft HybridCache**](MicrosoftHybridCache.md): can be used as an implementation of the new HybridCache abstraction from Microsoft, all while adding extra features
- [**🧙‍♂️ Adaptive Caching**](AdaptiveCaching.md): for when you don't know upfront the entry options (eg: `Duration`), since they depends on the value being cached itself
- [**🔃 Dependency Injection + Builder**](DependencyInjection.md): native support for Dependency Injection, with a nice fluent interface including a Builder support
- [**♊ Auto-Clone**](AutoClone.md): be sure that cached values returned can be safely modified
- [**💫 Fully sync/async**](CoreMethods.md): native support for both the synchronous and asynchronous programming model
- [**🧩 Plugins**](Plugins.md): extend FusionCache with additional behavior like adding support for metrics, statistics, etc...

### Observability

- [**🔭 OpenTelemetry**](OpenTelemetry.md): native observability support via OpenTelemetry
- [**📜 Logging**](Logging.md): comprehensive, structured and customizable, via the standard `ILogger` interface
- [**📞 Events**](Events.md): a comprehensive set of events, both at a high level and at lower levels (memory/distributed)
