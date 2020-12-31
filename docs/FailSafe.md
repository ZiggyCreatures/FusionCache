<div style="text-align:center;">

![FusionCache logo](logo-128x128.png)

</div>

# :bomb: Fail-Safe

Using a cache in general - not necessarily FusionCache - is a good thing because it makes our systems **way faster**, even though it means using values that may be **a little bit stale**.

That's ok in most situations, and is the tradeoff we accept to obtain better performance.

Setting an **expiration** to a cache entry is also good thing: it prevents us from using a piece of data for too long, giving us the ability to refresh the value from its source - say, a database - after some time.

Sometimes though when the entry **expires** and we go get an updated value from the database, things **may go bad**: the database may be totally down or overloaded, there may be temporary network congestion or really anything else bad that can happen.

In these cases what happens is your service will be down or super slow, like this:

![Without Fail Safe](images/stepbystep-01-memorycache.png)

Typically in these situations we would be out of luck because the expired value **is already gone for good**, even though we would have preferred to use it for a little bit longer (after all, we are using a cache because we are ok with using slightly stale data, that's the whole point), instead of having to most probably surface the error to our users.

Wouldn't it be nice to have a way to kepp using a stale value for a little longer?

This is exactly what the **Fail-Safe** mechanism does.

It allows us to specify for how long each entry should be kept around in case of problems, while at the same time let them *logically* expire at the right time.

To do that we have to simply set 3 things on a `FusionCacheEntryOptions` object:

- `IsFailSafeEnabled`: enable or disable the mechanism
- `FailSafeMaxDuration`: how long a value should be kept around, even after its *logical* expiration
- `FailSafeThrottleDuration`: how long an expired value (used because of a fail-safe *activation*) should be temporarily considered as non expired, to avoid going to check the database for every consecutive request of an expired value

The end result (also adding some [timeouts](Timeouts.md)) would be something like this:

![With Fail Safe](images/stepbystep-04-factorytimeouts.png)

Isn't it great?

## A practical example

Suppose we want to put something in the cache that should expire after `5` minutes, but we also want to be able to use the value for a total of `2` hours in case of problems, even if it is logically expired. Also, in case fail-safe will be *activated*, we want to use the expired value for at least `1` min before checking again.

So let's do this:

```csharp
product = cache.GetOrSet<Product>(
    "product:123",
    _ => GetProductFromDb(123),
    options => options
        .SetDuration(TimeSpan.FromMinutes(5))
        .SetFailSafe(true, TimeSpan.FromHours(2), TimeSpan.FromMinutes(1))
);
```

The value is not inthe cache, so the factory (`GetProductFromDb(123)`) is called and the product is put into the cache with the options specified and returned.

Everything is fine.

We then wait `2` minutes and call the same code again: the value is not expired (remember, `5` min `Duration`) so the value is immediately returned.

So we wait another `4` min (total `6` min) and call the same code: the value is not in the cache (expired after `5` min) so the factory is called again, but this time **the database is down**: whoops.

Normally an exception would be thrown, and we would have to handle that with an error page or similar but, since we enabled the fail-safe mechanism, the expired value will be put back into the cache with a new cache duration of `1` min (`FailSafeThrottleDuration`).

From now on everything will repeat in the same way, without throwing exceptions, until one of two things occurs:
1) the factory completes successfully again: in this case the cache will be updated with the new value + a duration of `5` min (`Duration`)
2) a total `2` hours is passed (`FailSafeMaxDuration`): the value is actually deleted from the cache, like, **for real**

:bulb: Setting a `FailSafeMaxDuration` is useful to avoid using a stale value for too long: you can set this for as long as you want, even a month or a year.