<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :stopwatch: Timeouts

There are different types of timeouts available and it may be useful to know them.

:bulb: For a complete example of how to use them and what results you can achieve there's the [:woman_teacher: Step By Step](StepByStep.md) guide.
## Factory Timeouts

Sometimes your data source (database, webservice, etc) is overloaded, the network is congested or something else bad is happening and the end result is things start to get **:snail: very slow** to get a fresh piece of data.

Wouldn't it be nice if there could be a way to simply let FusionCache temporarily reuse an expired cache entry if the factory is taking too long?

Enter **soft/hard timeouts**.

You can specify:

- `FactorySoftTimeout`: to be used if there's an expired cache entry to use as a fallback

- `FactoryHardTimeout`: to be used in any case, no matter what. In this last case an exception will be thrown and you will have to handle it yourself, but in some cases that would be more preferable than a very slow response

You can specify them both (the **soft** should be lower than the **hard**, of course) and the appropriate one will be used based on the presence of an expired entry to be eventually used as a fallback.

In both cases it is possible to set the bool flag `AllowTimedOutFactoryBackgroundCompletion`: it is enabled *by default*, so you don't have to do anything, and it lets the timed-out factory keep running in the background and update the cached value as soon as it finishes. This will give you the best of both worlds: a **fast response** and **fresh data** as soon as possible.

### :bulb: Example
As an example let's say we have a piece of code like this:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(2))
);
```

In this **very hypothetical and made up scenario** (don't look too much into the numbers, they just serve as a reference point) we may have a timeline like this:

![Timeline Without Timeouts](images/timeouts-timeline-blocking.png)

As you can see in this case the factory is taking around `300 ms`, and the entire method call is taking around `400 ms`, which is too much for us.

So we simply enable **fail-safe** and specify a **soft timeout** of `100 ms`:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(2))
        // ENABLE FAIL-SAFE
        .SetFailSafe(true)
        // SET A 100 MS SOFT TIMEOUT
        .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100))
);
```

The end result is this:

![Timeline With Timeouts](images/timeouts-timeline-background.png)

Now after `100 ms` the factory will timeout and FusionCache will **temporarily** give us back the expired value, in about `200 ms` total.

Also, it will complete the factory execution **in the background**: as soon as it will complete, the cached value **will be updated** so that any new request will have the fresh value ready to be used.


## Distributed Cache Timeouts

When using a distributed cache it is also possible to observe some slowdowns in case of network congestions or something else.

In this case it may be useful to also set soft/hard timeouts for the distributed cache (`DistributedCacheSoftTimeout` and `DistributedCacheHardTimeout`) for the operations that must be awaited (like getting a value), whereas the non critical ones (like saving a value) can simply run in the background, so a timeout is less important.

One last flag available in this space is `AllowBackgroundDistributedCacheOperations`: this will execute most operations on the distributed cache in the background like in a *fire-and-forget* way, so you will get most likely a perf boost.

Usually you can enable this without problems, but **remember** that if you somehow bypass FusionCache and directly check the distributed cache right after the method call is completed, maybe the distributed cache may not be updated yet.

Here's an example of such a particular scenario:

```csharp
// INSTANTIATE REDIS AS A DISTRIBUTED CACHE
var redis = new RedisCache(new RedisCacheOptions() { Configuration = "YOUR CONNECTION STRING HERE" });

// INSTANTIATE THE FUSION CACHE SERIALIZER
var serializer = new FusionCacheNewtonsoftJsonSerializer();

// INSTANTIATE FUSION CACHE
var cache = new FusionCache(new FusionCacheOptions());

// SETUP THE DISTRIBUTED 2ND LAYER
cache.SetupDistributedCache(redis, serializer);

// SET A VALUE IN THE CACHE VIA FUSION CACHE, WITH BACKGROUND DISTRIBUTED OPERATIONS
cache.Set<string>(
    "foo",
    "Sloths, sloths everywhere",
    new FusionCacheEntryOptions { AllowBackgroundDistributedCacheOperations = true }
);

// HERE foo MAY BE NULL, BECAUSE THE DISTRIBUTED CACHE MAY STILL BE SAVING THE VALUE IN THE BACKGROUND
var foo = redis.GetString("foo");
```
