<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# ♊ Auto-Clone

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to tell FusionCache to always return a clone of a cached value instead of the value itself: this will guarantee that any change made to the returned values will not be visible to subsequent requests. |

In general is never a good idea to mutate data retrieved from the cache: it should always be considered immutable/readonly.

To  see why, the typical example of such behaviour is an update flow: get something from the database, change it, and then save it back to the database. Seemingly easy peasy right?

The problem in this case is that _a cache is a cache_, meaning the value may not be the very last version from the database, even by just `1s` or less: since we are updating it, we want to have the very last version before changing it and saving it back, otherwise we would loose some changes made after the last time cached it.

Although optimistic concurrency, with a variation of ETag/last modified, surely helps avoiding critical issues like loosing some changes, by enlarging the temporal window where something may be stale we are also increasing the chance of optimistic concurrency errors.

Therefore in these scenarios (like the update flow mentioned above) the best practice is to bypass the cache entirely and just get it from the database directly while using the cache for the remaining reads: since the vast majority of real-world scenarios are read-heavy rather than write-heavy, this means that we can keep the incredible perf boost with caching in more than 99% of cases, while avoiding issues in trying to change cached values to then save them to the database.

Having said that, keep reading...

## 🙋‍♀️ To Each Their Own

It came up [multiple](https://github.com/ZiggyCreatures/FusionCache/issues/194) [times](https://github.com/ZiggyCreatures/FusionCache/issues/262) from community members a desire to be able to get something from the cache and _safely_ change it.

This may not necessarily be related to an update flow with the potential problems highlighted above: users may have a particular scenario in mind, and ideally they should be abe to just do that in an easy (and optimized!) way, by following the tried and true _"it just works"_ mindset.

A common solution that has been adopted by the community is to [create a wrapper implementation](https://github.com/ZiggyCreatures/FusionCache/issues/194#issuecomment-1951461803) of `IMemoryCache` and would act on an underlying `IMemoryCache` instance by adding a serialization + deserialization step, for each get operation on a cache entry: this `IMemoryCache` instance is then passed to FusionCache, to make it kinda "transparent".

This technically works, but has a couple of issues:
- it does not get the same _"it just works"_ vibe I always pushed for FusionCache
- it requires some extra coding (the wrapper class)
- it requries some extra setup
- since it's done at the entire cache level, it lacks granular control to specify for which cache entries it is needed, so it's an all-or-nothing approach
- for every get operation it would deserializes (which is the whole point) but it also serialize every time, and that is not so great for performance: deserializing is what guarantees us cloning, while the serialization to `byte[]` can happen only once.

## 💡 Idea: Auto-Clone to the rescue

Since I collected enough evidence that this is something needed by the community (even though probably not by a huge amount of people), I decided to finally tackle this with a new option, called Auto-Clone.

The idea is that:
- it just works, out of the box
- is easy to use
- doesn't require extra coding/setup (it's just a new option)
- uses existing code infrastructure (eg: `IFusionCacheSerializer`)
- has granular control on a per-entry basis
- is performant (as much as possible)

That's cool, but how?

Well FusionCache always had the ability to specify a serializer (type `IFusionCacheSerializer`) to be able to work with the distributed cache: it will use the same serializer to (de)serialize values, easy peasy.

To avoid being forced to also specify A new method `SetupSerializer(serializer)` is being created, while also being able to do the same via dependency injection, as always.
The option `ReThrowOriginalExceptions` will also be respected.

By simply setting `EnableAutoClone` to `true` in the entry options, FusionCache will take care of everything.

Since the feature creates the clear expectation for users to be able to get something from the cache and freely modify it without repercussions, an exception will be thrown in these cases:
- if the feature is enabled and there's no serializer, it will be thrown an `InvalidOperationException`
- if the serializer fails to serialize or deserialize, it will be thrown either the specific exception (by the serializer being used) or a `FusionCacheSerializationException`, depending on the `ReThrowOriginalExceptions` option

Of course `DefaultEntryOptions` are always at our disposal to do the usual [default + granular change flow](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md#defaultentryoptions), so we can both enable it granulary per-call or just once in the `DefaultEntryOptions` and forget about it (but remember: auto-cloning has a cost, so use it with care).

## 🚀 Performance

Regarding performance: instead of serializing + deserializing at every get call (which would be a waste of resources), FusionCache will keep track of an internal buffer on each memory entry and, only if and when it will be requried, will serialize it (just once) so that the binary payload will be available to deserialize at every get call.

Extra care will be put into avoiding any synchronization/double-serialization issue related to multithreading or high-load scenario.