using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;

namespace FusionCacheTests;

public partial class GeneralTests
{
	[Fact]
	public void CanUseNullFusionCache()
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

		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);

		var maybeFoo1 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);

		cache.Remove("foo", token: TestContext.Current.CancellationToken);

		var maybeBar1 = cache.TryGet<int>("bar", token: TestContext.Current.CancellationToken);

		cache.Expire("qux", token: TestContext.Current.CancellationToken);

		var qux1 = cache.GetOrSet("qux", _ => 1, token: TestContext.Current.CancellationToken);
		var qux2 = cache.GetOrSet("qux", _ => 2, token: TestContext.Current.CancellationToken);
		var qux3 = cache.GetOrSet("qux", _ => 3, token: TestContext.Current.CancellationToken);
		var qux4 = cache.GetOrDefault("qux", 4, token: TestContext.Current.CancellationToken);

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

		Assert.Throws<UnreachableException>(() =>
		{
			_ = cache.GetOrSet<int>("qux", _ => throw new UnreachableException("Sloths"), token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public void CanUseDefaultEntryOptionsProvider()
	{
		using var cache = new FusionCache(new FusionCacheOptions()
		{
			DefaultEntryOptions = new FusionCacheEntryOptions()
			{
				IsFailSafeEnabled = true,
				Duration = TimeSpan.FromMinutes(123)
			},
			DefaultEntryOptionsProvider = new MyEntryOptionsProvider()
		});

		bool isFactoryRun = false;
		bool isFailSafeEnabled = false;
		TimeSpan duration = TimeSpan.Zero;
		CacheItemPriority priority = CacheItemPriority.NeverRemove;

		// GET OR SET: KEY MATCHING FOO
		isFactoryRun = false;
		isFailSafeEnabled = false;
		duration = TimeSpan.Zero;
		priority = CacheItemPriority.NeverRemove;
		cache.GetOrSet<int>(
			"my-foo-01",
			(ctx, ct) =>
			{
				isFactoryRun = true;
				isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
				duration = ctx.Options.Duration;
				priority = ctx.Options.Priority;

				return 42;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.True(isFactoryRun);
		Assert.False(isFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(456), duration);
		Assert.Equal(CacheItemPriority.Low, priority);

		// GET OR SET: KEY MATCHING FOO, BUT OPTIONS DIRECTLY SPECIFIED
		isFactoryRun = false;
		isFailSafeEnabled = false;
		duration = TimeSpan.Zero;
		priority = CacheItemPriority.NeverRemove;
		cache.GetOrSet<int>(
			"my-foo-02",
			(ctx, ct) =>
			{
				isFactoryRun = true;
				isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
				duration = ctx.Options.Duration;
				priority = ctx.Options.Priority;

				return 42;
			},
			new FusionCacheEntryOptions()
			{
				IsFailSafeEnabled = false,
				Duration = TimeSpan.FromMinutes(21),
				Priority = CacheItemPriority.NeverRemove
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.True(isFactoryRun);
		Assert.False(isFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(21), duration);
		Assert.Equal(CacheItemPriority.NeverRemove, priority);

		// GET OR SET: KEY MATCHING FOO, BUT OPTIONS DIRECTLY SPECIFIED
		isFactoryRun = false;
		isFailSafeEnabled = false;
		duration = TimeSpan.Zero;
		priority = CacheItemPriority.NeverRemove;
		cache.GetOrSet<int>(
			"my-foo-03",
			(ctx, ct) =>
			{
				isFactoryRun = true;
				isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
				duration = ctx.Options.Duration;
				priority = ctx.Options.Priority;

				return 42;
			},
			options =>
			{
				// EMPTY
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.True(isFactoryRun);
		Assert.False(isFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(456), duration);
		Assert.Equal(CacheItemPriority.Low, priority);

		// GET OR SET: KEY MATCHING BAR
		isFactoryRun = false;
		isFailSafeEnabled = false;
		duration = TimeSpan.Zero;
		priority = CacheItemPriority.NeverRemove;
		cache.GetOrSet<int>(
			"my-bar-01",
			(ctx, ct) =>
			{
				isFactoryRun = true;
				isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
				duration = ctx.Options.Duration;
				priority = ctx.Options.Priority;

				return 42;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.True(isFactoryRun);
		Assert.False(isFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(789), duration);
		Assert.Equal(CacheItemPriority.High, priority);

		// GET OR SET: KEY MATCHING NOTHING -> DEFAULT OPTIONS
		isFactoryRun = false;
		isFailSafeEnabled = false;
		duration = TimeSpan.Zero;
		priority = CacheItemPriority.NeverRemove;
		cache.GetOrSet<int>(
			"whatever-01",
			(ctx, ct) =>
			{
				isFactoryRun = true;
				isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
				duration = ctx.Options.Duration;
				priority = ctx.Options.Priority;

				return 42;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.True(isFactoryRun);
		Assert.True(isFailSafeEnabled);
		Assert.Equal(TimeSpan.FromMinutes(123), duration);
		Assert.Equal(CacheItemPriority.Normal, priority);

		//// SET: KEY MATCHING FOO
		//await cache.SetAsync<int>(
		//	"my-foo-01",
		//	async (ctx, ct) =>
		//	{
		//		isFailSafeEnabled = ctx.Options.IsFailSafeEnabled;
		//		duration = ctx.Options.Duration;
		//		priority = ctx.Options.Priority;

		//		return 42;
		//	},
		//	token: TestContext.Current.CancellationToken
		//);

		//Assert.False(isFailSafeEnabled);
		//Assert.Equal(TimeSpan.FromMinutes(456), duration);
		//Assert.Equal(CacheItemPriority.Low, priority);
	}
}
