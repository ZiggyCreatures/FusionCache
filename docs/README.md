﻿<div align="center">

![FusionCache logo](logo-128x128.png)

</div>


# :book: Documentation

Sometimes topics can be explained a little bit more.


### [**🦄 A Gentle Introduction**](AGentleIntroduction.md)

What you need to know first, to make yourself comfortable with FusionCache.


### Features

A deeper description of the main features:

- [**🔀 Cache Levels**](CacheLevels.md): a bried description of the 2 available caching levels and how to setup them
- [**📢 Backplane**](Backplane.md): how to get an always synchronized cache, even in a multi-node scenario
- [**🚀 Cache Stampede prevention**](CacheStampede.md): no more overloads during a cold start or after an expiration
- [**💣 Fail-Safe**](FailSafe.md): an explanation of how the fail-safe mechanism works
- [**⏱ Timeouts**](Timeouts.md): the various types of timeouts at your disposal (calling a factory, using the distributed cache, etc)
- [**🔃 Dependency Injection**](DependencyInjection.md): how to work with FusionCache + DI in .NET
- [**📛 Named Caches**](NamedCaches.md): how to work with multiple named FusionCache instances
- [**🧙‍♂️ Adaptive Caching**](AdaptiveCaching.md): how to adapt cache duration (and more) based on the object being cached itself
- [**🔂 Conditional Refresh**](ConditionalRefresh.md): how to save resources when the remote data is not changed
- [**🦅 Eager Refresh**](EagerRefresh.md): how to start a background refresh eagerly, before the expiration occurs
- [**🎚 Options**](Options.md): everything about the available options, both cache-wide and per-call
- [**🕹 Core Methods**](CoreMethods.md): what you need to know about the core methods available
- [**📞 Events**](Events.md): the events hub and how to use it
- [**🧩 Plugins**](Plugins.md): how to create and use plugins
- [**📜 Logging**](Logging.md): logging configuration and usage


### [**👩‍🏫 Step By Step**](StepByStep.md)

A complete step by step example of why a cache is useful, why FusionCache could be even more so, how to apply most of the options available and what results you can expect to obtain.


### [**🆎 Comparison**](Comparison.md)

A feature comparison between existing .NET caching solutions, to  help you choose which one to use.
