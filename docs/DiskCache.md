<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 💾 Disk Cache

| ⚡ TL;DR (quick version) |
| -------- |
| When we want to ease cold starts but don't want need multi-nodes support, we can use an implementation of `IDistributedCache` based on SQLite to achieve that. |

In certain situations we may like to have some of the benefits of a 2nd level like better cold starts (when the memory cache is initially empty) but at the same time we don't want to have a separate **actual** distributed cache to handle, or we simply cannot have it. A good example of that may be a mobile app, where everything should be self contained.

In those situations we may want a distributed cache that is "not really distributed", something like an implementation of `IDistributedCache` that reads and writes directly to one or more local files: makes sense, right?

Yes, kinda, but there is more to that.

We should also think about the details, about all the things it should handle for a real-real world usage:
- have the ability to read and write data in a **persistent** way to local files (so the cached data will survive restarts)
- ability to prevent **data corruption** when writing to disk
- support some form of **compression**, to avoid wasting too much space on disk
- support **concurrent** access without deadlocks, starvations and whatnot
- be **fast** and **resource optimized**, so to consume as little cpu cycles and memory as possible
- and probably something more that I'm forgetting

That's a lot to do... but wait a sec, isn't that exactly what a **database** is?

Yes, yes it is!

## 😐 An actual database? Are you kidding me?

Of course I'm not suggesting to install (and manage) a local MySql/SqlServer/PostgreSQL instance or similar, that would be hard to do in most cases, impossible in others and frankly overkill.

So, what should we use?

## 💪 SQLite to the rescue!

If case you didn't know it yet, [SQLite](https://www.sqlite.org/) is an incredible piece of software:
- it's one of the highest quality software [ever produced](https://www.i-programmer.info/news/84-database/15609-in-praise-of-sqlite.html)
- it's used in production on [billions of devices](https://www.sqlite.org/mostdeployed.html), with a higher instance count than all the other database engines, combined
- it's [fully tested](https://www.sqlite.org/testing.html), with millions of test cases, 100% coverage, fuzz tests and more, way more (the link is a good read, I suggest to take a look at it)
- it's very robust and fully [transactional](https://www.sqlite.org/hirely.html), no worries about [data corruption](https://www.sqlite.org/transactional.html)
- it's fast, like [really really fast](https://www.sqlite.org/fasterthanfs.html). Like, 35% faster than direct file I/O!
- has a very [small footprint](https://www.sqlite.org/footprint.html)
- the [license](https://www.sqlite.org/copyright.html) is as free and open as it can get

Ok so SQLite is the best, how can we use it as the 2nd level?

## 👩‍🏫 Ok but how?

Luckily someone in the community created an implementation of `IDistributedCache` based on SQLite, and released it as the [NeoSmart.Caching.Sqlite](https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/) Nuget package (GitHub repo [here](https://github.com/neosmart/AspSqliteCache)).

The package:
- supports both the sync and async models natively, meaning it's not doing async-over-sync or vice versa, but a real double impl (like FusionCache does) which is very nice and will use the underlying system resources best
- uses a [pooling mechanism](https://github.com/neosmart/AspSqliteCache/blob/master/SqliteCache/DbCommandPool.cs) which means the memory allocation will be lower since they reuse existing objects instead of creating new ones every time and consequently, because of that, less cpu usage in the long run because less pressure on the GC (Garbage Collector)
- supports `CancellationToken`s, meaning that it will gracefully handle cancellations in case it's needed, like for example a mobile app pause/shutdown events or similar

It's a really good package, let's see how to use it.

### 👩‍💻 Example

We simply use the `SqliteCache` implementation and we'll be good to go:

```csharp
services.AddFusionCache()
    .WithSerializer(
        new FusionCacheNewtonsoftJsonSerializer()
    )
    .WithDistributedCache(
        new SqliteCache(new SqliteCacheOptions { CachePath = "CACHE PATH" })
    )
;
```

Alternatively, we can register it as *THE* `IDistributedCache` implementation, and just tell FusionCache to use the registered one, whatever that may be:

```csharp
// REGISTER SQLITE AS THE IDistributedCache IMPL
services.AddSqliteCache(options => {
    options.CachePath = "CACHE PATH";
});

services.AddFusionCache()
    .WithSerializer(
        new FusionCacheNewtonsoftJsonSerializer()
    )
    // USE THE REGISTERED IDistributedCache IMPL
    .WithRegisteredDistributedCache()
;
```

If you like what you are seeing, remember to give that [repo](https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/) a star ⭐ and share it!
