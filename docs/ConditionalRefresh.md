<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🔂 Conditional Refresh

| ⚡ TL;DR (quick version) |
| -------- |
| Inside a factory it's possible to use a stale value + the ETag or LastModified info to use HTTP Conditional Request or a similar approach: in case the data is not changed it's possible to tell FusionCache to re-use the stale value as new, or if it's changed the new value + ETag/LastModified info. |

Sometimes the payload to receive from the so called single source of truth (eg: the database, a remote service, etc) can be quite big, and in those situations it is a waste to get and process it (eg: deserializing) each and every time even when the data has not changed.

Ideally there should be a way to keep track of the "version" of the cached data and, when requesting for a refresh, be able to handle for data that is not changed, so to keep the "stale" data which, in such a case, would not actually be stale.

In the world of HTTP such approach is commonly known as [conditional requests](https://developer.mozilla.org/en-US/docs/Web/HTTP/Conditional_requests): wouldn't it be nice to have something similar, but for FusionCache?

Enter **Conditional Refresh**.

## How

While running a factory we have access to a context, specifically an instance of `FusionCacheFactoryExecutionContext<TValue>`.

Here we have various properties, like `Options` (to achieve [Adaptive Caching](AdaptiveCaching.md)) and more. Among them there are 3 props related to Conditional Refresh:
- `MaybeValue<TValue> StaleValue`: to access the previously cached value
- `string? ETag`: the ETag of the previously cached value
- `DateTimeOffset? LastModified`: the last modified of the previously cached value

All of them may have a value or not, depending on the fact that there's stale data available or if previously and ETag has been set or not, etc.

Additionally there are some extra props and methods to make it easier working in such scenarios:
- `bool HasStaleValue`: a prop that returns `true` if there is stale data available
- `bool HasETag`: a prop that returns true if an ETag has been previously set
- `bool HasLastModified`: a prop that returns true if the last modified date has been previously set
- `TValue NotModified()`: a method that can be called as an easy way to express the intent to return the previous value, as is not changed
- `TValue Modified(TValue value, string? etag = null, DateTimeOffset? lastModified = null)`: a method that can be called as an easy way to express the intent of returning a new value, possibly along new values for the ETag or LastModified props

With these features available it's very easy to avoid sending huge payloads when that is not actually needed, resulting in less bandwidth consumed and more performance.

## 👩‍💻 A Practical Example

Let's say we have some data returned from a remote HTTP endpoint, and we want to support conditional refresh.

Here's how to do it:

```csharp
var product = await cache.GetOrSetAsync<Product>(
	$"product:{id}",
	async (ctx, ct) =>
	{
		using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/product/{id}");

		if (ctx.HasETag && ctx.HasStaleValue)
		{
			// ETAG + STALE VALUE -> TRY WITH A CONDITIONAL GET
			req.Headers.Add("If-None-Match", ctx.ETag);
		}

		using var resp = await client.SendAsync(req, ct);

		resp.EnsureSuccessStatusCode();

		if (resp.StatusCode == HttpStatusCode.NotModified)
		{
			// NOT MODIFIED -> RETURN STALE VALUE
			return ctx.NotModified();
		}

		// NORMAL RESPONSE: SAVE ETAG + RETURN VALUE
		return ctx.Modified(
			await resp.Content.ReadFromJsonAsync<Product>(),
			resp.Headers.ETag?.ToString()
		);
	},
	opt => opt.SetDuration(duration).SetFailSafe(true)
);
```

Of course all of this can be combined with any other FusionCache feature, like [Adaptive Caching](AdaptiveCaching.md), [Timeouts](Timeouts.md), etc.
