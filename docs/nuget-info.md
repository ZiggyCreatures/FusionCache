<div align="center">

![FusionCache logo](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/logo-256x256.png)

</div>

# FusionCache

### FusionCache is an easy to use, high performance and robust cache with an optional distributed 2nd layer and some advanced features.

It was born after years of dealing with all sorts of different types of caches: memory caching, distributed caching, http caching, CDNs, browser cache, offline cache, you name it. So I've tried to put togheter these experiences and came up with FusionCache.

It uses a memory cache (any impl of the standard `IMemoryCache` interface) as the **primary** backing store and optionally a distributed, 2nd level cache (any impl of the standard `IDistributedCache` interface) as a **secondary** backing store for better resilience and higher performance, for example in a multi-node scenario or to avoid the typical effects of a cold start (initial empty cache, maybe after a restart).

<div style="text-align:center;">

![FusionCache diagram](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/images/diagram.png)

</div>

FusionCache also includes some advanced features like a **fail-safe** mechanism, concurrent **factory calls optimization** for the same cache key, fine grained **soft/hard timeouts** with **background factory completion**, customizable **extensive logging** and more (see below).

If you want to get yourself **comfortable with the overall concepts** there's [:unicorn: A Gentle Introduction](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/AGentleIntroduction.md) available.

If you want to see what you can achieve **from start to finish** with FusionCache, there's a [:trophy: Step By Step ](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/StepByStep.md) guide.

If instead you want to start using it **immediately** there's a [:star: Quick Start](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/README.md#star-quick-start) for you.

## :heavy_check_mark: Features
These are the **key features** of FusionCache:

- **:rocket: Optimized factory calls**: using the optimized `GetOrSet[Async]` method prevents multiple concurrent factory calls per key, with a guarantee that only 1 factory will be called at the same time for the same key (this avoids overloading the data source when no data is in the cache or when a cache entry expires)
- **:twisted_rightwards_arrows: Optional 2nd level**: FusionCache can transparently handle an optional 2nd level cache: anything that implements the standard `IDistributedCache` interface is supported (eg: Redis, MongoDB, SqlServer, etc)
- **:bomb: Fail-Safe**: enabling the fail-safe mechanism prevents throwing an exception when a factory or a distributed cache call would fail, by reusing an expired entry as a temporary fallback, all transparently and with no additional code required ([read more](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/FailSafe.md))
- **:stopwatch: Soft/Hard timeouts**: advanced timeouts management prevents waiting for too long when calling a factory or the distributed cache. This is done to avoid that such slow calls would hang your application. It is possible to specify both *soft* and *hard* timeouts that will be used depending on whether there's a fallback value to use for the specific call or not ([read more](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/Timeouts.md))
- **:dark_sunglasses: Background factory completion**: when you specify a factory timeout and it actually occurs, the timed-out factory can keep running in the background and, if and when it successfully complete, the cache will be immediately updated with the new value to be used right away ([read more](https://raw.githubusercontent.com/jodydonetti/ZiggyCreatures.FusionCache/main/docs/Timeouts.md))
- **:zap: High performance**: FusionCache is optimized to minimize CPU usage and memory allocations to get better performance and lower the cost of your infrastructure all while obtaining a more stable, error resilient application
- **:dizzy: Natively sync/async**: full native support for both the synchronous and asynchronous programming model, without the problematic “sync over async” or “async over sync” approach
- **:page_with_curl: Extensive logging**: comprehensive, structured, detailed and customizable logging via the standard `ILogger<T>` interface (you can use Serilog, NLog, etc)

Also, FusionCache has some other nice **additional features**:

- **Portable**: targets .NET Standard 2.0
- **Null caching**: explicitly supports caching of null values differently than "no value". This creates a less ambiguous usage, and typically leads to better performance because it avoids the classic problem of not being able to differentiate between *"the value was not in the cache, go check the database"* and *"the value was in the cache, and it was `null`"*
- **Distributed cache circuit-breaker**: it is possible to enable a simple circuit-breaker for when a distributed cache becomes temporarily unavailable. This will prevent the distributed cache to be hit with an additional load of requests (that would probably fail anyway) in a problematic moment, so it can gracefully get back on its feet. More advanced scenarios can be covered using a dedicated solution, like <a href="https://github.com/App-vNext/Polly">Polly</a>
- **Dynamic Jittering**: setting `JitterMaxDuration` will add a small randomized extra duration to a cache entry's normal duration. This is useful to prevent variations of the <a href="https://en.wikipedia.org/wiki/Thundering_herd_problem">Thundering Herd problem</a> in a multi-node scenario
- **Hot Swap**: supports thread-safe changes of the entire distributed cache implementation (add/swap/removal)
- **Code comments**: every property and method is fully documented in code, with useful informations provided via IntelliSense or similar technologies
- **Fully annotated for nullability**: every usage of nullable references has been annotated for a better flow analysis by the compiler

## 🧰 Supported Platforms

FusionCache targets .NET Standard 2.0, so any compatible .NET implementation is fine.

**NOTE**: if you are running on **.NET Framework 4.6.1** and want to use **.NET Standard** packages Microsoft suggests to upgrade to .NET Framework 4.7.2 or higher (see the [.NET Standard Documentation](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support)) to avoid some known dependency issues.