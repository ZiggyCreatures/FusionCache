using System.Diagnostics;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;

namespace FusionCacheTests;

public partial class GeneralTests
{
	[Fact]
	public async Task CanUseNullFusionCacheAsync()
	{
		using var cache = new NullFusionCache(new FusionCacheOptions()
		{
			CacheName = "SlothsAreCool42",
			DefaultEntryOptions = new FusionCacheEntryOptions()
			{
				IsFailSafeEnabled = true,
				Duration = TimeSpan.FromMinutes(123)
			}
		});

		await cache.SetAsync<int>("foo", 42);

		var maybeFoo1 = await cache.TryGetAsync<int>("foo");

		await cache.RemoveAsync("foo");

		var maybeBar1 = await cache.TryGetAsync<int>("bar");

		await cache.ExpireAsync("qux");

		var qux1 = await cache.GetOrSetAsync("qux", async _ => 1);
		var qux2 = await cache.GetOrSetAsync("qux", async _ => 2);
		var qux3 = await cache.GetOrSetAsync("qux", async _ => 3);
		var qux4 = await cache.GetOrDefaultAsync("qux", 4);

		Assert.Equal("SlothsAreCool42", cache.CacheName);
		Assert.False(string.IsNullOrWhiteSpace(cache.InstanceId));

		Assert.False(cache.HasDistributedCache);
		Assert.False(cache.HasBackplane);

		Assert.True(cache.DefaultEntryOptions.IsFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(123), cache.DefaultEntryOptions.Duration);

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeBar1.HasValue);

		Assert.Equal(1, qux1);
		Assert.Equal(2, qux2);
		Assert.Equal(3, qux3);
		Assert.Equal(4, qux4);

		await Assert.ThrowsAsync<UnreachableException>(async () =>
		{
			_ = await cache.GetOrSetAsync<int>("qux", async _ => throw new UnreachableException("Sloths"));
		});
	}
}
