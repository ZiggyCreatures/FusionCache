<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🔃 Dependency Injection

For some time now .NET added support for [Dependency Injection (DI)](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection), a design pattern to achieve a form of Inversion of Control (IoC) in our code.

This is a common way to handle creation, scope and dependencies handling that facilitates working with services in an easier and more flexible way.

## DI with FusionCache

It's very easy to work with FusionCache with DI: all we need to do is just register FusionCache like any other service and it will try to do its best to work with what is already registered.

In our startup phase just add this:

```csharp
services.AddFusionCache();
```

And FusionCache will be registered, ready to be used somewhere else in our code.

For example in an MVC controller we can just add an `IFusionCache` param in the constructor and it will be available to us, like this:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _cache;

    // THE cache PARAM WILL BE AUTOMATICALLY POPULATED BY THE DI FRAMEWORK
    public MyController(IFusionCache cache)
    {
        _cache = cache;
    }

    [Route("product/{id:int}")]
    public IActionResult Product(int id)
    {
        var product = _cache.GetOrSet<Product>(
            $"product:{id}",
            _ => GetProductFromDb(id),
            TimeSpan.FromSeconds(30)
        );

        return View(product);
    }
}
```

In this way the `cache` param will be automatically populated by the DI framework.

## Standard Behaviour

Normally, if we just use `services.AddFusionCache();` in the startup phase what will happen is that FusionCache will use a default behaviour, like:

- it will name the cache with a default value of `"FusionCache"`
- it will look for a registered distributed cache (any implementation of `IDistributedCache`) and, if it also finds a valid serializer (any implementation of `IFusionCacheSerializer`), will add a [2nd level](CacheLevels.md) for us, all automatically
- it will look for a registered [backplane](Backplane.md) (any implementation of `IFusionCacheBackplane`) and use it, again all automatically
- it will look for registered FusionCache [plugins](Plugins.md) (all registered implementations of `IFusionCachePlugin`) and add + initialize them

If we want though, we can customize that behaviour by changing the options used (`FusionCacheOptions`) and a couple of other things: keep reading to find out more.

## Options

Every FusionCache instance can be configured by passing some [options](Options.md) to the constructor, like this:

```csharp
var cache = new FusionCache(new FusionCacheOptions() {
	CacheName = "MyCache",
	DistributedCacheErrorsLogLevel = LogLevel.Warning,
	// CHANGE OTHER OPTIONS HERE
});
```

In a DI approach we can do the same by using the [standard .NET approach](https://docs.microsoft.com/en-us/dotnet/core/extensions/options-library-authors):

```csharp
services.AddFusionCache(options => {
	options.CacheName = "MyCache";
	options.DistributedCacheErrorsLogLevel = LogLevel.Warning;
	// CHANGE OTHER OPTIONS HERE
});
```

## Customization

Some things though are not options of how FusionCache works, but just of how it should behave during the DI-based instantiation.
Examples can be things like:
- should it automatically use a distributed cache if there's one registered? That would be pretty common
- should it ignore though the distributed cache, even if it finds one registered, in case it's the "fake" one (`MemoryDistributedCache`, which is not really a distributed cache) if there's one registered? That would also be pretty common, to avoid doing extra work for a 2nd level which is not really useful
- etc

Because of that it is possible to specify a couple of additional params during registration, like `useDistributedCacheIfAvailable` or `ignoreMemoryDistributedCache`.

Here's an example to disable the automatic use of a 2nd layer, even if there is an `IDistributedCache` service already registered:

```csharp
services.AddFusionCache(
    options => {
	    // CHANGE OPTIONS HERE
    },
    false
);
```

or, to be more explicit:

```csharp
services.AddFusionCache(
    options => {
	    // CHANGE OPTIONS HERE
    },
    useDistributedCacheIfAvailable: false
);
```

Finally it is also possible to customize our FusionCache instance even more *after* the initial automatic setup, thanks to a lambda which allows us to fine tune whatever we want on the instance that has been just created:

```csharp
services.AddFusionCache(
    options => {
	    // CHANGE OPTIONS HERE
    },
    setupCacheAction: (serviceProvider, cache) => {
        // CUSTOM SETUP
        var now = DateTime.UtcNow;
        cache.CacheName = $"Cache_{now.Year}_{now.Month}_{now.Day}";

        // CUSTOM INTEGRATION VIA SERVICE PROVIDER
        var myThing = serviceProvider.GetService<IMyThing>();
        myThing.DoSomethingWithFusionCache(cache);
    }
);
```

With these approaches available we should be able to achieve whatever we want.

But let's say we want to have even more control, for example in how FusionCache instances are actually created, and for some reason we don't want to use the standard flow: can we do that?

Yep, keep reading.

## Advanced Customization

This is not specific to FusionCache, but is more like a general approach we can use with the DI framework in .NET.

We can simply NOT call the standard `services.AddFusionCache();` method and instead do it ourselves, manually.

It will be something like this:

```csharp
services.TryAdd(ServiceDescriptor.Singleton<IFusionCache>(serviceProvider =>
{
    // INTERACT WITH THE DI FRAMEWORK VIA SERVICE PROVIDER
    var logger = serviceProvider.GetService<ILogger<FusionCache>>();

    // CREATE THE CACHE INSTANCE, PASSING ALONG THE LOGGER OBTAINED ABOVE
    var cache = new FusionCache(
        new FusionCacheOptions() {
            CacheName = "MyAwesomeCache"
        },
        logger: logger
    );
    
    // ADD THE BACKPLANE, BUT ONLY ON FRIDAY (BECAUSE, YOU KNOW, WHATEVER :D)
    if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Friday) {
        var backplane = serviceProvider.GetService<IFusionCacheBackplane>();
        if (backplane is object)
        {
            cache.SetupBackplane(backplane);
        }
    }

    // MAYBE DO OTHER THINGS

    // RETURN THE INSTANCE
    return cache;
};
```

In this way we are free to really do whatever we want, even crazy things like adding a backplane but only on Friday 😜.

The downside of this approach though is that we loose the automatic behaviours included in the standard way of registering it, like plugins discovery and auto-setup, etc and any future features that will be added in the standard way: of course we can do those things ourselves manually, there's nothing magic about it, but still it's important to keep that in mind.
