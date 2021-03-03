<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :ab: Comparisons / Benchmarks

FusionCache of course is not the only player in the field of caching libraries.

Any alternative out there has different features, different performance characteristics (cpu usage and memory consumption) and in general a different set of pros/cons.

The caching libraries I've looked at are (in alphabetical order):

- [**CacheManager**](https://github.com/MichaCo/CacheManager)
- [**CacheTower**](https://github.com/TurnerSoftware/CacheTower)
- [**EasyCaching**](https://github.com/dotnetcore/EasyCaching)
- [**LazyCache**](https://github.com/alastairtree/LazyCache)

All of them are good overall, and all have a different set of features.

In the end, any library out there is the materialization of a lot of **passion**, **time** and **efforts** of somebody who decided to take their experience, model it into a reusable piece of code, document it and release it for others to study and use, all for free.

And we should all be thankful for that.

| :loudspeaker: A note to other library authors |
| :--- |
| Even though I tried my best to be fair and objective, I'm sure you may have different opinions on each topic or I may have just used your library in the wrong way in the benchmarks. <br/> <br/> If that is the case please [**:envelope: let me know**](https://twitter.com/jodydonetti) and I will make the necessary changes. |

## :ballot_box_with_check: Features

Every library has a different set of features, mostly based on each library design: you may find logging support, both sync and async api, statistics, cache regions and a lot more.

In trying to make an honest and generic overview I've identified **a set of features** that I think are important: of course your mileage may vary, and you are welcome to **give me your opinions** so I can expand my view and make this comparison even better.

The general features I've identified as significants are:

- **Factory call optimization**: the ability to guarantee only one factory will be executed concurrently per-key, even in highly concurrent scenarios. This will reduce a lot the load on your origin datasource (database, etc)

- **Native sync/async support**: native support for both programming models. Even though nowadays the async one is more widly used and in general more performant, they are both useful and have their place in everyday usage

- **Stale data re-use**: the ability to temporarily re-use an expired entry in case it's not possible to get a new one. This can greatly reduce transient errors in your application

- **Multi-provider**: the abilty to use the same caching api towards different implementations (memory, Redis, MongoDb, etc)

- **Multi-level**: the ability to handle more than one caching level, transparently. This can give you - at the same time - the benefits of a local in-memory cache (high performance + data locality) and the benefits of a distributed cache (sharing of cached data + better cold start) without having to handle them separately

- **Logging**: when things go bad you would like to have some help investigating what went wrong, and logging is key

- **Portable**: the ability to run on both the older **.NET Framework** (full fx) and the new **.NET Core**. As time goes by .NET Core (from v5 now simply **.NET**) is the platform to be on, but it's a nice plus to be able to run on the older one as well

- **Tests**: having a suite of tests covering most of the library can greatly reduce the probabilty of bugs or regressions so, in theory, you can count on a more solid and stable library

- **Xml comments**: having informations always available at your fingertips [while you type](https://docs.microsoft.com/en-us/dotnet/csharp/codedoc) (Intellisense :tm: or similar) is fundamental for learning as you code and to avoid common pitfalls

- **Documentation**: an expanded documentation, a getting started guide or maybe some samples can greatly improve your learning

- **License**: important to know what are your rights and obligations

This is how they compare:

|                       | FusionCache | CacheManager | CacheTower (1) | EasyCaching (2) | LazyCache (3) |
| ---:                  | :---:       | :---:        | :---:          | :---:           |:---:          |
| **Factory call opt.** | ✔          | ❌           | ⚠             | ✔               | ✔            |
| **Sync usage**        | ✔          | ✔            | ❌            | ✔               | ✔            |
| **Async usage**       | ✔          | ❌           | ✔             | ✔               | ⚠            |
| **Stale data re-use** | ✔          | ❌           | ✔             | ❌              | ❌           |
| **Multi-provider**    | ✔          | ✔            | ✔             | ✔               | ❌           |
| **Multi-level**       | ✔          | ✔            | ✔             | ⚠              | ❌           |
| **Logging**           | ✔          | ✔            | ❌            | ✔              | ❌           |
| **Portable**          | ✔          | ✔            | ✔             | ✔               | ✔            |
| **Tests**             | ✔          | ✔            | ✔             | ✔               | ✔            |
| **Xml comments**      | ✔          | ✔            | ❌            | ✔               | ❌           |
| **Documentation**     | ✔          | ✔            | ✔             | ✔               | ✔            |
| **License**           | `MIT`       | `Apache 2.0` | `MIT`          | `MIT`           | `MIT`         |

:information_source: **NOTES**
- (1): **CacheTower** does have a factory call optimization, but (currently) seems to deadlock in some scenarios with multiple accessors
- (2): **EasyCaching** supports an HybridCachingProvider to handle 2 layers transparently, but it's implemented in a way that checks the distributed cache before the in-memory one, kind of invalidating the benefits of the latter, which is important to know
- (3): **LazyCache** does have both sync and async support, but not for all the available methods (eg. `Remove`). This may be perfectly fine for you or not, but it's up to you

## :checkered_flag: Benchmarks

### Rationale

There are a lot of different operations and a lot of different scenarios to benchmark, to fully compare the various caching libraries.

The one operation though that in my opinion is the most significant to measure is the one commonly known as `GetOrSet`/`GetOrAdd`.

This operation executes various steps that can reveal how a library performs:

- it tries to get something from the cache (a **READ**)
- if not there, it calls the provided factory to get the value (concurrent factory execution, maybe with a **LOCK**)
- saves it in the cache (a **WRITE**)

I also simulated the typical access pattern of a highly loaded web server by executing this operation in a *double parallel loop* on multiple cache keys and, for each key, with multiple concurrent accessors.

You can look at the code [here](https://github.com/jodydonetti/ZiggyCreatures.FusionCache/blob/main/benchmarks/ZiggyCreatures.FusionCache.Benchmarks/AsyncCacheComparisonBenchmark.cs), and it goes like this:

```csharp
var tasks = new ConcurrentBag<Task>();

Parallel.ForEach(Keys, key =>
{
    Parallel.For(0, Accessors, _ =>
    {
        var t = cache.GetOrSetAsync<T>(
            key,
            async ct =>
            {
                // FACTORY LOGIC HERE
            }
        );
        tasks.Add(t);
    });
});

await Task.WhenAll(tasks).ConfigureAwait(false);
```

I only use a **local cache** (in-memory) to reduce external factors, **do not enable** any advanced feature (fail-safe, timeouts, logging, etc) to be as balanced as possible between the different caching libraries.

I also introduced a small **delay** in the factory to simulate the actual time it would take to talk to a database or similar, which can make the factory execution strategy of each library more evident.

Finally, the benchmarks have been created with the amazing [**BenchmarkDotNet**](https://github.com/dotnet/BenchmarkDotNet) which is as awesome as it can get, and they have been run on a machine with this specs:

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.1411 (1909/November2018Update/19H2)
Intel Core i7-3770K CPU 3.50GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.103
  [Host]     : .NET Core 3.1.12 (CoreCLR 4.700.21.6504, CoreFX 4.700.21.6905), X64 RyuJIT
  DefaultJob : .NET Core 3.1.12 (CoreCLR 4.700.21.6504, CoreFX 4.700.21.6905), X64 RyuJIT
```

Of course running the benchmarks with different parameters and on different machines would lead to different results.

### Results

The most important columns to look at are `Ratio` and `Alloc`:

- `Ratio`: it's a proportion of how fast a library is compared to the baseline (in this case FusionCache). For example a value of `1.10` means that lib is `10%` slower than FusionCache, a value of `0.90` means it is `10%` faster and a value of, say, `10` means it's 10 times (not 10%, but 1.000%) slower
- `Alloc`: is the amount of memory allocated for the entire run, in KB. Of course the lower the better


### Async version

|       Method | Accessors | Rounds | Mean (ms) | Error (ms) | StdDev (ms) | Median (ms) | P95 (ms) | Ratio | Alloc (KB) |
|-------------:|:---------:|:------:|----------:|-----------:|------------:|------------:|---------:|------:|-----------:|
|  FusionCache |        10 |      1 |     61.98 |       0.38 |        0.35 |       62.08 |    62.26 |  1.00 |    1732.99 |
| CacheManager |        10 |      1 |    557.42 |      17.54 |       45.90 |      558.76 |   610.63 |  9.29 |     621.66 |
|  EasyCaching |        10 |      1 |    308.90 |       3.78 |        3.35 |      309.30 |   313.22 |  4.99 |    1665.23 |
|    LazyCache |        10 |      1 |     61.93 |       0.39 |        0.36 |       62.06 |    62.27 |  1.00 |    1103.96 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |        10 |     50 |     93.01 |       0.76 |        0.71 |       93.14 |    93.65 |  1.00 |   29387.49 |
| CacheManager |        10 |     50 |    751.67 |      59.09 |      169.56 |      725.20 | 1,127.47 |  9.16 |   16311.44 |
|  EasyCaching |        10 |     50 |    356.11 |       3.44 |        3.05 |      356.13 |   359.61 |  3.83 |   36400.13 |
|    LazyCache |        10 |     50 |     93.88 |       1.02 |        0.91 |       93.53 |    95.40 |  1.01 |   41831.50 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |       100 |      1 |     75.25 |       1.40 |        1.31 |       75.26 |    76.86 |  1.00 |   10761.00 |
| CacheManager |       100 |      1 |    581.46 |      15.52 |       42.76 |      562.24 |   658.74 |  7.60 |    1414.34 |
|  EasyCaching |       100 |      1 |    323.20 |       3.42 |        3.03 |      323.67 |   326.05 |  4.29 |   13667.62 |
|    LazyCache |       100 |      1 |     68.00 |       1.31 |        1.35 |       67.76 |    69.83 |  0.90 |    7580.27 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |       100 |     50 |    339.08 |       3.81 |        3.38 |      338.51 |   343.69 |  1.00 |  199070.79 |
| CacheManager |       100 |     50 |    897.31 |      56.60 |      153.03 |      935.97 | 1,089.13 |  2.59 |   55129.38 |
|  EasyCaching |       100 |     50 |    795.09 |       8.46 |        7.06 |      797.05 |   803.15 |  2.34 |  271082.52 |
|    LazyCache |       100 |     50 |    427.25 |       8.16 |        8.01 |      429.44 |   438.12 |  1.26 |  322621.61 |

### Sync version

|       Method | Accessors | Rounds | Mean (ms) | Error (ms) | StdDev (ms) | Median (ms) | P95 (ms) | Ratio | Alloc (KB) |
|-------------:|:---------:|:------:|----------:|-----------:|------------:|------------:|---------:|------:|-----------:|
|  FusionCache |        10 |      1 |     231.1 |      14.83 |       42.08 |       247.9 |    303.1 |  1.00 |     840.05 |
| CacheManager |        10 |      1 |     265.9 |      17.75 |       52.33 |       248.3 |    356.3 |  1.20 |     554.76 |
|  EasyCaching |        10 |      1 |   2,779.0 |     226.72 |      668.48 |     3,010.2 |  3,330.8 | 12.42 |     740.96 |
|    LazyCache |        10 |      1 |     207.7 |      20.74 |       61.14 |       175.9 |    309.2 |  0.94 |     817.21 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |        10 |     50 |     257.5 |      25.15 |       73.75 |       239.5 |    402.5 |  1.00 |   16168.52 |
| CacheManager |        10 |     50 |     304.6 |      18.93 |       54.91 |       301.1 |    400.6 |  1.28 |   16350.79 |
|  EasyCaching |        10 |     50 |   2,930.3 |     215.63 |      635.80 |     3,091.7 |  3,813.9 | 12.38 |   25861.98 |
|    LazyCache |        10 |     50 |     239.4 |      19.81 |       57.77 |       225.9 |    340.6 |  1.01 |   32873.77 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |       100 |      1 |     225.8 |      17.53 |       50.59 |       241.5 |    315.3 |  1.00 |    1620.23 |
| CacheManager |       100 |      1 |     250.1 |      12.19 |       34.98 |       251.7 |    314.5 |  1.15 |    1317.66 |
|  EasyCaching |       100 |      1 |   2,880.4 |     198.82 |      586.22 |     3,030.2 |  3,613.0 | 13.55 |    3435.03 |
|    LazyCache |       100 |      1 |     231.6 |      17.38 |       50.98 |       232.0 |    315.0 |  1.07 |    4516.67 |
|              |           |        |           |            |             |             |          |       |            |
|  FusionCache |       100 |     50 |     303.2 |      24.37 |       70.71 |       310.4 |    419.7 |  1.00 |   54983.16 |
| CacheManager |       100 |     50 |     387.8 |      22.70 |       65.13 |       406.7 |    485.5 |  1.34 |   55093.08 |
|  EasyCaching |       100 |     50 |   2,917.8 |     240.93 |      710.39 |     3,113.7 |  3,791.8 | 10.18 |  159445.31 |
|    LazyCache |       100 |     50 |     356.8 |      18.76 |       54.43 |       370.3 |    426.0 |  1.24 |  221217.07 |

:information_source: **NOTES**

**CacheTower** performed pretty well but, because of the (current) deadlock problem mentioned above, I was not able to conclude the benchmarks. As soon as a fix will be available I will happily add it.