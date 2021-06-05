<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :level_slider: Options

Even if FusionCache typically *just works* by default, it may be important to fine tune the available options to better suite your needs, and maybe save some memry allocations, too.

**:bulb: NOTE**: all of this information is fully available via Intellisense, thanks to the embedded code comments.

## FusionCacheOptions

When creating the entire cache it is possible to setup some cache-wide options, using the `FusionCacheOptions` type: you can configure some things, like the **custom logging levels** to use or a **cache key prefix** to be applied to each cache key.

The most important one is the `DefaultEntryOptions` which is a `FusionCacheEntryOptions` object (see below) used as a default:

```csharp
var cache = new FusionCache(new FusionCacheOptions() {
    // DEFAULT ENTRY OPTIONS
    DefaultEntryOptions = new FusionCacheEntryOptions {
        Duration = TimeSpan.FromMinutes(1),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
    }
});
```

This will be used when none is given for a specific method call:

```csharp
// THIS USES THE DEFAULT OPTIONS
cache.Set<int>("foo", 42);
```

or as a "starting point" to be duplicated and modified when expressing the call options as a lambda, like this:

```csharp
cache.Set<int>(
    "foo",
    42,
    // THIS DUPLICATES THE DEFAULT OPTIONS, SET A DIFFERENT DURATION AND ENABLES FAIL-SAFE
    options => options.SetDuration(TimeSpan.FromMinutes(2)).SetFailSafe(true)
);
```

In general this can be used as a set of options that will act as the *baseline*, while being able to granularly change everything you want **for each call**.

| Name | Type | Default | Description |
| ---: | :---: | :---: | :--- |
| `DefaultEntryOptions`                       | `FusionCacheEntryOptions` | *see below* | This is the default entry options object that will be used when one is not passed to each method call that need one, and as a starting point when duplicating one, either via the explicit `FusionCache.CreateOptions(...)` method or in one of the *overloads* of each *core method*. |
| `DistributedCacheCircuitBreakerDuration`    | `TimeSpan`                     | `none` | If set to a value greater than zero, every time the distributed cache will fail it will become temporarily unavailable for this amount of time. This is useful to avoid overloading the distributed cache when it has some problems. |
| `CacheKeyPrefix`                            | `string?`                 | `null` | If specified, each call to a core method will pre-process the specified cache key, prefixing it with this value. Uesful for example when using the same distributed cache for different environments (lowering the costs), to avoid cache entries from the development environment to mix with the ones from the staging environment. |
| `EnableSyncEventHandlersExecution`          | `bool`                    | `false` | If set to `true` all registered event handlers will be run synchronously: this is really, very, highly discouraged as it may slow down all other handlers and FusionCache itself. |
| `SerializationErrorsLogLevel`               | `LogLevel`                | `Error` | Used when logging serialization errors (while working with the distributed cache) |
| `DistributedCacheSyntheticTimeoutsLogLevel` | `LogLevel`                | `Warning` | Used when logging synthetic timeouts (both soft/hard) while using the distributed cache |
| `DistributedCacheErrorsLogLevel`            | `LogLevel`                | `Warning` | Used when logging any other kind of errors while using the distributed cache |
| `FactorySyntheticTimeoutsLogLevel`          | `LogLevel`                | `Warning` | Used when logging synthetic timeouts (both soft/hard) while calling the factory |
| `FactoryErrorsLogLevel`                     | `LogLevel`                | `Warning` | Used when logging any other kind of errors while calling the factory |
| `FailSafeActivationLogLevel`                | `LogLevel`                | `Warning` | Used when logging fail-safe activations |
| `EventHandlingErrorsLogLevel`               | `LogLevel`                | `Warning` | Used when logging errors while executing event handlers |


## FusionCacheEntryOptions

Almost every core method (like GetOrSet/Set/Remove/etc, see below) accepts a `FusionCacheEntryOptions` object that describes how to behave.

Things like fail-safe settings, soft/hard timeouts and more can be specified in this way: this lets you have granular control over each operation you perform.

For a better **developer experience** and to **consume less memory** (higher performance + lower infrastructure costs) it is possible to define a cache-wide `DefaultEntryOptions` (see above) and avoid passing one every time.


| Name | Type | Default | Description |
| ---: | :---: | :---: | :--- |
| `LockTimeout` | `TimeSpan` | `none` | To guarantee only one factory is called per each cache key, a lock mechanism is used: this value specifies a timeout after which the factory may be called nonetheless, ignoring the *single call* optimization. Usually it is not necessary, but to avoid any potential deadlock that may *theoretically* happen you can set a value. |
| `Duration` | `TimeSpan` | `30 sec` | The logical duration of the cache entry. This value will be used as the *actual* duration in the cache, but only if *fail-safe* is disabled. If *fail-safe* is instead enabled, the duration in the cache will be `FailSafeMaxDuration`, but this value will still be used to see if the entry is expired, to try to execute the factory to get a new value. |
| `JitterMaxDuration` | `TimeSpan` | `none` | If set to a value greater than zero it will be used as the maximum value for an additional, randomized duration of a cache entry's normal `Duration`. This is useful to prevent variations of the <a href="https://en.wikipedia.org/wiki/Thundering_herd_problem">Thundering Herd problem</a> in a multi-node scenario. |
| `Size` | `long` | `1` | This is only used to set the `MemoryCacheEntryOptions.Size` property when saving an entry in the underlying memory cache. |
| `Priority` | `CacheItemPriority` | `Normal` | This is only used to set the `MemoryCacheEntryOptions.Priority` property when saving an entry in the underlying memory cache. |
| `IsFailSafeEnabled` | `bool` | `false` | If fail-safe is enabled a cached entry will be available even after the logical expiration as a fallback, in case of problems while calling the factory to get a new value. |
| `FailSafeMaxDuration` | `TimeSpan` | `1 day` | When fail-safe is enabled, this is the amount of time a cached entry will be available, even as a fallback. |
| `FailSafeThrottleDuration` | `TimeSpan` | `30 sec` | When the fail-safe mechanism is actually activated in case of problems while calling the factory, this is the (usually small) new duration for a cache entry used as a fallback. This is done to avoid repeatedly calling the factory in case of an expired entry, and basically prevents <a href="https://en.wikipedia.org/wiki/Denial-of-service_attack">DOS</a>-ing yourself. |
| `FactorySoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory, applied only if fail-safe is enabled and there is a fallback value to return. |
| `FactoryHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory in any case, even if there is not a stale value to fallback to. |
| `AllowTimedOutFactoryBackgroundCompletion` | `bool` | `true` | It enables a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value. |
| `DistributedCacheSoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache when is not problematic to simply timeout. |
| `DistributedCacheHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fallback to. |
| `AllowBackgroundDistributedCacheOperations` | `bool` | `false` | Normally operations on the distributed cache are executed in a blocking fashion: setting this flag to true let them run in the background in a kind of fire-and-forget way. This will give a perf boost, but watch out for rare side effects. |
