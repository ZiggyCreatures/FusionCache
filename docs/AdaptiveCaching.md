<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🧙‍♂️ Adaptive Caching

| ⚡ TL;DR (quick version) |
| -------- |
| It's possible to change entry options in-flight, inside of a factory, to make them adapt specifically to the value being cached. Just use the factory signature with the context and the cancellation token. |

Sometimes when you are caching a piece of data with the `GetOrSet` method you don't know upfront what the cache duration should be: this may happen because the cache duration depends on the object being cached itself.

Some examples may be:

- **📰 news articles**: a fresh article that has just been published on a news site may very well receive some updates very soon, maybe because it has been published very fast to get a news out but then some typos have been found or a slight change is needed. In this case it would be nice to be able to cache fresh content for a low amount of time like `30 sec` or `1 min`, whereas an old article that has not been touched for a year may very well be cached for like `10 min` or more, because it will be very unlikely that a quick update will be needed after all this time
- **🔑 auth tokens**: auth tokens or similar pieces of data typically have an associated expiration, and it would be nice to cache them accordingly
- **🌍 HTTP api results**: if your remote data source is not a database but, for example, an HTTP service like a rest endpoint the HTTP response may contain caching-related informations in the form of the `Cache-Control` HTTP header, which in turns typically contains the `max-age` directive. It would be nice to use that as the duration for the cache, respecting the service specification for how much a piece of data should be cached

The problem in all these cases is that when you call `GetOrSet` you'll provide both the factory (the function to be executed to get the data) and some options for that call (via `FusionCacheEntryOptions`), but as we said some of the options (eg: the `Duration`) may depend on the result of the factory, which has not run yet.

It seems like a chicken and egg problem, right?

Thankfully we have a solution: enter **adaptive caching**.


## 👩‍🏫 How it works

When calling `GetOrSet` you can choose different overloads, and the ones with a factory are available in 2 factory flavors: one with a *context* (of type `FusionCacheFactoryExecutionContext`) and one without it.

In the ones **with** the context you can simply change the context's `Options` property however you like.

Here are 2 examples, with and without the *context* object.


### 👩‍💻 Example: without adaptive caching

As you can see we are specifying the factory as a lambda that takes as input only a cancellation token `ct` (of type `CancellationToken`) and nothing else.

```csharp
var id = 42;

// WITHOUT ADAPTIVE CACHING: THE DURATION IS FIXED TO 1 MIN
var product = cache.GetOrSet<Product>(
    $"product:{id}",
    ct => GetProductFromDb(id, ct),
    options => options.SetDuration(TimeSpan.FromMinutes(1)) // FIXED: 1 MIN
);
```

### 👩‍💻 Example: with adaptive caching

As you can see we are specifying the factory as a lambda that takes as input both a context `ctx` (of type `FusionCacheFactoryExecutionContext`) and a cancellation token `ct` (of type `CancellationToken`), so that we are able to change the options inside the factory itself.

```csharp
var id = 42;

// WITH ADAPTIVE CACHING: THE DURATION DEPENDS ON THE OBJECT BEING CACHED
var product = cache.GetOrSet<Product>(
    $"product:{id}",
    (ctx, ct) => {
        var product = GetProductFromDb(id, ct);

        if (product is null) {
            // CACHE null FOR 5 minutes
            ctx.Options.Duration = TimeSpan.FromMinutes(5);
        } else if (product.LastUpdatedAt > DateTime.UtcNow.AddDays(-1)) {
            // CACHE PRODUCTS UPDATED IN THE LAST DAY FOR 1 MIN
            ctx.Options.Duration = TimeSpan.FromMinutes(1);
        } else if (product.LastUpdatedAt > DateTime.UtcNow.AddDays(-10)) {
            // CACHE PRODUCTS UPDATED IN THE LAST 10 DAYS FOR 10 MIN
            ctx.Options.Duration = TimeSpan.FromMinutes(10);
        } else {
            // CACHE ANY OLDER PRODUCT FOR 30 MIN
            ctx.Options.Duration = TimeSpan.FromMinutes(30);
        }

        return product;
    },
    options => options.SetDuration(TimeSpan.FromMinutes(1)) // DEFAULT: 1 MIN
);
```

You may change other options too, like the `Priority` for example.

Of course ther are some changes that wouldn't make much sense: if for example we change the `FactorySoftTimeout` after the factory has been already executed we shouldn't expect much to happen, right 😅 ?

## Use adaptive caching to skip cache write

Adaptive caching can also be used to skip cache write altogether. This pattern is useful if you want to skip cache write for some specific values returned by the factory.

Here is an example:

```csharp
var id = 42;

// USE ADAPTIVE CACHING TO SKIP CACHE WRITE FOR SOME VALUES RETURNED BY THE FACTORY
var product = cache.GetOrSet<Product>(
    $"product:{id}",
    (ctx, ct) => {
        var product = GetProductFromDb(id, ct);

        if (product.IsFlashSale) {
            // THE PRICE AND STOCK OF FLASH SALE PRODUCTS CHANGE CONSTANTLY, SO WE DON'T WANT TO CACHE THEM TO AVOID SERVING STALE DATA
            ctx.Options.SkipMemoryCacheWrite = true;
			ctx.Options.SkipDistributedCacheWrite = true;
        } 

        return product;
    },
    options => options.SetDuration(TimeSpan.FromMinutes(1)) // DEFAULT: 1 MIN
);
```

## ⏱ Timeouts & Background Factory Completion

Short version: everything works as expected!

Longer version: there are times when, if a factory is taking a lot of time to complete (because maybe the database is overloaded or there's a temporary network congestion), you would prefer stale data very fast instead of fresh data but slowly.

A nice feature of FusionCache is the ability to set soft/hard [timeouts](Timeouts.md) to get the best of both worlds: an always fast response with fresh data as soon as possible. As we know when a timeout kicks in, the running factory is not stopped but kept running in the background.

A question we may ask ourselves is if adaptive caching would still work in that scenario.

The answer is **absolutely yes**: you don't need to change anything, do extra steps or give up on some features 🎉