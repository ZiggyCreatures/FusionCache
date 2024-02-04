<div align="center">

![FusionCache logo](logo-128x128.png)

</div>


# 📕 Documentation

Sometimes topics can be explained a little bit more, and the following docs can help you with that.

## [**🦄 A Gentle Introduction**](AGentleIntroduction.md)

What you need to know first, to make yourself comfortable with FusionCache.

## [**👩‍🏫 Step By Step**](StepByStep.md)

A complete step by step example of why a cache is useful, why FusionCache could be even more so, how to apply most of the options available and what results you can expect to obtain.


## [**🆎 Comparison**](Comparison.md)

A feature comparison between existing .NET caching solutions, to  help you choose which one to use.

## 📖 Features

A deeper description of the main features:

- [**🛡️ Cache Stampede prevention**](CacheStampede.md): automatic protection from the Cache Stampede problem
- [**🔀 Optional 2nd level**](CacheLevels.md): an optional 2nd level handled transparently, with any implementation of `IDistributedCache`
- [**💣 Fail-Safe**](FailSafe.md): a mechanism to avoids transient failures, by reusing an expired entry as a temporary fallback
- [**⏱ Soft/Hard timeouts**](Timeouts.md): a slow factory (or distributed cache) will not slow down your application, and no data will be wasted
- [**📢 Backplane**](Backplane.md): in a multi-node scenario, it can notify the other nodes about changes in the cache, so all will be in-sync
- [**↩️ Auto-Recovery**](AutoRecovery.md): automatic handling of transient issues with retries and sync logic
- [**🧙‍♂️ Adaptive Caching**](AdaptiveCaching.md): for when you don't know upfront the cache duration, as it depends on the value being cached itself
- [**🔂 Conditional Refresh**](ConditionalRefresh.md): like HTTP Conditional Requests, but for caching
- [**🦅 Eager Refresh**](EagerRefresh.md): start a non-blocking background refresh before the expiration occurs
- [**🔃 Dependency Injection**](DependencyInjection.md): native support for Dependency Injection, with a nice fluent interface including a Builder support
- [**📛 Named Caches**](NamedCaches.md): easily work with multiple named caches, even if differently configured
- [**🔭 OpenTelemetry**](OpenTelemetry.md): native observability support via OpenTelemetry
- [**📜 Logging**](Logging.md): comprehensive, structured and customizable, via the standard `ILogger` interface
- [**💫 Natively sync/async**](CoreMethods.md): native support for both the synchronous and asynchronous programming model
- [**📞 Events**](Events.md): a comprehensive set of events, both at a high level and at lower levels (memory/distributed)
- [**🧩 Plugins**](Plugins.md): extend FusionCache with additional behavior like adding support for metrics, statistics, etc...
