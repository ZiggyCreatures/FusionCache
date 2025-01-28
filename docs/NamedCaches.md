<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 📛 Named Caches

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to register, configure and request multiple named caches: simply register each one and give them a different name (and configuration) and they'll all just work, both via `IFusionCacheProvider` and via [Keyed Services](DependencyInjection.md#-keyed-services). |

Just like with the standard [named http clients](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-7.0#named-clients) in .NET, with FusionCache it's possible to have multiple named caches.

Thanks to the native [builder](DependencyInjection.md) support, it's very easy to configure different caches identified by different names.

Instead of using:

```csharp
services.AddFusionCache();
```

to register the so-called _default cache_, we just use:

```csharp
services.AddFusionCache("Products");
```

to register a cache named `"Products"`.

We can register more than one of course:

```csharp
services.AddFusionCache("Products");
services.AddFusionCache("Customers");
```

To use FusionCache in, say, a controller we would normally add an `IFusionCache` param in a controller constructor:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _cache;

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

But of course we cannot do that with multiple named caches, because... which one would be picked?

The aforementioned [named http clients](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-7.0#named-clients) approch solves this by simply not having a param of type `HttpClient`, but of type `IHttpClientFactory` and then ask it to create a client with `CreateClient(name)`.

In FusionCache it's the same, and we can simply change the param from `IFusionCache` to `IFusionCacheProvider`, and then ask it to get a cache with `GetCache(name)`.

So basically:

- `IHttpClientFactory` -> `CreateClient(name)` -> `HttpClient`
- `IFusionCacheProvider` -> `GetCache(name)` -> `IFusionCache`

By using a similar approach, hopefully we should feel at home 😊.

Here's the example above, updated:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _cache;

    public MyController(IFusionCacheProvider cacheProvider)
    {
        _cache = cacheProvider.GetCache("Products");
    }

    // ...
}
```

And of course if you want to access 2 caches at the same time you can just do it:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _productsCache;
    private readonly IFusionCache _customersCache;

    public MyController(IFusionCacheProvider cacheProvider)
    {
        _productsCache = cacheProvider.GetCache("Products");
        _customersCache = cacheProvider.GetCache("Customers");
    }

    // ...
}
```

## ⭐ Default Cache

But wait, does the "normal" way of using FusionCache - by declaring a param of type `IFusionCache` - still works?

Of course it does!

It will just return the default cache, that is the one registered via `services.AddFusionCache()` without specifying a name, and we will not have strange runtime surprises of getting back one of the named caches, randomly.

Our existing code that was not using named caches will still work, without changes 🎉

**ℹ NOTE:** nitpicking corner here, but the _default cache_ has a cache name equals to `FusionCacheOptions.DefaultCacheName`.

## ⭐ Default Cache + Named Caches

It is also possible to register and use the default cache, along with other named caches at the same time.

Just do it:

```csharp
services.AddFusionCache();
services.AddFusionCache("Products");
services.AddFusionCache("Customers");
```

But how can we access the default cache, if we now need to specify a name?

Simple, we can choose between:
- a param of type `IFusionCache`
- just ask our new friend `IFusionCacheProvider` for the default cache, explicitly

So we can either do this:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _defaultCache;
    private readonly IFusionCache _productsCache;

    public MyController(IFusionCache defaultCache, IFusionCacheProvider cacheProvider)
    {
        _defaultCache = defaultCache;
        _productsCache = cacheProvider.GetCache("Products");
    }
}
```

or this:

```csharp
public class MyController : Controller
{
    private readonly IFusionCache _defaultCache;
    private readonly IFusionCache _productsCache;

    public MyController(IFusionCacheProvider cacheProvider)
    {
        _defaultCache = cacheProvider.GetDefaultCache();
        _productsCache = cacheProvider.GetCache("Products");
    }
}
```

FusionCache with the default cache, multiple named caches, the new `IFusionCacheProvider` and the DI container in general all work together harmoniously without unwanted runtime surprises or problems 🥳

## ⚙️ Different configurations

It goes without saying, but better be explicit: thanks to the [Builder](DependencyInjection.md) support we can configure each cache differently, including the default one.

For example:

```csharp
// DEFAULT CACHE
services.AddFusionCache()
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(10);
    })
;

// PRODUCTS CACHE
services.AddFusionCache("Products")
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(20);
    })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(new RedisCache(new RedisCacheOptions {
        Configuration = "PRODUCTS_CACHE_CONNECTION"
    }))
;

// CUSTOMERS CACHE
services.AddFusionCache("Customers")
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(30);
    })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(new RedisCache(new RedisCacheOptions {
        Configuration = "CUSTOMERS_CACHE_CONNECTION"
    }))
;
```

Here we registered, on top of some default entry options:
- the **default cache** to use only the memory cache
- the **products cache** to use memory + distributed, pointing to one Redis instance
- the **customers cache** to use memory + distributed, pointing to anotner Redis instance

## ⚙️ With Registered _Whatever_

We can also just use the same Redis instance, and maybe register a common serializer and a common distributed cache (and, why not, a common backplane) in the DI container, configure them once and just use the **registered** components where we want by just saying `WithRegisteredXyz()`:

```csharp
// COMPONENTS
services.AddFusionCacheSystemTextJsonSerializer();
services.AddStackExchangeRedisCache(opt => opt.Configuration = "REDIS_CONNECTION");
services.AddFusionCacheStackExchangeRedisBackplane(opt => opt.Configuration = "REDIS_CONNECTION");

// DEFAULT CACHE
services.AddFusionCache()
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(10);
    })
;

// PRODUCTS CACHE
services.AddFusionCache("Products")
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(20);
    })
    .WithCacheKeyPrefix()
    .WithRegisteredSerializer()
    .WithRegisteredDistributedCache()
    .WithRegisteredBackplane()
;

// CUSTOMERS CACHE
services.AddFusionCache("Customers")
    .WithDefaultEntryOptions(opt =>
    {
        opt.Duration = TimeSpan.FromSeconds(30);
    })
    .WithCacheKeyPrefix()
    .WithRegisteredSerializer()
    .WithRegisteredDistributedCache()
    .WithRegisteredBackplane()
;
```

## 💥 Collisions?

But wait a minute: if the distributed cache (a Redis instance in this case) is the same, does it mean that 2 cache entries from different caches but with the same cache key would collide?

Meaning, something like this:

```csharp
_productsCache.Set("Foo123", myProduct)
_customersCache.Set("Foo123", myCustomer)
```

Normally the answer would be yes, but in this case is a resounding _"nope!"_ because our new friend `CacheKeyPrefix` enters the scene.

### 🔑 Cache Key Prefix

If you notice in the code above we also added `WithCacheKeyPrefix()`: that tells FusionCache to add a prefix to each cache key we will pass to it, solving the issue automatically.

By default, when no specific prefix is specified, the `CacheName` plus a little `":"` separator will be used.

Of course it can also be specified manually, by simply using the overload `WithCacheKeyPrefix(prefix)`.

Basically when doing `_productsCache.Set("Foo123", myProduct)` from the example above the actual cache key used in both the underlying memory and distributed cache will be turned from `"Foo123"` to `"Products:Foo123"`, automatically and transparently avoiding any collision between different cache entries from different caches. The transformed cache key will be used consistently throughout the entire flow: memory cache, distributed cache, events, etc.

Ain't it nice 😬 ?
