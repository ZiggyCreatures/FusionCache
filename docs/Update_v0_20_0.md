<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🆙 Update to v0.20.0

With the `v0.20.0` release, FusionCache introduced an important feature: [Builder Pattern](DependencyInjection.md) support.

This has made the experience of using FusionCache much easier and clearer, allowing us to be more precise in what we want to do and also allowing us to work with multiple [Named Caches](NamedCaches.md), also introduced in `v0.20.0`.

## ⚠ Breaking Change

To have the best possible long term experience, one small breaking change for one particular use case was needed.

What follows is a quick TL;DR version, followed by a longer version if you want to know more.

## Short Version

If previously we were doing this:

```csharp
services.AddFusionCache();
```

now we should simply add a call to `TryWithAutoSetup()`, and do this:

```csharp
services.AddFusionCache().TryWithAutoSetup();
```

And everything will remain the same as before.

Any other update that may be needed will be highlighted as a warning when we compile, so we can follow the instructions and move forward.

The end.

## Long Version

Before `v0.20.0`, there was only one extension method available to register FusionCache in the DI container:

```csharp
public static IServiceCollection AddFusionCache(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null, bool useDistributedCacheIfAvailable = true, bool ignoreMemoryDistributedCache = true, Action<IServiceProvider, IFusionCache>? setupCacheAction = null);
```

This could have been used like this:

```csharp
services.AddFusionCache(opt =>
{
    opt.BackplaneAutoRecoveryMaxItems = 123;
});
```

By calling this method, apart from configuring one option, the **implicit** behavior was that all the registered and compatible components - like the distributed cache, the backplane, the plugins and so on - would have been auto-magically discovered in the DI container and used by FusionCache.

With the introduction of the builder pattern though, we now have to be **explicit** in what we want to use, and with the above call we now have **just the bare minimum**, basically the memory cache: everything else must be explicitly declared via one of the many methods available on the builder, like `WithRegisteredDistributedCache(...)`, `WithBackplane(...)`, `WithPlugin(...)`, `WithAllRegisteredPlugins()` and so on.

### But...

But wait, we may think _"wait, but if there are components registered in the DI container, why would we need to explicitly tell FusionCache to use them?"_ and the answer to that is that some of them, like the distributed cache (via the core `IDistributedCache` interface) are _generic_ components of .NET and not _specific_ to FusionCache: having them registered in the DI container does NOT necessarily mean that we want them to be used with FusionCache.

Therefore so we have to be specific.

### Ok, but...

Then we may think _"ok, makes sense, but what about components that are specific to FusionCache? If they have been registered in the DI container, then we [surely](https://www.youtube.com/watch?v=KM2K7sV-K74) would want to use them right?"_ and to that, the answer is still no, not necessarily: that's because FusionCache, again from `v0.20.0`, also added support for multiple [Named Caches](NamedCaches.md) and if you have multiple different caches you may not need them all to be configured in the same way. For example thinking about the configuration of the distributed cache, it may be very likely that different FusionCache instances may need to go to different distributed caches.

Ok, so now an idea may be that _"we can have auto-magic behavior with the default cache, and no auto-magic behavior with a named cache"_ but then we would have different behaviors to remember for each kind of FusionCache registration (default and named) and that would create confusion: uniformity here is definitely the better choice.

Therefore, again, we have to be specific.


### Makes sense: so what is the problem?

For now, the old ext method will still be there (so our code still compiles) and marked as `[Obsolete]`, with helpful instructions on what to do to move to the new world, so there can be no confusion.

But - and this is key - since all the params of that old ext method are _optional_, we could have also called it like this:

```csharp
services.AddFusionCache();
```

This should be pretty rare expect for quick demos, since in a real world scenario we would want to configure something (like some options, etc), right?

But still, this is a possibility.

And what is the problem with the call above? With the builder pattern support added, there's now a new parameterless `AddFusionCache()` method available which works as the base for all the builder stuff, and that now will take precedence over the old one.

So, if we have existing code calling the old method *WITH at least one argument (or more)*, after the update to `v0.20.0` the call will still be made to the old one, and the compilation will warn us about the `[Obsolete]` usage and the message will tell us what to do.

All good.

But if we have existing code calling the old method *WITHOUT any argument* (so, in a parameterless way) after the update to `v0.20.0` the call suddenly become the new one, and the behavior will change from "auto-magic" to "bare-minimum", without any warning (sadly it's not detectable in any way).

### Ok, now what?

Having said all of this, how much complex it is to just have the previous auto-magic behavior, if we really want to?

Easy peasy, just add `TryWithAutoSetup()`:

```csharp
services.AddFusionCache().TryWithAutoSetup();
```

and that's all, the old auto-magic behavior will be there.

### One more thing

When using `TryWithAutoSetup()` you can start chaining other builder methods to do more, like this:

```csharp
services.AddFusionCache()
    .TryWithAutoSetup()
    .WithOptions(opt =>
    {
        opt.BackplaneAutoRecoveryMaxItems = 123;
    })
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(30);
        opt.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
    })
;
```
