<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🆎 Comparison

FusionCache of course is not the only player in the field of caching libraries.

Any alternative out there has different features, different performance characteristics (cpu/memory) and in general a different set of pros/cons.

The caching libraries I've looked at are (in alphabetical order):

- [**CacheManager**](https://github.com/MichaCo/CacheManager)
- [**CacheTower**](https://github.com/TurnerSoftware/CacheTower)
- [**EasyCaching**](https://github.com/dotnetcore/EasyCaching)
- [**HybridCache**](https://github.com/dotnet/aspnetcore/issues/53255)
- [**LazyCache**](https://github.com/alastairtree/LazyCache)

All of them are good overall, and all have a different set of features.

In the end, any library out there is the materialization of a lot of **passion**, **time** and **efforts** of somebody who decided to take their experience, model it into a reusable piece of code, document it and release it for others to study and use, all for free.

And we should all be thankful for that.

| 📢 A note to other library authors |
| :--- |
| Even though I tried my best to be fair and objective, I'm sure you may have different opinions about each topic or I may have just used your library in the wrong way or maybe a newer version came out with new features. <br/> <br/> If that is the case please [**open an issue**](https://github.com/ZiggyCreatures/FusionCache/issues/new/choose) or send a pr and I will make the necessary changes. |

## ☑ Features

Every library has a different set of features, mostly based on each library design: you may find logging support, both a sync and an async api, statistics, events, jittering, cache regions and a lot more so making a 1:1 comparison is hardly possible.

In trying to make an honest and generic overview I've identified a set of **very high-level features** that I think are important: of course your mileage may vary, and you are welcome to **give me your opinion** so I can expand my view and make this comparison even better.

The general features I've identified are:

- [**Cache Stampede**](CacheStampede.md): the ability to protect from Cache Stampede, meaning a guarantee that only one factory will be executed concurrently per-key, even in highly concurrent scenarios. This will reduce a lot the load on your origin datasource (database, etc)
- **Native sync/async support**: native support for both programming models. Even though nowadays the async one is more widly used and in general more performant, they are both useful and have their place in everyday usage
- [**Fail-Safe**](FailSafe.md) (or similar mechanism): in general the ability to temporarily re-use an expired entry in case it's currently not possible to get a new one. This can greatly reduce transient errors in your application
- [**Timeouts**](Timeouts.md): the ability to avoid factories to run for too long, potentially creating a blocking situation or resulting in too slow responses
- [**Adaptive Caching**](AdaptiveCaching.md): the ability to adapt the caching options for a specific cache entry based on the result of the factory execution. This may be important for example to have a cache duration that changes based on the object being cached
- [**Tagging**](Tagging.md): the ability to associate tags with entries, to later expire them all at once
- [**Clear**](Clear.md): the ability to clear an entire cache at once, even with a shared L2, a cache key prefix, etc
- [**Microsoft HybridCache**](MicrosoftHybridCache.md): the ability to be used as an implementation of the new HybridCache abstraction from Microsoft
- [**Cancellation**](https://docs.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads): the ability to cancel the execution of methods via a `CancellationToken`. This may be important in some situations to release some resources (http connections, locks, etc) when the calling code has already decided that something is not necessary anymore. It would be great to be able to cancel each method and also cancel the provided factory method itself, so the entire execution pipeline can be gracefully canceled
- [**Multi-provider**](CacheLevels.md): the abilty to use the same caching api towards different implementations (memory, Redis, MongoDb, etc)
- [**Multi-level**](CacheLevels.md): the ability to handle more than one caching level, transparently. This can give you - at the same time - the benefits of a local in-memory cache (high performance + data locality) and the benefits of a distributed cache (sharing of cached data + better cold start) without having to handle them separately
- [**Backplane**](Backplane.md): available with different names, it allows a change in a distributed cache to be reflected in the local memory cache
- [**Named Caches**](NamedCaches.md): it's possible to register multiple instances via DI and later request them via either keyed services or a different mechanism (eg: like `IFusionCacheProvider`)
- [**Auto-Recovery**](AutoRecovery.md): a way to automatically handle transient errors so that they can be solved, automatically, without doing anything
- [**Events**](Events.md): the ability to be notified when certain events happen in the cache, useful to collect custom metrics, etc
- [**OpenTelemetry**](OpenTelemetry.md): support for full observability via [OpenTelemetry](https://opentelemetry.io/)
- [**Logging**](Logging.md): when things go bad you would like to have some help investigating what went wrong, and logging is key
- **Portable**: the ability to run on both the older **.NET Framework** (full fx) and the new **.NET Core**. As time goes by .NET Core (from v5 now simply **.NET**) is the platform to be on, but it's a nice plus to be able to run on the older one as well
- **Tests**: having a suite of tests covering most of the library can greatly reduce the probabilty of bugs or regressions so, in theory, you can count on a more solid and stable library
- [**Xml Comments**](https://docs.microsoft.com/en-us/dotnet/csharp/codedoc): having informations always available at your fingertips while you type (Intellisense :tm: or similar) is fundamental for learning as you code and to avoid common pitfalls
- [**Docs**](docs/README.md): an expanded documentation, a getting started guide or maybe some samples can greatly improve your learning
- [**License**](../LICENSE.md): important to know what are your rights and obligations

This is how they compare:

|                       | FusionCache | HybridCache (3) | CacheManager | CacheTower  | EasyCaching (1) | LazyCache (2) |
| ---:                  | :---:       | :---:           | :---:        | :---:       | :---:           |:---:          |
| **Cache Stampede**    | ✔          | ✔               | ❌           | ✔          | ✔              | ✔            |
| **Sync Api**          | ✔          | ❌              | ✔            | ❌         | ✔              | ✔            |
| **Async Api**         | ✔          | ✔               | ❌           | ✔          | ✔              | ⚠            |
| **Fail-Safe**         | ✔          | ❌              | ❌           | ❌         | ❌             | ❌           |
| **Timeouts**          | ✔          | ❌              | ❌           | ❌         | ❌             | ❌           |
| **Adaptive Caching**  | ✔          | ❌              | ❌           | ❌         | ❌             | ✔            |
| **Tagging**           | ✔          | ✔               | ❌           | ❌         | ❌             | ✔            |
| **Clear**             | ✔          | ❓              | ❌           | ❌         | ❌             | ✔            |
| **MS HybridCache**    | ✔          | ✔               | ❌           | ❌         | ❌             | ✔            |
| **Cancellation**      | ✔          | ✔               | ❌           | ❌         | ❌             | ❌           |
| **Multi-provider**    | ✔          | ✔               | ✔            | ✔          | ✔              | ❌           |
| **Multi-level**       | ✔          | ✔               | ✔            | ✔          | ⚠              | ❌           |
| **Backplane**         | ✔          | ✔               | ✔            | ✔          | ✔              | ❌           |
| **Named Caches**      | ✔          | ❌              | ❌           | ❌         | ❌             | ❌           |
| **Auto-Recovery**     | ✔          | ❌              | ❌           | ❌         | ❌             | ❌           |
| **Events**            | ✔          | ❌              | ✔            | ❌         | ❌             | ❌           |
| **OpenTelemetry**     | ✔          | ❓              | ❌           | ❌         | ❌             | ❌           |
| **Logging**           | ✔          | ❌              | ✔            | ❌         | ✔              | ❌           |
| **Portable**          | ✔          | ✔               | ✔            | ✔          | ✔              | ✔            |
| **Tests**             | ✔          | ✔               | ✔            | ✔          | ✔              | ✔            |
| **Xml Comments**      | ✔          | ❌              | ✔            | ✔          | ✔              | ❌           |
| **Docs**              | ✔          | ✔               | ✔            | ✔          | ✔              | ✔            |
| **License**           | `MIT`       | `Same as .NET`  | `Apache 2.0` | `MIT`       | `MIT`           | `MIT`        |

ℹ **NOTES**
- (1): **EasyCaching** supports an `HybridCachingProvider` to handle 2 levels transparently, but it's implemented in a way that checks the distributed cache before the in-memory one, kind of invalidating the benefits of the latter, which is important to know.
- (2): **LazyCache** does have both sync and async support, but not for all the available methods (eg. `Remove`). This may be perfectly fine for you or not, but it's good to know.
- (3): **HybridCache** is not currently out yet, I'll update the table when it will be released.
