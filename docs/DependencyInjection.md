<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🔃 Dependency Injection

In .NET there's full support for [Dependency Injection (DI)](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection), a design pattern to achieve a form of Inversion of Control (IoC) in our code.

This is a common way to handle creation, dependencies, scopes and disposal of resources that makes it easier and more flexible to work with any _service_ we may need.

| 🙋‍♂️ Updating from before `v0.20.0` ? please [read here](Update_v0_20_0.md). |
|:-------|

## FusionCache + DI

It's very easy to work with FusionCache when using DI: all we need to do is just register it like any other service and, if needed, configure it the way we want.

In our startup phase we just add this:

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

By simply calling `services.AddFusionCache()` we will have FusionCache configured with the default options and using only a memory cache (1st level) and nothing else: no distributed cache (2nd level), no backplane nor any plugin.

But usually we want to do more, right? Maybe add a distributed cache or configure some options.

For that we can use a **builder** approach.

## 👷‍♂️ Builder

By calling `services.AddFusionCache()` what we get back is an instance of `IFusionCacheBuilder` from which we have access to a lot of different extension methods with a [fluent interface](https://en.wikipedia.org/wiki/Fluent_interface) design, all readily available to do whatever we want.

### Configure options

To configure some cache-wide options we can use:

```csharp
services.AddFusionCache()
    .WithOptions(opt =>
    {
        opt.BackplaneAutoRecoveryMaxItems = 123;
    })
;
```

To configure some default entry options we can use:

```csharp
services.AddFusionCache()
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(30);
        opt.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
    })
;
```

Of course we can combine them (remember? fluent interface!):

```csharp
services.AddFusionCache()
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

### Configure components

Ok, these are various types of options: but what about sub-components like the memory cache, the distributed cache, the serializer, the backplane, etc?

FusionCache has a _unified_ approach, and everything is (hopefully) very uniform for each component.

For example for the memory cache we can tell FusionCache:

- `WithRegisteredMemoryCache()`: USE the one REGISTERED in the DI container (if not there, an exception will be thrown)
- `TryWithRegisteredMemoryCache()`: TRY TO USE the one REGISTERED in the DI container (if not there, no problem)
- `WithMemoryCache(IMemoryCache memoryCache)`: USE an instance that we provide DIRECTLY
- `WithMemoryCache(Func<IServiceProvider, IMemoryCache> factory)`: USE an instance built via a FACTORY that we provide

The same is available for other components, like the backplane for example:

- `WithRegisteredBackplane()`
- `TryWithRegisteredBackplane()`
- `WithBackplane(IFusionCacheBackplane backplane)`
- `WithBackplane(Func<IServiceProvider, IFusionCacheBackplane> factory)`

and so on.

This approach is currently available for:
- logger
- memory cache
- distributed cache + serializer
- backplane

### Configure distributed cache

A slightly particular case is the distributed cache, since it requires a serializer to do its job and there's a common case that we may want to ignore.

Because of this, in these methods there are some extra params like:
- `bool throwIfMissingSerializer`: tells FusionCache if it should throw in case if finds a valid distributed cache but no serializer, to avoid surprises down the road like _"I specified a distributed cache, but it's not using it and it didn't tell me anything, why?"_
- `bool ignoreMemoryDistributedCache`: tells FusionCache if it should accept an instance of `MemoryDistributedCache`, which is not really a distributed cache and is typically registered automatically by ASP.NET MVC without us being able to avoid it, and using it is just a waste of resources

### Configure plugins

Everything is the same, but since we can have multiple plugins some methods works in a "plural" way: for example we have `WithAllRegisteredPlugins()` which will use all of the registered `IFusionCachePlugin` services, not just one.

Also, we can call `WithPlugin(...)` multiple times and add multiple plugins to the same FusionCache instance.


### Post setup

Sometimes we may need to further customize a FusionCache instance in some ways.

To do that we have at our disposal a `WithPostSetup(Action<IServiceProvider, IFusionCache> action)` method where we can specify some custom logic to apply after the creation of that FusionCache instance.

### Auto setup

It's probably not that frequent, but let's say you would like FusionCache to try and look into the DI container and basically use everything it can. Found a backplane? Use it. Found a valid distributed cache + a valid serializer? Use them. Any plugin found? Yep, use them all.

In this way we don't have a lot of control about what has been done so we may have surprises at runtime, but if this is what we want then we can use the `TryWithAutoSetup()` method: as the name implies, it will _try_ to _automatically_ do a _setup_ of what it can find.

In more details, it will:
- try to look for a registered [logger](Logging.md) (any implementation of `ILogger<FusionCache>`), and use it
- try to look for a registered memory cache (any implementation of `IMemoryCache`), and use it
- try to look for a registered distributed cache (any implementation of `IDistributedCache`) and, if it also finds a valid serializer (any implementation of `IFusionCacheSerializer`), add add a [2nd level](CacheLevels.md)
- try to look for a registered [backplane](Backplane.md) (any implementation of `IFusionCacheBackplane`) and use it
- try to look for all registered FusionCache [plugins](Plugins.md) (all registered implementations of `IFusionCachePlugin`) and add + initialize them

## Registered components and direct instances

When specifying which components to use we have 2 choices:
1. tell FusionCache exactly what to use (either via an direct instance or a factory)
2. register a component in the DI container, then tell FusionCache to use what is registered

This is an example of the first approach, via a direct instance:

```csharp
services.AddFusionCache()
    .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
    {
        Configuration = "CONNECTION_STRING_HERE"
    }))
;
```

Again the first approach, but with a factory:

```csharp
services.AddFusionCache()
    .WithBackplane(serviceProvider => new RedisBackplane(new RedisBackplaneOptions
    {
        Configuration = serviceProvider.GetService<MyConfigStuff>().RedisConfig
    }))
;
```

And this is an example of the second approach:

```csharp
services.AddFusionCacheStackExchangeRedisBackplane(opt =>
{
    opt.Configuration = "CONNECTION_STRING_HERE";
});

services.AddFusionCache()
    .WithRegisteredBackplane()
;
```

Maybe we prefer flexibility in what we register, and then always use what is registered.

Maybe we prefer to directly specify what we want to use.

FusionCache supports both approaches, and let us choose what we prefer.