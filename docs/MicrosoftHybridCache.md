<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# Ⓜ️ Microsoft HybridCache Support

| ⚡ TL;DR (quick version) |
| -------- |
| FusionCache can ALSO be used as an implementation of the new `HybridCache` abstraction from Microsoft, with the added extra features of FusionCache. Oh, and it's the first production-ready implementation of HybridCache (see below). |

With .NET 9 Microsoft [introduced](https://www.youtube.com/watch?v=rjMfDUP4-eQ) their own hybrid cache, called [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0).

This of course sparked a lot of [questions](https://x.com/markjerz/status/1785654100375281954) about what I thought about it, and the future of FusionCache.

If you like you can read [my thoughts](https://github.com/ZiggyCreatures/FusionCache/discussions/266#discussioncomment-9915972) about it, but the main take away is:

> FusionCache will NOT leverage the new HybridCache, in the sense that it will not be built "on top of it", BUT it will do 2 main things:
> 
> 1. be available ALSO as an implementation of HybridCache
> 2. it may take advantage of some of the new underlying bits being created FOR it

You see, the nice thing is that Microsoft introduced not just a (default) _implementation_ (which as of this writing, Jan 2025, has not been released yet) but also a shared _abstraction_ that anyone can implement.

## 🖼️ Abstractions

Ok so `HybridCache` is first and foremost an abstraction, and?

This may turn the HybridCache `abstract class` into some sort of "lingua franca" for a basic set of common features for all hybrid caches in .NET.

So FusionCache is available **ALSO** as an implementation of HybridCache, via an adapter class.

> [!IMPORTANT]  
> To be clear, this does **NOT** mean that FusionCache will now be based **ON** `HybridCache` from Microsoft, but that it will **ALSO** be available **AS** an implementation of it, via an adapter class.

Ok cool, but how?

Easy peasy:

```csharp
services.AddFusionCache()
  .AsHybridCache(); // MAGIC
```

When setting up FusionCache in our `Startup.cs` file, we simply add `.AsHybridCache()`, that's it.

Now, every time we'll ask for HybridCache via DI (taken as-is from the [official Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0#the-main-getorcreateasync-overload)):

```csharp
public class SomeService(HybridCache cache)
{
    private HybridCache _cache = cache;

    public async Task<string> GetSomeInfoAsync(string name, int id, CancellationToken token = default)
    {
        return await _cache.GetOrCreateAsync(
            $"{name}-{id}", // Unique key to the cache entry
            async cancel => await GetDataFromTheSourceAsync(name, id, cancel),
            cancellationToken: token
        );
    }

    public async Task<string> GetDataFromTheSourceAsync(string name, int id, CancellationToken token)
    {
        string someInfo = $"someinfo-{name}-{id}";
        return someInfo;
    }
}
```

we'll be using in reality FusionCache underneath _acting as_ HybridCache, all transparently.

Oh, and we'll still be able to get `IFusionCache` too at the same time, so another `SomeService2` in the same app, similarly as the above example, can do this:

```csharp
public class SomeService2(IFusionCache cache)
{
    private IFusionCache _cache = cache;
    
    // ...
```

and the **SAME** FusionCache instance will be used for both, directly as well as via the HybridCache adapter.

As FusionCache users this means we'll have 2 options available:
- use `FusionCache` directly, as they did up until today
- depend on the `HybridCache` shared _abstraction_ by Microsoft, but use the `FusionCache` _implementation_ (the adapter)

Actually, as said, we can do them both at the same time, in the same app: if there are components that depend on the HybridCache abstraction we can use the adapter for them, and if we want more power and more control in our own code we can use FusionCache directly, all while sharing the same underlying data.

Basically, we register FusionCache (eg: `.AddFusionCache()`), make it also available as HybridCache (eg: `.AsHybridCache()`), and use what we want based on the need.

Also, when using the adapter based on FusionCache, we'll have more features anyway.

Yep, more features: read on.

> [!NOTE]  
> FusionCache is the **first** 3rd party implementation of HybridCache from Microsoft. But not just that: in a strange turn of events, since at the time of this writing (Jan 2025) Microsoft has not yet released their default implementation, FusionCache is the **first** production-ready implementation of HybridCache **at all**, including the one by Microsoft itself. Quite bonkers 😬

## 🆎 Feature Comparison

Let's see which features are on the table.

For the Microsoft implementation, the features will be:
- cache stampede protection (also [in FusionCache](CacheStampede.md))
- usable as L1 only (memory) or L1+L2 (memory + distributed) (also [in FusionCache](CacheLevels.md))
- multi-node notifications (also [in FusionCache](Backplane.md))
- tagging (also [in FusionCache](Tagging.md))
- serialization compression (not there yet, but already working on it)

FusionCache on the other hand has more, like:
- [fail-safe](FailSafe.md)
- [soft/hard timeouts](Timeouts.md)
- [adaptive caching](AdaptiveCaching.md)
- [conditional refresh](ConditionalRefresh.md)
- [eager refresh](EagerRefresh.md)
- [auto-recovery](AutoRecovery.md)
- [multiple named caches](NamedCaches.md)
- [clear](Clear.md)
- [advanced logging](Logging.md)
- [events](Events.md)
- [background distributed operations](BackgroundDistributedOperations.md)
- [full OpenTelemetry support](OpenTelemetry.md)
- the API is both [sync+async](CoreMethods.md) (HybridCache is async-only)

## 🚀 I Want Moar

Being able to use FusionCache "as" HybridCache means we'll also have the power of FusionCache itself, even when using it via the `HybridCache` abstraction.

This includes the resiliency of [fail-safe](FailSafe.md), the speed of [soft/hard timeouts](Timeouts.md) and [eager-refresh](EagerRefresh.md), the automatic synchronization of the [backplane](Backplane.md), the self-healing power of [auto-recovery](AutoRecovery.md), the full observability of the native [OpenTelemetry support](OpenTelemetry.md) and more.

Oh (x2), and we'll be even able to read and write from **BOTH** at the **SAME TIME**, fully protected from Cache Stampede! Yup, this means that when doing `hybridCache.GetOrCreateAsync("foo", ...)` at the same time as `fusionCache.GetOrSetAsync("foo", ...)`, they both will do only ONE database call, at all, among the 2 of them.

Oh (x3), and since FusionCache supports both the sync and async programming model in a unified way (while HybridCache only supports the async one), this also means that Cache Stampede protection and every other feature will work perfectly well even when calling at the same time:
- `hybridCache.GetOrCreateAsync("foo", ...)` (async call from the HybridCache adapter)
- `fusionCache.GetOrSet("foo", ...)` (sync call from FusionCache directly)

They'll be both not just protected from Cache Stampede automatically, but among themselves: this means that accross both the HybridCache adapter instance and the FusionCache instance, only 1 database call will be executed.

Nice 😬

## 🚀 I Said Moar!

Ok, here's something crazy to think about.

The `HybridCache` implementation from Microsoft currently available (it's in preview, the GA is not out yet) has some limitations, in particular:
- **NO L2 OPT-OUT**: there's no way to control the use of `IDistributedCache` or not. If it's registered in the DI container it will be used, otherwise it will not, meaning if another component needs it, you'll be then forced to use it in HybridCache too
- **SINGLE INSTANCE:** it does not support multiple named caches, there can be only one
- **NO KEYED SERVICES**: since it does not support multiple caches, it means it cannot support Microsoft's own [Keyed Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-9.0#keyed-services)

Now these are the limitation of the (current) HybridCache _implementation_ , not the _abstraction_: does this mean that when using FusionCache via the HybridCache adapter, we can go above and beyound those limits?

Yup 🎉

First: since the registration is the one for FusionCache, this means we have total control over what components to use (see the builder [here](DependencyInjection.md)), so we are not forced to use an L2 just because there's an `IDistributedCache` registered in the DI container.

Second: thanks to the [Named Caches](NamedCaches.md) feature of FusionCache, we can register more than one, and each of them can be exposed via the adapter.

But how can we _access_ them separately in our code?

Easy, via Keyed Services!

Instead of registering it like this:

```csharp
services.AddFusionCache()
	.AsHybridCache();
```

and then request it via `serviceProvider.GetRequiredService<HybridCache>()` or via a param injection like this:

```csharp
public class SomeService(HybridCache cache) {
    ...
}
```

we can register it like this:

```csharp
services.AddFusionCache()
	.AsKeyedHybridCache("Foo");
```

and then request it via `serviceProvider.GetRequiredKeyedService<HybridCache>("Foo")` or via a param injection like this:

```csharp
public class SomeService([FromKeyedServices("Foo")] HybridCache cache) {
    ...
}
```

And is it possible to do both at the same time? Of course, simply register one FusionCache with `.AsHybridCache()` and or more with `.AsKeyedHybridCache(...)`, that's it.

Boom!

## 🚳 Limitations

The HybridCache API surface area is more limited: for example for each `GetOrCreate()` call we can pass a `HybridCacheEntryOptions` object instead of a `FusionCacheEntryOptions` object.

Because of this, when using FusionCache via the HybridCache adapter we can configure all of this goodness only at startup, and not on a per-call basis: still, it's a lot of power to have available for when we need or want to depend on the Microsoft abstraction.

But there's more: the `HybridCacheEntryOptions` type already allows for some level of flexibility, like controlling Memory/Distributed Read/Write per-call. Therefore the FusionCache adapter automatically maps all of the options to the corresponding ones in FusionCache, and will work flawlessly.

As an example, using `HybridCacheEntryFlags.DisableLocalCacheRead` in the `HybridCacheEntryOptions` becomes `SkipMemoryCacheRead` in `FusionCacheEntryOptions`, again all automatically.

## 🙏 Microsoft (and Marc) and OSS

To me, this can be a good example of what it may look like when Microsoft and the OSS community have a constructive dialog.

First and foremost many thanks to the HybridCache lead @mgravell for the [openness](https://github.com/dotnet/aspnetcore/issues/53255#issuecomment-1941156200), the [back](https://github.com/dotnet/aspnetcore/issues/53255#issuecomment-1945153484) and [forth](https://github.com/microsoft/garnet/issues/85#issuecomment-2014683897) and the time spent reading my [mega-comments](https://github.com/dotnet/aspnetcore/issues/53255#issuecomment-1944576582).

I think this can be a really good starting point, and future endeavours by Microsoft to come up with new core components already existing in the OSS space should go even beyond, and be even more collaborative: frequent meetings between the maintainers of the main OSS packages in that space, to be all aligned and have a shared vision while respecting each other's work.

With that, Microsoft can provide - if it makes sense in each case - a basic default implementation but even more importantly a shared abstraction, which btw **must** be designed to allow augmentation by the OSS alternatives: in doing so Microsoft must inevitably accept strong inputs from the OSS community to do this well, and yes I know it takes time and resources, but imho it's the only way to make it work.

Then it should also give visibility to the OSS alternatives (in the main docs, samples, videos, etc), and encourage all .NET users to discover and try the alternatives: in doing so they will not lose anything, and instead in turn the .NET ecosystem as a whole will thrive.

In the past this has not always been the case, but the future _may_ be different.

Just my 2 cents.