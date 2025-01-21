<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 💣 Fail-Safe

| ⚡ TL;DR (quick version) |
| -------- |
| By enabling fail-safe FusionCache can temporarily re-use an expired value in case of problems encountered while calling a factory for a refresh: this avoids problems to *bubble up* to the users, while automatically re-trying later to get a fresh version. |

Using a cache in general - not necessarily FusionCache - is a good thing because it makes our systems **way faster**, even though it means using values that may be **a little bit stale**.

That's ok in most situations, and is the tradeoff we accept to obtain better performance.

Setting an **expiration** to a cache entry is also good thing: it prevents us from using a piece of data for too long, giving us the ability to refresh the value from its source - say, a database - after some time.

Sometimes though when the entry **expires** and we go get an updated value from the database, things **may go bad**: the database may be totally down or overloaded, there may be temporary network congestion or really anything else bad that can happen, and this will result in the factory throwing an exception.

In these cases what happens is your service will be down or super slow, like this:

![Situation Without Fail Safe](images/stepbystep-01-memorycache.png)

Typically in these situations we would be out of luck because the expired value **is already gone for good**, even though we would have preferred to use it for a little bit longer, instead of having to most probably surface the error to our users. After all, we are using a cache because we are ok with using slightly stale data, that's the whole point.

Wouldn't it be nice to have a way to keep using a stale value for a little longer?

This is exactly what the **Fail-Safe** mechanism does.

It allows us to specify for how long each cache entry should be "kept around" after it expires, so that in case of problems (that is, when the factory throws an exception) we can re-use it that instead of having a factory exception bubble up to our calling code, all while at the same time let them *logically* expire at the right time.

To do that we simply have to enable it by using the `IsFailSafeEnabled` option on the `FusionCacheEntryOptions`.

Also, if we want, we can also set two additional options to have more control:
- `FailSafeThrottleDuration`: how long an expired value (used because of a fail-safe *activation*) should be temporarily considered as non-expired, to avoid going to check the database for every consecutive request
- `FailSafeMaxDuration`: how long a value should be kept around at most, after its *logical* expiration

> [!NOTE]
> Please note that fail-safe **must** be enabled when saving an entry in the cache for it to work, not just when getting a value from the cache.
> Also, since fail-safe is about a **fail** while executing a factory, it only works with the `GetOrSet` method (where there is a factory): to force getting a stale value with readonly methods (eg: `TryGet`/`GetOrDefault`) we can use another option, `AllowStaleOnReadOnly`.

The end result (also adding some [timeouts](Timeouts.md)) would be something like this:

![Situation With Fail Safe](images/stepbystep-04-factorytimeouts.png)

Isn't it great?

## 👩‍💻 A Practical Example

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

The value is not in the cache, so the factory (`GetProductFromDb(123)`) is called and the product is put into the cache with the options specified and returned.

Everything is fine.

We then wait `2` minutes and call the same code again: the value is not expired (remember, `5` min `Duration`) so the value is immediately returned.

So we wait another `4` min (total `6` min) and call the same code: the value is not in the cache (expired after `5` min) so the factory is called again, but this time **the database is down**: whoops.

Normally an exception would be thrown, and we would have to handle that with an error page or similar but, since we enabled the fail-safe mechanism, the expired value will be put back into the cache with a new cache duration of `1` min (`FailSafeThrottleDuration`).

From now on everything will repeat in the same way, without throwing exceptions, until one of two things occurs:
1) the factory completes successfully again: in this case the cache will be updated with the new value + a duration of `5` min (`Duration`)
2) a total `2` hours is passed (`FailSafeMaxDuration`): the value is actually deleted from the cache, like, **for real**

> [!TIP]
> Setting a `FailSafeMaxDuration` is useful to avoid using a stale value for too long: you can set this for as long as you want, even a month or a year.

## 💥 Even Without Exceptions

The natural way to trigger fail-safe is of course by an exception being thrown during a factory execution.

This makes sense, since the whole point of fail-safe is to protect us when an error occurs while executing a factory execution, and an error usually means exceptions.

There may be other ways to signal an error though, for example when working with the Result Pattern or similar approaches, in which throwing an exception is not strictly necessary.

By simply calling the `Fail(...)` method on the factory execution context and return, just like we can do with the `Modified(...)` or `NotModified(...)` methods for [Conditional Refresh](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md), it is possible to trigger the fail-safe flow without having to throw an exception, saving on performance and making our code a little more functional in the meantime.

Here's an example:

```csharp
var productResult = await cache.GetOrSetAsync<Result<Product>>(
	$"product:{id}",
	async (ctx, ct) =>
	{
		var productResult = GetProductFromDb(id);

		if (productResult.IsSuccess == false)
		{
			return ctx.Fail(productResult.Error);
		}

		return productResult;
	},
	opt => opt.SetDuration(duration).SetFailSafe(true)
);
```

## 🥇 There's Always A First Time

What if we would like to use a fallback value with fail-safe, but it's the first time calling the factory?

Well, in that case there's nothing in the cache, right? So what can FusionCache return?

Easy: when calling `GetOrSet` we can also specify a `failSafeDefaultValue` which, if specified by us, would act as a final fallback to avoid propagating the factory error to our users. This may not always make sense, because sometimes there is not a reasonable default value to use, but if there is, we can use it.

## ❤️ A Beautiful Movie
The name is also an homage to a somewhat forgotten [beautiful movie](https://en.wikipedia.org/wiki/Fail_Safe_(1964_film)), directed by the great Sidney Lumet and with a stellar cast. It's from a different era, so expect black and white and a different acting style, but give it a chance: it's remarkable.
