<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :level_slider: Options

Even if FusionCache typically *just works* by default, it may be important to fine tune the available options to better suite your needs, and maybe save some memory allocations, too.

**:bulb: NOTE**: all of this information is fully available via Intellisense, thanks to the embedded code comments.

## FusionCacheOptions

When creating the entire cache it is possible to setup some cache-wide options, using the `FusionCacheOptions` type: with that you can configure things like the cache name, custom logging levels to use and more.

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
| `CacheName`                                 | `string` | `"FusionCache"` | The name of the cache: it can be used for identification, and in a multi-node scenario it is typically shared between nodes to create a logical association. |
| `DefaultEntryOptions`                       | `FusionCacheEntryOptions`  | *see below* | This is the default entry options object that will be used when one is not passed to each method call that need one, and as a starting point when duplicating one, either via the explicit `FusionCache.CreateOptions(...)` method or in one of the *overloads* of each *core method*. |
| `DistributedCacheCircuitBreakerDuration`    | `TimeSpan`                 | `none` | The duration of the circuit-breaker used when working with the distributed cache. |
| `CacheKeyPrefix`           | `string?`     | `null` | A prefix that will be added to each cache key for each call: it can be useful when working with multiple named caches. With the builder it can be set using the `WithCacheKeyPrefix(...)` method. |
| `DistributedCacheKeyModifierMode`           | `CacheKeyModifierMode`     | `Prefix` | Specify the mode in which cache key will be changed for the distributed cache (eg: to specify the wire format version). |
| `BackplaneCircuitBreakerDuration`           | `TimeSpan`                 | `none` | The duration of the circuit-breaker used when working with the backplane. |
| `BackplaneChannelPrefix`                    | `string?`                  | `null` | The prefix to use in the backplane channel name: if not specified the `CacheName` will be used. |
| `EnableBackplaneAutoRecovery`               | `bool`                     | `true` | Enable auto-recovery for the backplane notifications to better handle transient errors without generating synchronization issues: notifications that failed to be sent out will be retried later on, when the backplane becomes responsive again. |
| `BackplaneAutoRecoveryMaxItems`             | `int?`                     | `null` | The maximum number of items in the auto-recovery queue: this can help reducing memory consumption. If set to `null` there will be no limit. |
| `BackplaneAutoRecoveryReconnectDelay`       | `TimeSpan`                     | `2s` | The amount of time to wait, after a backplane reconnection, before trying to process the auto-recovery queue: this may be useful to allow all the other nodes to be ready. |
| `EnableDistributedExpireOnBackplaneAutoRecovery`| `bool`                     | `true` | Enable expiring a cache entry, only on the distributed cache (if any), when anauto-recovery message is being published on the backplane, to ensure that the value in the distributed cache will not be stale. |
| `EnableSyncEventHandlersExecution`          | `bool`                    | `false` | If set to `true` all registered event handlers will be run synchronously: this is really, very, highly discouraged as it may slow down all other handlers and FusionCache itself. |
| `IncoherentOptionsNormalizationLogLevel`    | `LogLevel`                | `Warning` | Used when some options have incoherent values that have been fixed with a normalization, like for example when a FailSafeMaxDuration is lower than a Duration, so the Duration is used instead. |
| `SerializationErrorsLogLevel`               | `LogLevel`                | `Error` | Used when logging serialization errors (while working with the distributed cache). |
| `DistributedCacheSyntheticTimeoutsLogLevel` | `LogLevel`                | `Warning` | Used when logging synthetic timeouts (both soft/hard) while using the distributed cache. |
| `DistributedCacheErrorsLogLevel`            | `LogLevel`                | `Warning` | Used when logging any other kind of errors while using the distributed cache. |
| `FactorySyntheticTimeoutsLogLevel`          | `LogLevel`                | `Warning` | Used when logging synthetic timeouts (both soft/hard) while calling the factory. |
| `FactoryErrorsLogLevel`                     | `LogLevel`                | `Warning` | Used when logging any other kind of errors while calling the factory. |
| `FailSafeActivationLogLevel`                | `LogLevel`                | `Warning` | Used when logging fail-safe activations. |
| `EventHandlingErrorsLogLevel`               | `LogLevel`                | `Warning` | Used when logging errors while executing event handlers. |
| `BackplaneSyntheticTimeoutsLogLevel`        | `LogLevel`                | `Warning` | Used when logging synthetic timeouts (both soft/hard) while using the backplane. |
| `BackplaneErrorsLogLevel`                   | `LogLevel`                | `Warning` | Used when logging any other kind of errors while using the backplane. |
| `PluginsInfoLogLevel`                       | `LogLevel`                | `Information` | Used when logging informations about a plugin. |
| `PluginsErrorsLogLevel`                     | `LogLevel`                | `Error` | Used when logging an error while working with a plugin. |


## FusionCacheEntryOptions

Almost every core method (like GetOrSet/Set/Remove/etc, see below) accepts a `FusionCacheEntryOptions` object that describes how to behave.

Things like fail-safe settings, soft/hard timeouts and more can be specified in this way: this lets you have granular control over each operation you perform.

For a better **developer experience** and to **consume less memory** (higher performance + lower infrastructure costs) it is possible to define a cache-wide `DefaultEntryOptions` (see above) and avoid passing one every time.

| **ℹ LEGEND** |
|:----------------|
| Options that supports [adaptive caching](AdaptiveCaching.md) (that is, that can be changed during a factory execution) are marked with a 🧙‍♂️ icon. |

| Name | Type | Default | Description |
| ---: | :---: | :---: | :--- |
| `LockTimeout` | `TimeSpan` | `none` | To guarantee only one factory is called per each cache key, a lock mechanism is used: this value specifies a timeout after which the factory may be called nonetheless, ignoring the *single call* optimization. Usually it is not necessary, but to avoid any potential deadlock that may *theoretically* happen you can set a value. |
| 🧙‍♂️ `Duration` | `TimeSpan` | `30 sec` | The logical duration of the cache entry. This value will be used as the *actual* duration in the cache, but only if *fail-safe* is disabled. If *fail-safe* is instead enabled, the duration in the cache will be `FailSafeMaxDuration`, but this value will still be used to see if the entry is expired, to try to execute the factory to get a new value. |
| 🧙‍♂️ `JitterMaxDuration` | `TimeSpan` | `none` | If set to a value greater than zero it will be used as the maximum value for an additional, randomized duration of a cache entry's normal `Duration`. This is useful to prevent variations of the <a href="https://en.wikipedia.org/wiki/Cache_stampede">Cache Stampede problem</a> in a multi-node scenario. |
| 🧙‍♂️ `Size` | `long` | `1` | This is only used to set the `MemoryCacheEntryOptions.Size` property when saving an entry in the underlying memory cache. |
| 🧙‍♂️ `Priority` | `CacheItemPriority` | `Normal` | This is only used to set the `MemoryCacheEntryOptions.Priority` property when saving an entry in the underlying memory cache. |
| `IsFailSafeEnabled` | `bool` | `false` | If fail-safe is enabled a cached entry will be available even after the logical expiration as a fallback, in case of problems while calling the factory to get a new value. |
| 🧙‍♂️ `FailSafeMaxDuration` | `TimeSpan` | `1 day` | When fail-safe is enabled, this is the amount of time a cached entry will be available, even as a fallback. |
| 🧙‍♂️ `FailSafeThrottleDuration` | `TimeSpan` | `30 sec` | When the fail-safe mechanism is actually activated in case of problems while calling the factory, this is the (usually small) new duration for a cache entry used as a fallback. This is done to avoid repeatedly calling the factory in case of an expired entry, and basically prevents <a href="https://en.wikipedia.org/wiki/Denial-of-service_attack">DOS</a>-ing yourself. |
| `FactorySoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory, applied only if fail-safe is enabled and there is a fallback value to return. |
| `FactoryHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory in any case, even if there is not a stale value to fallback to. |
| `AllowTimedOutFactoryBackgroundCompletion` | `bool` | `true` | It enables a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value. |
| 🧙‍♂️ `DistributedCacheDuration` | `TimeSpan?` | `null` | The custom duration to use for the distributed cache: this allows to have different duration between the 1st and 2nd layers. If `null`, the normal `Duration` will be used. |
| 🧙‍♂️ `DistributedCacheFailSafeMaxDuration` | `TimeSpan?` | `null` | The custom fail-safe max duration to use for the distributed cache: this allows to have different duration between the 1st and 2nd layers. If `null`, the normal `FailSafeMaxDuration` will be used. |
| 🧙‍♂️ `DistributedCacheSoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache when is not problematic to simply timeout. |
| 🧙‍♂️ `DistributedCacheHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fallback to. |
| 🧙‍♂️ `AllowBackgroundDistributedCacheOperations` | `bool` | `false` | Normally operations on the distributed cache are executed in a blocking fashion: setting this flag to true let them run in the background in a kind of fire-and-forget way. This will give a perf boost, but watch out for rare side effects. |
| 🧙‍♂️ `ReThrowDistributedCacheExceptions` | `bool` | `false` | Set this to true to allow the bubble up of distributed cache exceptions (default is `false`). Please note that, even if set to true, in some cases you would also need `AllowBackgroundDistributedCacheOperations` set to false and no timeout (neither soft nor hard) specified. |
| 🧙 `ReThrowSerializationExceptions`    | `bool` | `true` | Set this to false to disable the bubble up of serialization exceptions (default is `true`). |
| 🧙‍♂️ `SkipBackplaneNotifications` | `bool` | `false` | Skip sending backplane notifications after some operations, like a SET (via a Set/GetOrSet call) or a REMOVE (via a Remove call). |
| 🧙‍♂️ `AllowBackgroundBackplaneOperations` | `bool` | `true` | By default every operation on the backplane is non-blocking: that is to say the FusionCache method call would not wait for each backplane operation to be completed. Setting this flag to `false` will execute these operations in a blocking fashion, typically resulting in worse performance. |
| 🧙‍♂️ `EagerRefreshThreshold` | `float?` | `null` | The threshold to apply when deciding whether to refresh the cache entry eagerly (that is, before the actual expiration). |
| 🧙‍♂️ `SkipDistributedCache` | `bool` | `false` | Skip the usage of the distributed cache, if any. |
| 🧙‍♂️ `SkipDistributedCacheReadWhenStale` | `bool` | `false` | When a 2nd layer (distributed cache) is used and a cache entry in the 1st layer (memory cache) is found but is stale, a read is done on the distributed cache: the reason is that in a multi-node environment another node may have updated the cache entry, so we may found a newer version of it. |
| 🧙‍♂️ `SkipMemoryCache` | `bool` | `false` | Skip the usage of the memory cache. |
