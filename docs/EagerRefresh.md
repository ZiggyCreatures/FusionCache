<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🦅 Eager Refresh

FusionCache already has advanced [timeouts](Timeouts.md) features, so that a slow factory cannot slow down our code while refreshing the data.

A different approach we may take is to just start refreshing earlier (before expiration occurs) and in the background so not to slow down the normal flow.

Enter **Eager Refresh**.

## How

The idea is that it's possible to specify a so called `EagerRefreshThreshold`, which indicates after what *percentage* of our `Duration` a new request would start a background refresh: if a request arrives after that threshold (but before the data is expired) then a background refresh kicks in, without slowing down your normal operations and, as soon as finished, will update the data in the cache.

As is common in these situations the percentage is expressed as a `float` ranging from `> 0.0` to `< 1.0`, where `0.5` means `50%`, `0.9` means `90%` and so on.

Of course only the FIRST request will trigger an eager refresh, because FusionCache protects us from [Cache Stampede](CacheStampede.md) even in this case.

## Only When Actively Used

It's important to understand how eager refresh works: it does not start a timer or something similar, but it will act only IF a request comes in after the specified threshold.

For example if we set it to `0.8`, we are NOT saying *"after 80% of the `Duration` start refreshing in the background"*, instead we are saying *"IF a request comes in after 80% of the `Duration` (and before expiration), start refreshing in the background"*.

This is because we  only want to act on data that is actively used: a simple timer would basically turn a piece of cached data into something that would stay in the cache forever, no matter what. Our cache would grow indefinitely and without control, which is of course really bad.

## Valid Range

The valid range is any value `> 0.0` and `< 1.0`.

Values outside of this range (`<= 0.0` or `>= 1.0`) are automatically turned into `null`, meaning *"no eager refresh"*.

**ℹ NOTE:** typically a good value is `0.8` or above, meaning *"if a request comes in after 80% of the Duration, start a background refresh"*. Very low values like `0.1` (`10%`) would result in a constant refresh cycle, probably overloading your database.

## Why no TimeSpan?

So why the threshold has been modeled as a *percentage*, and not a fixed value like a `TimeSpan`?

Because in FusionCache we have this really nice thing called `DefaultEntryOptions`, which serves as a default set of [options](Options.md), acting as a "starting point" from where each call's options can then be changed as needed. If we want to enable fail-safe for every call, we just set it in the `DefaultEntryOptions`. If we also want some timeouts? Same thing.

But setting an eager threshold directly as a `TimeSpan` would mean making it a fixed value, instead of one relative to each call's `Duration`.

Suppose we specify some defaults, lke a `Duration` of `10 min` and an eager refresh threshold of `9 min`: if in a specific call we then set the `Duration` to `1 hour` without also remembering to change the eager refresh threshold to `90%` of that (`54 min`), the eager refresh would remain at `9 min`. Basically we would have a piece of data cached for `1 hour` but refreshed after just `9 min`, most definitely not what we want.

Something interesting to note is that the Akamai CDN took the same approach for their [Cache Prefresh](https://techdocs.akamai.com/property-mgr/docs/cache-prefresh-refresh) feature:

> Set the slider to the percentage of an object’s TTL after which you would like the ​Akamai​ edge server to asynchronously refresh the object in cache.

This basically makes the threshold *dynamic* based on each call's `Duration`: by setting it to `0.9` (`90%`) it will always mean *"start refreshing in the background after 90% of this cached data duration, whatever that is"*.

## A Practical Example

Let's say we store some data in the cache with a `Duration` of `10 min` with [fail-safe](FailSafe.md) enabled:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        // DURATION
        .SetDuration(TimeSpan.FromMinutes(10))
        // FAIL-SAFE
        .SetFailSafe(true)
);
```

This means that any request coming in during these `10 min` will be served the value in the cache, and the first request coming in after `10 min` will start the refresh in a blocking way.

To alleviate blocking, We can set a `FatorySoftTimeout` to `100 ms` so we wouldn't be blocked by a refresh by no more than `100 ms`:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(10))
        .SetFailSafe(true)
        // SOFT TIMEOUT
        .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100))
);
```

In this way FusionCache will have `100 ms` to refresh the data, and after that will simply return the stale one and let the factory keep running in the background: as soon as it will finish, the data in the cache will be updated.

But what if instead we don't want to wait at all, and be sure we always get fresh data as soon as possible (as long as requests come in for that piece of data)?

We can specify an `EagerRefreshThreshold` of `0.9` (meaning 90% of the `Duration`):

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(10))
        .SetFailSafe(true)
        // EAGER REFRESH
        .SetEagerRefresh(0.9f)
);
```

Finally, what if we do this but no request arrives in the last `10%` of the Duration, but only after it's expired? The normal refresh cycle would still work, but we would be blocked in case the factory is taking too much time.

The solution is to combine the 2 approaches (timeouts + eager refresh), like this:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(10))
        .SetFailSafe(true)
        .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100))
        .SetEagerRefresh(0.9f)
);
```

This means that:
- ANY request coming in for the first `9 min` (`90%` of the `Duration`) will be served the fresh data immediately
- ANY request coming in after `9 min` (but before `10 min`) will also be served fresh data immediately. On top of that, IF it's the FIRST to do so, it will start the background refresh in a non-blocking way. If this happens, the cache will be updated as soon as the background refresh finishes
- the FIRST request coming in after `10 min` (if no request previously started an eager refresh) will start a normal refresh, and:
  - if the factory completes in under `100 ms`, the cache will be updated and fresh data will be returned
  - if the factory runs for more than `100 ms`, the cache will be *temporarily* updated with stale data, the same stale data will be returned and the factory will be left running in background. When that will finish, the cache will be updated with fresh data ready to be used for new requests

So basically fresh data as soon as possible + no blocking + complete protection from Cache Stampede.

Ain't it nice 😬 ?