using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public partial class InternalKeyPrefixTests
{
	[Theory]
	[MemberData(nameof(CulturesAndKeys))]
	public void CanRemoveByTagWithSpecialKeys(string culture, string key)
	{
		using var _ = new CultureScope(culture);
		using var cache = CreateCache();

		cache.Set(key, 1, tags: ["x"], token: TestContext.Current.CancellationToken);
		cache.Set("other", 2, tags: ["y"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, cache.GetOrSet<int>(key, _ => 10, tags: ["x"], token: TestContext.Current.CancellationToken));

		cache.RemoveByTag("x", token: TestContext.Current.CancellationToken);

		Assert.False(cache.TryGet<int>(key, token: TestContext.Current.CancellationToken).HasValue);
		Assert.Equal(0, cache.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(11, cache.GetOrSet<int>(key, _ => 11, tags: ["x"], token: TestContext.Current.CancellationToken));
		Assert.Equal(2, cache.GetOrDefault<int>("other", token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndKeysAndClearKinds))]
	public void CanClearWithSpecialKeys(string culture, string key, bool allowFailSafe)
	{
		using var _ = new CultureScope(culture);
		using var cache = CreateCache(useExternalMemoryCache: true);

		cache.Set(key, 1, token: TestContext.Current.CancellationToken);
		Assert.Equal(1, cache.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));

		cache.Clear(allowFailSafe, token: TestContext.Current.CancellationToken);

		Assert.False(cache.TryGet<int>(key, token: TestContext.Current.CancellationToken).HasValue);
		Assert.Equal(2, cache.GetOrSet<int>(key, _ => 2, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, cache.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndKeys))]
	public void BackplaneInvalidatesSpecialKeys(string culture, string key)
	{
		using var _ = new CultureScope(culture);

		var cacheName = Guid.NewGuid().ToString("N");
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		using var cache1 = CreateCacheWithBackplane(backplaneConnectionId, "C1", cacheName);
		using var cache2 = CreateCacheWithBackplane(backplaneConnectionId, "C2", cacheName);

		Thread.Sleep(InitialBackplaneDelay);

		cache2.Set(key, 1, options => options.SetSkipBackplaneNotifications(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(1, cache2.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));

		// A SET ON NODE 1 MUST INVALIDATE THE (NOW STALE) ENTRY ON NODE 2
		cache1.Set(key, 2, token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(0, cache2.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Theory]
	[MemberData(nameof(CulturesAndTags))]
	public void BackplanePropagatesRemoveByTagWithSpecialTags(string culture, string tag)
	{
		using var _ = new CultureScope(culture);

		var cacheName = Guid.NewGuid().ToString("N");
		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		using var cache1 = CreateCacheWithBackplane(backplaneConnectionId, "C1", cacheName);
		using var cache2 = CreateCacheWithBackplane(backplaneConnectionId, "C2", cacheName);

		Thread.Sleep(InitialBackplaneDelay);

		cache2.Set("foo", 1, options => options.SetSkipBackplaneNotifications(true), tags: [tag], token: TestContext.Current.CancellationToken);
		cache2.Set("bar", 2, options => options.SetSkipBackplaneNotifications(true), tags: ["other"], token: TestContext.Current.CancellationToken);
		Assert.Equal(1, cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken));

		cache1.RemoveByTag(tag, token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(0, cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken));
		Assert.Equal(2, cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken));
	}
}
