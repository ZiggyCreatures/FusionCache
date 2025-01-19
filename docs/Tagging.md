<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🏷️ Tagging

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to associate some tags to cache entries, and later call `RemoveByTag("my-tag")` to remove them all at once. The design is such that, even when this impact a massive number of cache entries, the workload is naturally distributed and without a high upfront cost. |

Sometimes we may need to logically _"group together"_ multiple cache entries: this can happen because later on we may want to evict them all at once.

An example can be caching multiple entries related to a certain category: when something changes there, it would be useful to remove all the related entries so they don't contain stale data.

So, how to do that?

### 😎 Super Easy, Barely An Inconvenience

When saving cache entries we can specify some tags, like this:

```csharp
cache.Set<int>("foo", 1, tags: ["tag-1", "tag-2"]);
cache.Set<int>("bar", 2, tags: ["tag-2", "tag-3"]);
cache.GetOrSet<int>("baz", _ => 3, tags: ["tag-1", "tag-3"]);
```

Later on we can then remove all entries associated with tag `"tag-1"`, like this:

```csharp
cache.RemoveByTag("tag-1");
```

After this call, only the entry for `"bar"` will remain.

What if we know the tags only when getting the data from the database?

Well, FusionCache supports [Adaptive Caching](AdaptiveCaching.md), so the question is: are tags are supported with it, too?

Of course they are, by simply doing this:

```csharp
cache.GetOrSet<int>(
    "baz",
    (ctx, _) =>
    {
        ctx.Tags = ["tag-1", "tag-3"]; // SET TAGS HERE DYNAMICALLY
        return 3;
    });
```

But wait, if we massively remove the entries by tag, would we be losing the ability to use them as a fallback in [fail-safe](FailSafe.md) scenarios?

No, fail-safe is totally supported too!

Easy peasy.

... well definitely easy to _use_, sure, but the feature itself is a monumental beast, and what lies underneath is something else, a particular design that I think deserves some understanding: to know more about the inception of the Tagging design in FusionCache, there's the [original proposal](https://github.com/ZiggyCreatures/FusionCache/issues/319).

## 🗿 Monumentally Complex

As we all know, cache invalidation in general is an [uber complex beast](https://martinfowler.com/bliki/TwoHardThings.html) to approach.

This is already true with "just" a memory cache, although it's doable.

Things get more complex with a distributed cache, where we are usually limited by the operations available in the specific cache service (eg: Redis, Memcached, etc).

If we then talk about an _hybrid_ cache like FusionCache, we can have 2 levels (L1 + L2, memory + distributed) and multi-node invalidation for horizontal scalability (eg: [backplane](Backplane.md)), and this makes the problem even more complex.

Also, as a cherry on top, in the case of FusionCache specifically we also need to consider the automatic management of transient errors with features like [fail-safe](FailSafe.md), [soft timeouts](Timeouts.md), [auto-recovery](AutoRecovery.md) and more.

We have a seemingly insurmountable task ahead.

Oh, finally we should also consider multiple [named caches](NamedCaches.md), which may or may not share the same underlying memory and/or distributed cache: this means that a proper remove by tag" may touch entries for logically separate caches that are stored in the same L2 instance.

Yeah, there's that too.

Damn 🥲

This is one of the hardest tasks in the world of caching, because it involves multiple entries all at once and can _potentially_ mean an upfront massive operation that also, ideally, hopefully, should not block or slow down our entire cache.

Imagine having a cache composed of millions of entries (been there, dont that) and, of those, tens of thousands are tagged with a certain tag: when we _remove by tag_ for that tag what needs to happen is similar to something like this (pseudo-code):

`DELETE * FROM table WHERE "my-tag" IN tags`

In general, this kind of operation is not something that distributed caches are typically good at.

Not a good start.

## 🚳 Limitations

Something to deal with, first and foremost, is a _design_ decision that sits at the foundation of FusionCache itself since the beginning: using the available .NET abstractions for L1 and L2, which are are quite limited in terms of functionalities.

In particular for L2 this means [`IDistributedCache`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache?view=net-8.0#methods): this decision paid a lot of dividends along the years, because any implementation of `IDistributedCache` is automatically usable with FusionCache and, since there are [a lot of them readily available](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md#-packages) covering very different needs, this is a **very powerful** characteristic to have available.

On the other hand `IDistributedCache` has a very limited set of functionalities available, basically only 3 methods:
- `Set`
- `Get`
- `Remove`

That's it, and it's not a lot to work with.

So, what can be done?

## 🛬 Approaches

There are basically 2 different approaches:
- **Server-Assisted**: the L2 (eg: Redis or Memcached) makes a real "remove by tag" in some way
- **Client-Assisted**: the client library (eg: FusionCache) does "something" so that the end result _looks like_ the same

The **Server-Assisted** approach is not reasonably doable, since not a lot of distributed caches support the operations needed: on top of this, it would mean abandoning the use of the `IDistributedCache` and creating a new abstraction with more features, leaving behind all the existing implementations.

Not good.

The **Client-Assisted** approach instead does not have _any_ requirement for the server-side, and is complex _"only"_ for the caching library itself. But, like, really complex.

So, is it doable?

## 🪄 The Solution

Yes, with the **Client-Assisted** approach and a delicate balance of the workload needed.

Here's what happens: a `RemoveByTag("tag123")` would simply set internally an entry with a key `"__fc:t:tag123"`, containing the current timestamp. The concrete cache key will also consider any potential `CacheKeyPrefix`, so mutliple named caches on shared cache backends would automatically be supported, too.

Then when getting a cache entry, **after** getting it from L1/L2 but **before** returning it to the outside world, FusionCache would see if it has tags attached to it and, in that case and only in thase case (so no extra costs when not used), it would get the expiration timestamp for each tag to see if it's expired and when.

For each related tag, if an expiration timestamp is present and that is greater than the timestamp at which the cache entry has been created, it should then be considered expired.

This would logically work as a sort of "barrier" or ["high-pass filter"](https://en.wikipedia.org/wiki/High-pass_filter) to "hide" data that is logically expired because of one or more of the associated tags: think of this as a "soft delete" VS a "real delete", but instead of marking the cache entries all at once one by one, FusionCache saves one extra entry for that tag with the timestamp and then it's done.

Regarding the options for tags data, like `Duration` or timeouts, they are configurable via a dedicated `TagsDefaultEntryOptions` option, but they already have sensible defaults with things like a `Duration` of 10 days and so on.

This can be considered a "passive" approach (waiting for each read to see if it's expired) instead of an "active" one (actually go and massively expire data immediately everywhere).

When get-only methods (eg: `TryGet`, `GetOrDefault`) are called and a cache entry is found to be expired because of tags, FusionCache not only hides it from the outside world but it also effectively expire it.

When get-set methods (eg: `GetOrSet`) is called and a cache entry is found to be expired because of tags, it just skip it internally and call the factory, since that would produce a new value and resolve the problem anyway.

For both types of operations when an entry must be expired or overwritten because of a remove by tag has been detected, the normal FusionCache behaviour kicks in: the result will be applied both locally in the L1, remotely on L2 and on each other node's L1 remotely (thanks to the [backplane](Backplane.md)).

So the system would automatically updates internally based on actual usage, only if and when needed, without massive updates to be made when expiring by a tag.

Nice 😬

## 🎲 Of Statistics And Probability

By understanding the probabilistic nature of tags and by most importantly relying on all the existing plumbing that FusionCache already provides (like L1+L2 support, fail-safe, non-blocking background distributed operations, auto-recovery, etc) we can "simply" do as said, and all will work well: one extra entry per tag, that's it.

Regarding the probabilistic nature: basically a lot of tags will be shared between multiple cache entries, so the reuse will be typically high (think about the [Birthday Paradox](https://en.wikipedia.org/wiki/Birthday_problem)).

So, on one hand it's true that on a real-world big system in production we'll probably have a lot of tags and tag invalidations along time.

But on the other it's also true that, by their own nature, a lot of tags will be **shared** between cache entries: this is the whole point of tagging anyway.

On top of this, we can simply set some reasonable limits: simply do not _overtag_, meaning we should not tag an entry with a ton of tags. That's it.

This is also a pretty common best practice in general: for example when working with metrics in [OTEL](https://opentelemetry.io/) systems, it is a known to avoid having a huge amount of different values, a situation known as "high cardinality", so we can say the same here.

Because of all of this, the workload required to accomplish a massive "remove by tag" will be naturally distributed along both time and different entries (and nodes, if multiple).

Nice.

## 🤔 What About?

What about app restarts?
<br/>
No big deal, since everything is based on the common plumbing of FusionCache, all will work normally and tag-eviction data will get re-populated again automatically, lazily, and based on only the effective usage.

What about needing tag expiration for "tag1" by 2 difference cache entries at the same time?
<br/>
Only one load will happen, thanks to the [Cache Stampede](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md) protection.

What about tag expiration data being propagated to other ones?
<br/>
We are covered, thanks to the [Backplane](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md).

And what if tags are based on the data returned from the factory, so that it is not known upfront?
<br/>
No worries, [Adaptive Caching](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md) has been extended with a new `Tags` property, where we can dynamically specify the tags.

What about potential transient errors?
<br/>
We are covered, thanks to [Fail-Safe](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md) and [Auto-Recovery](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md).

What about slow distributed operations?
<br/>
Again we are covered, thanks to advanced [Timeouts](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md) and [Background Distributed Operations](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/BackgroundDistributedOperations.md).

All of this because of the solid foundations that have been built in FusionCache for years.

## 🚀 Performance Considerations

Wait, so we basically said that when getting `1` entry with `N` tags associated, another get for each tag will be needed, right?

This is probably ringing a bell for a lot of people reding this: `1` load, then `N` loads... isn't this a variation of the dreaded ["SELECT N+1 problem"](https://stackoverflow.com/a/97253/284332)?

Actually not, at least realistically that is not the case.

But why?

Well mostly because of probabilistic theory (see above the Birthday Paradox) and adaptive loading based on concrete usage.

Let me explain.

A `SELECT N+1` problem happens when, to get a piece of data, we do a first select that returns N elements and then, for each element, we do an additional SELECT.

Here this does not happen, because:
- as soon as a tag returns a timestamp that makes the entry expired, the process stops, reducing the SELECT amount to the minimum
- because of how tags are used, meaning shared between different entries, one load of tag expiration data will be used for multiple entries, reducing again the SELECT amount
- some internal optimizations in FusionCache such that the amount of read operations is reduced even more

As an example let's look at getting these cache entries (either concurrently or one after the other, it's the same):

- key `"foo"`, tagged `["tag-1", "tag-2"]`
- key `"bar"`, tagged `["tag-2", "tag-3"]`
- key `"baz"`, tagged `["tag-1", "tag-3"]`

The expiration data for `"tag-1"` will be loaded lazily (only when needed) and only once, and automatically shared between the processing of cache entries for both `"foo"` and `"baz"`, same thing for `"tag2"` and so on.
And since as said tags are frequently shared between different cache entries, the system will automatically load only what's needed, when it's needed, and once.

In the end some extra reads may be needed, yes, but definitely not the SELECT N+1 case which would only remain as a rare worst case scenario, and definitely not for every single cache read.

What about cache entries without tags? Zero extra cost.

What about entries that are already expired, but kept around because of fail-safe? Zero extra cost.

There are also some internal optimizations made specifically for the Tagging feature, to make it even better: an example is that when we do a `RemoveByTag()` and we are using a [backplane](Backplane.md), the nodes receiving the notifications will already have the data needed to immediately update the local memory level (L1) with that data. This will avoid an extra remote read on L2 in the future when it would've been needed, making the information immediately available.

## 🧱 Of Ancient Egypt and Suffolk, England

Look at this beauty:

<div align="center">

![A crinkle crankle wall in Bramfield, Suffolk](images/crinkle-crankle-wall.jpg)

_A crinkle crankle wall in Bramfield, Suffolk_
<br/>
_Nat Bocking / Crinkle-Crankle Wall in Bramfield / CC BY-SA 2.0_

</div>

This is known as a [Crinkle Crankle wall](https://en.wikipedia.org/wiki/Crinkle_crankle_wall): it originated in Ancient Egypt and is now found in Suffolk, England.

Ok, so?

Well, I discovered them some time ago while doomscrolling Wikipedia at late night and I kept a screenshot around, feeling there was something to it but not knowing exactly why: then it recently clicked, and they've been an inspiration for how to design tagging in FusionCache.

You see, most people when looking at them may think _"that's stupid, what a waste of bricks!"_, but the reality is the opposite.

Their particular _DESIGN_ and the way the bricks are _DISTRIBUTED_ allow a Crinkle Crankle wall with a single line of bricks to be as _ROBUST_, if not _MORE_, than _NORMAL_ straight walls composed of more lines of bricks, lowering the _COST_: even though they are typically 22% _LONGER_ (if straighten) they use _LESS RESOURCES_, all because of the peculiar _DESIGN_.

See where I'm going with this?

The client-assisted passive _DESIGN_ of tagging in FusionCache has a lower _COST_ (both upfront and total) because it does not need massive operations like a _NORMAL_ approach would do and because it will only actually expire the entries that are later actually requested (otherwise they'll expire normally). It makes the work needed more naturally _DISTRIBUTED_ over time and over multiple nodes and even though it may take _LONGER_ to physically expire all the entries underneath, the observable end result is instantly the same, actually even _MORE_ rapid, while requiring _LESS RESOURCES_ overall and making the entire system more _ROBUST_.

I really like to take inspiration by random stuff from the real world that are totally unrelated.