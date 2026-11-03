using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public partial class InternalKeyPrefixTests
{
	[Theory]
	[MemberData(nameof(CulturesAndKeys))]
	public async Task CanRemoveByTagWithSpecialKeysAsync(string culture, string key)
	{
		using var _ = new CultureScope(culture);
		using var cache = CreateCache();

		await cache.SetAsync(key, 1, tags: ["x"], token: TestContext.Current.CancellationToken);
		await cache.SetAsync("other", 2, tags: ["y"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1, await cache.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, await cache.GetOrSetAsync<int>(key, async _ => 10, tags: ["x"], token: TestContext.Current.CancellationToken));

		await cache.RemoveByTagAsync("x", token: TestContext.Current.CancellationToken);

		Assert.False((await cache.TryGetAsync<int>(key, token: TestContext.Current.CancellationToken)).HasValue);
		Assert.Equal(0, await cache.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(11, await cache.GetOrSetAsync<int>(key, async _ => 11, tags: ["x"], token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache.GetOrDefaultAsync<int>("other", token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndKeysAndClearKinds))]
	public async Task CanClearWithSpecialKeysAsync(string culture, string key, bool allowFailSafe)
	{
		using var _ = new CultureScope(culture);
		using var cache = CreateCache(useExternalMemoryCache: true);

		await cache.SetAsync(key, 1, token: TestContext.Current.CancellationToken);
		Assert.Equal(1, await cache.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));

		await cache.ClearAsync(allowFailSafe, token: TestContext.Current.CancellationToken);

		Assert.False((await cache.TryGetAsync<int>(key, token: TestContext.Current.CancellationToken)).HasValue);
		Assert.Equal(2, await cache.GetOrSetAsync<int>(key, async _ => 2, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndKeys))]
	public async Task BackplaneInvalidatesSpecialKeysAsync(string culture, string key)
	{
		using var _ = new CultureScope(culture);

		var cacheName = Guid.NewGuid().ToString("N");
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		using var cache1 = CreateCacheWithBackplane(backplaneConnectionId, "C1", cacheName);
		using var cache2 = CreateCacheWithBackplane(backplaneConnectionId, "C2", cacheName);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache2.SetAsync(key, 1, options => options.SetSkipBackplaneNotifications(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));

		// A SET ON NODE 1 MUST INVALIDATE THE (NOW STALE) ENTRY ON NODE 2
		await cache1.SetAsync(key, 2, token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		Assert.Equal(0, await cache2.GetOrDefaultAsync<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndTags))]
	public async Task BackplanePropagatesRemoveByTagWithSpecialTagsAsync(string culture, string tag)
	{
		using var _ = new CultureScope(culture);

		var cacheName = Guid.NewGuid().ToString("N");
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		using var cache1 = CreateCacheWithBackplane(backplaneConnectionId, "C1", cacheName);
		using var cache2 = CreateCacheWithBackplane(backplaneConnectionId, "C2", cacheName);

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

		await cache2.SetAsync("foo", 1, options => options.SetSkipBackplaneNotifications(true), tags: [tag], token: TestContext.Current.CancellationToken);
		await cache2.SetAsync("bar", 2, options => options.SetSkipBackplaneNotifications(true), tags: ["other"], token: TestContext.Current.CancellationToken);
		Assert.Equal(1, await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken));

		await cache1.RemoveByTagAsync(tag, token: TestContext.Current.CancellationToken);
		await Task.Delay(MultiNodeOperationsDelay, TestContext.Current.CancellationToken);

		Assert.Equal(0, await cache2.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken));
		Assert.Equal(2, await cache2.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken));
	}
}
