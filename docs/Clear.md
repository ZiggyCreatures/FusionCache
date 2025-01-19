<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🧼 Clear

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to clear the entire cache, all at once, without worrying about complex scenarios like a shared memory level (L1) or distributed level (L2), cache key prefix or else. Based on the [Tagging](Tagging.md) feature, the design handles everything. |

A seemingly simple feature for a cache is to be able to _clear_ it, just like we do with a `Dictionary`, right?

The idea seems obvious, but in reality it's quite hard to do (and even more so for a hybrid cache like FusionCache) because we have to consider a lot of other things and scenarios, like:
- L1 only
- L1+L2 (optional)
- backplane (optional)
- isolated L1 or shared L1
- isolated L2 or shared L2
- usage of multiple named caches
- usage of a cache-key prefix

Then we need to multiply all of these for cases like:
- single node
- multiple nodes

Finally, as a cherry on top, everything should automatically handle transient errors and work with features like [fail-safe](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md), [soft timeouts](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md), [auto-recovery](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md) and more.

And... that is a lot!

So how is it possible to do achieve all of this?

## 🏷️ Tagging to the rescue

Now that [Tagging](Tagging.md) is finally here, we have our solution.

By simply using a "special tag" like `"*"` we can use tagging to make a proper `Clear()` mechanism work (for a detail of Tagging works underneath, please refer to its [docs](Tagging.md)).

Here's an example:

```csharp
cache.Set("foo", 1);
cache.Set("bar", 2, tags: ["tag-1", "tag-2"]);
cache.Set("baz", 3);

// CLEAR
cache.Clear();

// HERE maybeFoo.HasValue IS false
var maybeFoo = cache.TryGet<int>("foo");
```

Nice 😬

Please be aware that since the design is based on the Tagging feature, the behaviour and considerations are the same, so it's suggested to get to know that feature.

Now, in reality the `Clear()` method has an additional, optional parameter: `bool allowFailSafe` with a default value of `true`, so we can do `Clear(true)` or `Clear(false)`.

But why is that?

Well, since FusionCache has the powerful [fail-safe](FailSafe.md) feature, we can pick between two operations:
- `Clear(true)`: basically like an "expire all", where entries with fail-safe enabled wil be marked as expired so they can be used as a fallback later on. This uses the special `"*"` tag
- `Clear(false)`: basically like a "remove all", where entries wil be removed for good. This uses the special `"!"` tag

## 🚀 Performance Considerations

On one hand, using Tagging to achieve `Clear()` support is a great design choice: all the plumbing available in FusionCache is used to achieve and empower Tagging, and in turn all the Tagging plumbing is used to achieve and empower `Clear()` support.

Nice, really.

On the other hand, we can go one step further: since the special tags used for clear are 2 one only 2, we may special-case them to get even better results.

This is why FusionCache is also saving the expiration timestamp for the 2 special tags ("clear expire" and "clear remove") directly in memory, meaning in 2 normal variables, so that FusionCache will keep them there forever and every `Clear(true/false)` call will also update them: in this way the speed for checking them would be even greater than checking the cache entry for the special tag.

But wait, in a multi-node scenario a `Clear()` may happen on another node, and we may receive a backplane notification from that other node!

Correct, and that is why when receiving a backplane notification FusionCache also checks to see if it is for the special tags and, if so, do what is needed so that the special timestamps (and the dedicated variables) is updated, automatically.

And what happens in case of transient issues while sending that backplane notification?

No big deal, we are already covered thanks to [Auto-Recovery](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md).

## 🐯 Raw Clear()

From some time now the standard `MemoryCache` (currently used as the L1) supports a "real" `Clear()` method that does what I call a "raw clear", meaning a full one like you do with a `Dictionary` instead of the "simulated" one done thanks for the client-assisted approach of the Tagging feature.

So, can't just FusionCache use it?

Not so fast, for the reasons exposed at the beginning, meaning:
- L1+L2: if we raw clear L1, at the next request the data that should've been cleared will come back from L2 (where it is not possible to do a raw clear)
- if the L1 is shared: if the same `MemoryCache` instance is used with different FusionCache instances (usually also with `CacheKeyPrefix`), a `Clear()` on the `MemoryCache` instance would effectively clear all the FusionCache instances that use the same underlying `MemoryCache` instance

But some users use FusionCache without L2, without a backplane, and without sharing a `MemoryCache` instance between multiple FusionCache instances, so... can't FusionCache do a raw clear in those cases?

Yes, yes it can!

This is in fact what FusionCache will do.

So to recap, if:
- there is no L2
- there is no backplane
- the underlying `MemoryCache` supports a raw clear
- the underlying `MemoryCache` is not shared (between different FusionCache instances)

then when `Clear()` is invoked FusionCache will automatically call `Clear()` on the underlying `MemoryCache`, and immediately wipe out the entire thing for good.

Can I say, again, nice 🙂 ?