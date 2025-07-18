<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🎚 Options

> [!TIP]
> All these informations are fully available via IntelliSense, auto-suggest or similar technologies, thanks to the embedded code comments.

Even if FusionCache typically *just works* by default, it may be important to fine tune the available options to better suite our needs, and maybe save some memory allocations, too.

In FusionCache, just like in `IMemoryCache` and `IDistributedCache`, there are 2 kinds of options: `FusionCacheOptions` and `FusionCacheEntryOptions`.

If we think about `IMemoryCache` and `IDistributedCache`, we can see a common theme in the naming:

|                     | per-cache options                                 | per-entry options              |
| :---                | ---:                                              | ---:                           |
| `IMemoryCache`      | `MemoryCacheOptions`                              | `MemoryCacheEntryOptions`      |
| `IDistributedCache` | `[Various]CacheOptions` (eg: `RedisCacheOptions`) | `DistributedCacheEntryOptions` |
| `FusionCache`       | `FusionCacheOptions`                              | `FusionCacheEntryOptions`      |

In this way we should feel at home when using FusionCache.

These 2 kinds of options serve different purposes:
- `FusionCacheOptions`: options related to an entire cache, configurable once at setup time
- `FusionCacheEntryOptions`: options related to each specific cache entry, configurable at every method call

## Per-cache options (`FusionCacheOptions`)

When configuring the entire cache (a `FusionCache` instance) it's possible to setup some cache-wide options, using the `FusionCacheOptions` type: with this we can configure things like [Auto-Recovery](AutoRecovery.md), the cache key prefix, custom logging levels to use and more.

## Per-entry options (`FusionCacheEntryOptions`)

Almost every core method like `GetOrSet`, `Set`, `Remove`, etc accepts a `FusionCacheEntryOptions` object that describes how to behave.

Things like fail-safe settings, soft/hard timeouts and more can be specified in this way: this lets us have **granular** control over each operation we perform.

In case we need to explicitly duplicate some entry options, there's a `Duplicate()` method on the `FusionCacheEntryOptions` object that, well, duplicate it to a new one (and, spoiler, it's the one used internally by FusionCache when using the lambda `opt => opt` etc).

Whether we specify them or not, they are there for every call (this is important).

## DefaultEntryOptions

A fundamental option in the `FusionCacheOptions` class is the `DefaultEntryOptions`, which is a `FusionCacheEntryOptions` object to be used as a default:

```csharp
var options = new FusionCacheOptions() {
    DefaultEntryOptions = new FusionCacheEntryOptions {
        Duration = TimeSpan.FromMinutes(1),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
    }
};
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

In general this can be used as a set of options that will act as the *baseline*, while being able to granularly change everything we want **for each call**.

Basically, when calling each method we can specify them in different ways:
- nothing: by not passing anything, the above mentioned `DefaultEntryOptions` will be used, as-is
- direct instance: by directly passing a `FusionCacheEntryOptions` instance, that will be used, as-is (no inherited defaults)
- lambda: by passing a lambda, you can easily "start from" the `DefaultEntryOptions` which will be duplicated for you by FusionCache and then make some changes, tyically via a fluent api (eg: `opt.SetDuration(...).SetFailSafe(...)` etc)

It's important to understand that the `DefaultEntryOptions` will act as "defaults" **ONLY** when not passing anything or when using the lambda: if we directly pass a `FusionCacheEntryOptions` instance, that instance will be used as-is, without any more changes.

If we want to have defaults but also pass a direct instance, we would need to duplicate the `DefaultEntryOptions` manually, make some changes, and then store the result somewhere and pass that: of course though, every change made to the `DefaultEntryOptions` after that would not be reflected to the already-duplicated entry options.

## Adaptive Caching?

What happens when using [Adaptive Caching](AdaptiveCaching.md)?

First thing to know: nothing will break!

By normally calling `GetOrSet`, some entry options will be used anyway by one onf the above ways (eg: if we don't pass anything the `DefaultEntryOptions` will be used, etc): these options are the ones we'll find inside the `FusionCacheFactoryExecutionContext` object in the `Options` property.

Now, when inside of the factory we can either change some properties on the `FusionCacheFactoryExecutionContext.Options` property like this:

```csharp
// INSIDE THE FACTORY
ctx.Options.Duration = TimeSpan.FromSeconds(10);
```

or even completely overwrite it, like this:

```csharp
// INSIDE THE FACTORY
ctx.Options = new FusionCacheEntryOptions() {
  // ...
};
```

But hold on: we may be wondering what happens when we change some options inside the factory. Are we inadvertently modifying the entry options passed in the outer method call? And if we don't pass anything, are we modifying the global `DefaultEntryOptions`?

The answer is no, everything is taken care of, since FusionCache will protect us from this problems by automatically duplicating the entry options behind the scenes to avoid changing a shared options object.

Also, it will do this both automatically (no manual intervention needed) and in an optimized way, meaning it will do that only when we are actually trying to modify something that was not marked as being "modifiable", and not for every single call.

This allows us to have total control and flexibility, all without wasting resources.

## Recap

So, to recap, every cache has their own "global" options of type `FusionCacheOptions`, and one of these options is the `DefaultEntryOptions` (of type `FusionCacheEntryOptions`) that serves as a default for every entry/method call we'll make.

Then, each time we call a method like `Set`, `Get`, `GetOrSet`, etc we can:
- specify different ones directly: by passing an instance of `FusionCacheEntryOptions` they'll be used as-is, no inheritance from defaults or anything
- specify a lambda: this will duplicate the `DefaultEntryOptions` and apply the changes we specified via the lambda
- specify nothing: the `DefaultEntryOptions` will be used, as-is, with no change
- when using Adaptive Caching we can be sure the original/outer options will not be changed, while still being able to act on `ctx.Options` to:
  - change one or more options there (eg: `ctx.Options.Duration = TimeSpan.FromSeconds(5)`)
  - change the `ctx.Options` entirely (eg: `ctx.Options = myNewOptions`)
- in case we need it, we can always call `myEntryOptions.Duplicate()` to get a copy of an existing entry options object, ready to be modified without altering the original one

## Detailed View

All these informations are fully available via IntelliSense, auto-suggest or similar technologies, thanks to the embedded code comments.

### FusionCacheOptions

| Name | Type | Default | Description |
| ---: | :---: | :---: | :--- |
| `CacheName`                                 | `string` | `"FusionCache"` | The name of the cache: it can be used for identification, and in a multi-node scenario it is typically shared between nodes to create a logical association. |
| `DefaultEntryOptions`                       | `FusionCacheEntryOptions`  | *see below* | This is the default entry options object that will be used when one is not passed to each method call that need one, and as a starting point when duplicating one, either via the explicit `FusionCache.CreateOptions(...)` method or in one of the *overloads* of each *core method*. |
| `DistributedCacheCircuitBreakerDuration`    | `TimeSpan`                 | `none` | The duration of the circuit-breaker used when working with the distributed cache. |
| `CacheKeyPrefix`           | `string?`     | `null` | A prefix that will be added to each cache key for each call: it can be useful when working with multiple named caches. With the builder it can be set using the `WithCacheKeyPrefix(...)` method. |
| `DistributedCacheKeyModifierMode`           | `CacheKeyModifierMode`     | `Prefix` | Specify the mode in which cache key will be changed for the distributed cache (eg: to specify the wire format version). |
| `BackplaneCircuitBreakerDuration`           | `TimeSpan`                 | `none` | The duration of the circuit-breaker used when working with the backplane. |
| `BackplaneChannelPrefix`                    | `string?`                  | `null` | The prefix to use in the backplane channel name: if not specified the `CacheName` will be used. |
| `IgnoreIncomingBackplaneNotifications`      | `bool`                     | `false` | Ignores incoming backplane notifications, which normally is DANGEROUS. |
| `EnableAutoRecovery`                        | `bool`                     | `true` | Enable auto-recovery for the backplane notifications to better handle transient errors without generating synchronization issues: notifications that failed to be sent out will be retried later on, when the backplane becomes responsive again. |
| `AutoRecoveryMaxItems`                      | `int?`                     | `null` | The maximum number of items in the auto-recovery queue: this is usually not needed, but it may help reducing memory consumption in extreme scenarios. |
| `AutoRecoveryDelay`                         | `TimeSpan`                 | `2s` | The amount of time to wait before actually processing the auto-recovery queue, to better handle backpressure. |
| `AutoRecoveryMaxRetryCount`                 | `int?`                     | `null` | The maximum number of retries for a auto-recovery item: after this amount an item is discarded, to avoid keeping it for too long. Please note though that a cleanup is automatically performed, so in theory there's no need to set this. |
| `EnableSyncEventHandlersExecution`          | `bool`                    | `false` | If set to `true` all registered event handlers will be run synchronously: this is really, very, highly discouraged as it may slow down all other handlers and FusionCache itself. |
| `ReThrowOriginalExceptions`                 | `bool`                    | `false` | If enabled, and re-throwing of exceptions is also enabled, it will re-throw the original exception as-is instead of wrapping it into one of the available specific exceptions. |
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

### FusionCacheEntryOptions

| **ℹ LEGEND** |
|:----------------|
| Options that supports [adaptive caching](AdaptiveCaching.md) (that is, that can be changed during a factory execution) are marked with a 🧙‍♂️ icon. |

| Name | Type | Default | Description |
| ---: | :---: | :---: | :--- |
| `LockTimeout` | `TimeSpan` | `none` | To guarantee only one factory is called per each cache key, a lock mechanism is used: this value specifies a timeout after which the factory may be called nonetheless, ignoring the *single call* optimization. Usually it is not necessary, but to avoid any potential deadlock that may *theoretically* happen we can set a value. |
| 🧙‍♂️ `Duration` | `TimeSpan` | `30 sec` | The logical duration of the cache entry. This value will be used as the *actual* duration in the cache, but only if *fail-safe* is disabled. If *fail-safe* is instead enabled, the duration in the cache will be `FailSafeMaxDuration`, but this value will still be used to see if the entry is expired, to try to execute the factory to get a new value. |
| 🧙‍♂️ `JitterMaxDuration` | `TimeSpan` | `none` | If set to a value greater than zero it will be used as the maximum value for an additional, randomized duration of a cache entry's normal `Duration`. This is useful to prevent variations of the <a href="https://en.wikipedia.org/wiki/Cache_stampede">Cache Stampede problem</a> in a multi-node scenario. |
| 🧙‍♂️ `Size` | `long?` | `null` | This is only used to set the `MemoryCacheEntryOptions.Size` property when saving an entry in the underlying memory cache. |
| 🧙‍♂️ `Priority` | `CacheItemPriority` | `Normal` | This is only used to set the `MemoryCacheEntryOptions.Priority` property when saving an entry in the underlying memory cache. |
| `IsFailSafeEnabled` | `bool` | `false` | If fail-safe is enabled a cached entry will be available even after the logical expiration as a fallback, in case of problems while calling the factory to get a new value. |
| 🧙‍♂️ `FailSafeMaxDuration` | `TimeSpan` | `1 day` | When fail-safe is enabled, this is the amount of time a cached entry will be available, even as a fallback. |
| 🧙‍♂️ `FailSafeThrottleDuration` | `TimeSpan` | `30 sec` | When the fail-safe mechanism is actually activated in case of problems while calling the factory, this is the (usually small) new duration for a cache entry used as a fallback. This is done to avoid repeatedly calling the factory in case of an expired entry, and basically prevents <a href="https://en.wikipedia.org/wiki/Denial-of-service_attack">DOS</a>-ing ourrselves. |
| `FactorySoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory, applied only if fail-safe is enabled and there is a fallback value to return. |
| `FactoryHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for the factory in any case, even if there is not a stale value to fallback to. |
| `AllowTimedOutFactoryBackgroundCompletion` | `bool` | `true` | It enables a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value. |
| 🧙‍♂️ `DistributedCacheDuration` | `TimeSpan?` | `null` | The custom duration to use for the distributed cache: this allows to have different duration between the 1st and 2nd levels. If `null`, the normal `Duration` will be used. |
| 🧙‍♂️ `DistributedCacheFailSafeMaxDuration` | `TimeSpan?` | `null` | The custom fail-safe max duration to use for the distributed cache: this allows to have different duration between the 1st and 2nd levels. If `null`, the normal `FailSafeMaxDuration` will be used. |
| 🧙‍♂️ `DistributedCacheSoftTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache when is not problematic to simply timeout. |
| 🧙‍♂️ `DistributedCacheHardTimeout` | `TimeSpan` | `none` | The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fallback to. |
| 🧙‍♂️ `AllowBackgroundDistributedCacheOperations` | `bool` | `false` | Normally operations on the distributed cache are executed in a blocking fashion: setting this flag to true let them run in the background in a kind of fire-and-forget way. This will give a perf boost, but watch out for rare side effects. |
| 🧙‍♂️ `ReThrowDistributedCacheExceptions` | `bool` | `false` | Set this to true to allow the bubble up of distributed cache exceptions (default is `false`). Please note that, even if set to true, in some cases we would also need `AllowBackgroundDistributedCacheOperations` set to false and no timeout (neither soft nor hard) specified. |
| 🧙 `ReThrowSerializationExceptions`    | `bool` | `true` | Set this to false to disable the bubble up of serialization exceptions (default is `true`). |
| 🧙‍♂️ `SkipBackplaneNotifications` | `bool` | `false` | Skip sending backplane notifications after some operations, like a SET (via a Set/GetOrSet call) or a REMOVE (via a Remove call). |
| 🧙‍♂️ `AllowBackgroundBackplaneOperations` | `bool` | `true` | By default every operation on the backplane is non-blocking: that is to say the FusionCache method call would not wait for each backplane operation to be completed. Setting this flag to `false` will execute these operations in a blocking fashion, typically resulting in worse performance. |
| 🧙‍♂️ `ReThrowBackplaneExceptions` | `bool` | `false` | Set this to true to allow the bubble up of backplane exceptions (default is `false`). Please note that, even if set to true, in some cases we would also need `AllowBackgroundBackplaneOperations` set to false. |
| 🧙‍♂️ `EagerRefreshThreshold` | `float?` | `null` | The threshold to apply when deciding whether to refresh the cache entry eagerly (that is, before the actual expiration). |
| 🧙‍♂️ `SkipDistributedCacheWrite` | `bool` | `false` | Skip writing to the distributed cache, if any. |
| `SkipDistributedCacheRead` | `bool` | `false` | Skip reading from the distributed cache, if any. |
| `SkipDistributedCacheReadWhenStale` | `bool` | `false` | When a 2nd level (distributed cache) is used and a cache entry in the 1st level (memory cache) is found but is stale, a read is done on the distributed cache: the reason is that in a multi-node environment another node may have updated the cache entry, so we may found a newer version of it. |
| 🧙‍♂️ `SkipMemoryCacheWrite` | `bool` | `false` | Skip writing to the memory cache. |
| `SkipMemoryCacheRead` | `bool` | `false` | Skip reading from the memory cache. |
