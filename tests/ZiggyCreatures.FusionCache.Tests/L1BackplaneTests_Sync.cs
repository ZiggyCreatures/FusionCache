using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public partial class L1BackplaneTests<T>
{
	[Fact]
	public void WorksWithDifferentCaches()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache("C1", CreateBackplane(backplaneConnectionId), cacheInstanceId: "C1");
		using var cache2 = CreateFusionCache("C2", CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-01");
		using var cache2bis = CreateFusionCache("C2", CreateBackplane(backplaneConnectionId), cacheInstanceId: "C2-02");

		Thread.Sleep(InitialBackplaneDelay);

		cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);
		cache2bis.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(1, cache1.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(0, cache2.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, cache2bis.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));

		cache1.Set(key, 21, token: TestContext.Current.CancellationToken);
		cache2.Set(key, 42, token: TestContext.Current.CancellationToken);

		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(21, cache1.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken));
		Assert.Equal(42, cache2.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken));
		Thread.Sleep(MultiNodeOperationsDelay);
		Assert.Equal(78, cache2bis.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken));
		Thread.Sleep(MultiNodeOperationsDelay);
		Assert.Equal(88, cache2.GetOrSet(key, _ => 88, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken));
	}

	[Fact]
	public void CanSkipNotifications()
	{
		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		var key = Guid.NewGuid().ToString("N");
		using var cache1 = CreateFusionCache(null, CreateBackplane(backplaneConnectionId));
		using var cache2 = CreateFusionCache(null, CreateBackplane(backplaneConnectionId));
		using var cache3 = CreateFusionCache(null, CreateBackplane(backplaneConnectionId));

		cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;
		cache3.DefaultEntryOptions.SkipBackplaneNotifications = true;

		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

		Thread.Sleep(InitialBackplaneDelay);

		cache1.Set(key, 1, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		cache2.Set(key, 2, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		cache3.Set(key, 3, TimeSpan.FromMinutes(10), token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		Assert.Equal(1, cache1.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(2, cache2.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
		Assert.Equal(3, cache3.GetOrDefault<int>(key, token: TestContext.Current.CancellationToken));
	}

	[Fact]
	public void ReThrowsBackplaneExceptions()
	{
		var backplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions()));
		var chaosBackplane = new ChaosBackplane(backplane);

		chaosBackplane.SetAlwaysThrow();
		using var fusionCache = new FusionCache(CreateFusionCacheOptions());
		fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		fusionCache.DefaultEntryOptions.ReThrowBackplaneExceptions = true;

		fusionCache.SetupBackplane(chaosBackplane);

		Thread.Sleep(InitialBackplaneDelay);

		Assert.Throws<FusionCacheBackplaneException>(() =>
		{
			fusionCache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public void CanClear()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");
		var key = Guid.NewGuid().ToString("N");

		using var cache1 = CreateFusionCache(null, CreateBackplane(backplaneConnectionId));
		cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;

		using var cache2 = CreateFusionCache(null, CreateBackplane(backplaneConnectionId));
		cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;

		Thread.Sleep(InitialBackplaneDelay);

		logger.LogInformation("STEP 1");

		cache1.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		cache1.Set<int>("bar", 1, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		cache1.Set<int>("baz", 1, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 2");

		var foo1_1 = cache1.GetOrSet<int>("foo", _ => 2, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var bar1_1 = cache1.GetOrSet<int>("bar", _ => 2, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var baz1_1 = cache1.GetOrSet<int>("baz", _ => 2, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1_1);
		Assert.Equal(1, bar1_1);
		Assert.Equal(1, baz1_1);

		logger.LogInformation("STEP 3");

		var foo2_1 = cache2.GetOrSet<int>("foo", _ => 3, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var bar2_1 = cache2.GetOrSet<int>("bar", _ => 3, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var baz2_1 = cache2.GetOrSet<int>("baz", _ => 3, options => options.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);

		Assert.Equal(3, foo2_1);
		Assert.Equal(3, bar2_1);
		Assert.Equal(3, baz2_1);

		logger.LogInformation("CLEAR");

		cache1.Clear(token: TestContext.Current.CancellationToken);

		logger.LogInformation("STEP 4");

		var foo1_2 = cache1.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1_2 = cache1.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz1_2 = cache1.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo1_2);
		Assert.Equal(0, bar1_2);
		Assert.Equal(0, baz1_2);

		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 5");

		var foo2_2 = cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_2 = cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz2_2 = cache2.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_2);
		Assert.Equal(0, bar2_2);
		Assert.Equal(0, baz2_2);

		logger.LogInformation("STEP 6");

		var foo2_3 = cache2.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2_3 = cache2.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz2_3 = cache2.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2_3);
		Assert.Equal(0, bar2_3);
		Assert.Equal(0, baz2_3);
	}

	[Fact]
	public void CanRemoveByTag()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var cacheName = FusionCacheInternalUtils.GenerateOperationId();

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		using var cache1 = CreateFusionCache(
			cacheName,
			CreateBackplane(backplaneConnectionId),
			opt =>
			{
				opt.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
				opt.DefaultEntryOptions.SkipBackplaneNotifications = true;
			},
			cacheInstanceId: "C1",
			logger: logger
		);

		using var cache2 = CreateFusionCache(
			cacheName,
			CreateBackplane(backplaneConnectionId),
			opt =>
			{
				opt.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
				opt.DefaultEntryOptions.SkipBackplaneNotifications = true;
			},
			cacheInstanceId: "C2",
			logger: logger
		);

		Thread.Sleep(InitialBackplaneDelay);

		logger.LogInformation("STEP 1");

		var foo = 1;
		var bar = 2;
		var baz = 3;

		cache1.Set<int>("foo", foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		cache1.Set<int>("bar", bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		cache1.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		var cache1_foo_1 = cache1.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var cache1_bar_1 = cache1.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var cache1_baz_1 = cache1.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache1_foo_1);
		Assert.Equal(2, cache1_bar_1);
		Assert.Equal(3, cache1_baz_1);

		var cache2_foo_1 = cache2.GetOrSet<int>("foo", _ => foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var cache2_bar_1 = cache2.GetOrSet<int>("bar", _ => bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var cache2_baz_1 = cache2.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1, cache2_foo_1);
		Assert.Equal(2, cache2_bar_1);
		Assert.Equal(3, cache2_baz_1);

		logger.LogInformation("STEP 2");

		// REMOVE BY TAG ("x") ON CACHE 1
		cache1.RemoveByTag("x", token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 3");

		foo = 11;
		bar = 22;
		baz = 33;

		var cache1_foo_3 = cache1.GetOrSet<int>("foo", _ => foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var cache1_bar_3 = cache1.GetOrSet<int>("bar", _ => bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var cache1_baz_3 = cache1.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(foo, cache1_foo_3);
		Assert.Equal(2, cache1_bar_3);
		Assert.Equal(baz, cache1_baz_3);

		var cache2_foo_3 = cache2.GetOrSet<int>("foo", _ => foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var cache2_bar_3 = cache2.GetOrSet<int>("bar", _ => bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var cache2_baz_3 = cache2.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(foo, cache2_foo_3);
		Assert.Equal(2, cache2_bar_3);
		Assert.Equal(baz, cache2_baz_3);

		logger.LogInformation("STEP 4");

		// REMOVE BY TAG ("y") ON CACHE 2
		cache2.RemoveByTag("y", token: TestContext.Current.CancellationToken);
		Thread.Sleep(MultiNodeOperationsDelay);

		logger.LogInformation("STEP 5");

		foo = 111;
		bar = 222;
		baz = 333;

		var cache1_foo_4 = cache1.GetOrSet<int>("foo", _ => foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var cache1_bar_4 = cache1.GetOrSet<int>("bar", _ => bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var cache1_baz_4 = cache1.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(foo, cache1_foo_4);
		Assert.Equal(bar, cache1_bar_4);
		Assert.Equal(33, cache1_baz_4);

		var cache2_foo_4 = cache2.GetOrSet<int>("foo", _ => foo, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var cache2_bar_4 = cache2.GetOrSet<int>("bar", _ => bar, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var cache2_baz_4 = cache2.GetOrSet<int>("baz", _ => baz, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(foo, cache2_foo_4);
		Assert.Equal(bar, cache2_bar_4);
		Assert.Equal(33, cache2_baz_4);
	}
}
