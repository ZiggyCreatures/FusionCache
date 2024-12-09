using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;

namespace FusionCacheTests;

public class MemoryLevelTests
	: AbstractTests
{
	public MemoryLevelTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	[Fact]
	public async Task CanRemoveAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo");
		await cache.RemoveAsync("foo");
		var foo2 = await cache.GetOrDefaultAsync<int>("foo");
		Assert.Equal(42, foo1);
		Assert.Equal(0, foo2);
	}

	[Fact]
	public void CanRemove()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42);
		var foo1 = cache.GetOrDefault<int>("foo");
		cache.Remove("foo");
		var foo2 = cache.GetOrDefault<int>("foo");
		Assert.Equal(42, foo1);
		Assert.Equal(0, foo2);
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryFailsAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var cache = new FusionCache(options);
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		await Task.Delay(500);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void ReturnsStaleDataWhenFactoryFails()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var cache = new FusionCache(options);
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Thread.Sleep(500);
		var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryFailsWithoutExceptionAsync()
	{
		var errorMessage = "Sloths are cool";
		var throttleDuration = TimeSpan.FromSeconds(1);

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;
		options.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromMinutes(10);

		using var cache = new FusionCache(options);

		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42);

		await Task.Delay(500);

		var newValue = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => ctx.Fail(errorMessage));

		Assert.Equal(initialValue, newValue);

		await Task.Delay(throttleDuration.PlusALittleBit());

		Exception? exc = null;
		try
		{
			_ = await cache.GetOrSetAsync<int>(
				"foo",
				async (ctx, _) => ctx.Fail(errorMessage),
				opt => opt.SetFailSafe(false)
			);
		}
		catch (Exception exc1)
		{
			exc = exc1;
		}

		Assert.NotNull(exc);
		Assert.IsType<FusionCacheFactoryException>(exc);
		Assert.Equal(errorMessage, exc.Message);
	}

	[Fact]
	public void ReturnsStaleDataWhenFactoryFailsWithoutException()
	{
		var errorMessage = "Sloths are cool";
		var throttleDuration = TimeSpan.FromSeconds(1);

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;
		options.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromMinutes(10);

		using var cache = new FusionCache(options);

		var initialValue = cache.GetOrSet<int>("foo", _ => 42);

		Thread.Sleep(500);

		var newValue = cache.GetOrSet<int>("foo", (ctx, _) => ctx.Fail(errorMessage));

		Assert.Equal(initialValue, newValue);

		Thread.Sleep(throttleDuration.PlusALittleBit());

		Exception? exc = null;
		try
		{
			_ = cache.GetOrSet<int>(
				"foo",
				(ctx, _) => ctx.Fail(errorMessage),
				opt => opt.SetFailSafe(false)
			);
		}
		catch (Exception exc1)
		{
			exc = exc1;
		}

		Assert.NotNull(exc);
		Assert.IsType<FusionCacheFactoryException>(exc);
		Assert.Equal(errorMessage, exc.Message);
	}

	[Fact]
	public async Task ThrowsWhenFactoryThrowsWithoutFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		await Task.Delay(1_100);
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
		});
	}

	[Fact]
	public void ThrowsWhenFactoryThrowsWithoutFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Thread.Sleep(1_100);
		Assert.ThrowsAny<Exception>(() =>
		{
			var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
		});
	}

	[Fact]
	public async Task ThrowsOnFactoryHardTimeoutWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
		{
			var value = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
		});
	}

	[Fact]
	public void ThrowsOnFactoryHardTimeoutWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		Assert.Throws<SyntheticTimeoutException>(() =>
		{
			var value = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100));
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		await Task.Delay(1_100);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Thread.Sleep(1_100);
		var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotSoftTimeoutWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.Equal(21, initialValue);
	}

	[Fact]
	public void DoesNotSoftTimeoutWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.Equal(21, initialValue);
	}

	[Fact]
	public async Task DoesHardTimeoutEvenWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
		});
	}

	[Fact]
	public void DoesHardTimeoutEvenWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		Assert.ThrowsAny<Exception>(() =>
		{
			var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		await Task.Delay(1_100);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
		Assert.Equal(42, newValue);
	}

	[Fact]
	public void ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Thread.Sleep(1_100);
		var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500));
		Assert.Equal(42, newValue);
	}

	[Fact]
	public async Task SetOverwritesAnExistingValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		var newValue = 21;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		await cache.SetAsync<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		var actualValue = await cache.GetOrDefaultAsync<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		Assert.Equal(newValue, actualValue);
	}

	[Fact]
	public void SetOverwritesAnExistingValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		var newValue = 21;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		var actualValue = cache.GetOrDefault<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		Assert.Equal(newValue, actualValue);
	}

	[Fact]
	public async Task GetOrSetDoesNotOverwriteANonExpiredValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void GetOrSetDoesNotOverwriteANonExpiredValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)));
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotReturnStaleDataIfFactorySucceedsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		await Task.Delay(1_500);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public void DoesNotReturnStaleDataIfFactorySucceeds()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Thread.Sleep(1_500);
		var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesReturnStaleDataWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		await Task.Delay(1_500);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void GetOrDefaultDoesReturnStaleDataWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Thread.Sleep(1_500);
		var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesNotReturnStaleDataWithoutFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true });
		await Task.Delay(1_500);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false });
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public void GetOrDefaultDoesNotReturnStaleWithoutFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Thread.Sleep(1_500);
		var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false));
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public async Task FactoryTimedOutButSuccessfulDoesUpdateCachedValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
		var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
		await Task.Delay(1_500);
		var middleValue = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
		var interstitialValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		await Task.Delay(3_000);
		var finalValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

		Assert.Equal(42, initialValue);
		Assert.Equal(42, middleValue);
		Assert.Equal(42, interstitialValue);
		Assert.Equal(21, finalValue);
	}

	[Fact]
	public void FactoryTimedOutButSuccessfulDoesUpdateCachedValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
		var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)));
		Thread.Sleep(1_500);
		var middleValue = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500));
		var interstitialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Thread.Sleep(3_000);
		var finalValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

		Assert.Equal(42, initialValue);
		Assert.Equal(42, middleValue);
		Assert.Equal(42, interstitialValue);
		Assert.Equal(21, finalValue);
	}

	[Fact]
	public async Task TryGetReturnsCorrectlyAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		var res1 = await cache.TryGetAsync<int>("foo");
		await cache.SetAsync<int>("foo", 42);
		var res2 = await cache.TryGetAsync<int>("foo");
		Assert.False(res1.HasValue);
		Assert.Throws<InvalidOperationException>(() =>
		{
			var foo = res1.Value;
		});
		Assert.True(res2.HasValue);
		Assert.Equal(42, res2.Value);
	}

	[Fact]
	public void TryGetReturnsCorrectly()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		var res1 = cache.TryGet<int>("foo");
		cache.Set<int>("foo", 42);
		var res2 = cache.TryGet<int>("foo");
		Assert.False(res1.HasValue);
		Assert.Throws<InvalidOperationException>(() =>
		{
			var foo = res1.Value;
		});
		Assert.True(res2.HasValue);
		Assert.Equal(42, res2.Value);
	}

	[Fact]
	public async Task CanCancelAnOperationAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions());
		int res = -1;
		var sw = Stopwatch.StartNew();
		var outerCancelDelayMs = 200;
		var factoryDelayMs = 5_000;
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			var cts = new CancellationTokenSource(outerCancelDelayMs);
			res = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
		});
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		TestOutput.WriteLine($"Outer Cancel: {outerCancelDelayMs} ms");
		TestOutput.WriteLine($"Factory Delay: {factoryDelayMs} ms");
		TestOutput.WriteLine($"Elapsed (with extra pad): {elapsedMs} ms");

		Assert.Equal(-1, res);
		Assert.True(elapsedMs >= outerCancelDelayMs, "Elapsed is less than outer cancel");
		Assert.True(elapsedMs < factoryDelayMs, "Elapsed is not less than factory delay");
	}

	[Fact]
	public void CanCancelAnOperation()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions());
		int res = -1;
		var sw = Stopwatch.StartNew();
		var outerCancelDelayMs = 200;
		var factoryDelayMs = 5_000;
		Assert.ThrowsAny<OperationCanceledException>(() =>
		{
			var cts = new CancellationTokenSource(outerCancelDelayMs);
			res = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, options => options.SetDurationSec(60), cts.Token);
		});
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		TestOutput.WriteLine($"Outer Cancel: {outerCancelDelayMs} ms");
		TestOutput.WriteLine($"Factory Delay: {factoryDelayMs} ms");
		TestOutput.WriteLine($"Elapsed (with extra pad): {elapsedMs} ms");

		Assert.Equal(-1, res);
		Assert.True(elapsedMs >= outerCancelDelayMs, "Elapsed is less than outer cancel");
		Assert.True(elapsedMs < factoryDelayMs, "Elapsed is not less than factory delay");
	}

	[Fact]
	public async Task HandlesFlexibleSimpleTypeConversionsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)42;
		await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
		var newValue = await cache.GetOrDefaultAsync<int>("foo");
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void HandlesFlexibleSimpleTypeConversions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)42;
		cache.Set("foo", initialValue, TimeSpan.FromHours(24));
		var newValue = cache.GetOrDefault<int>("foo");
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task HandlesFlexibleComplexTypeConversionsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)ComplexType.CreateSample();
		await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24));
		var newValue = await cache.GetOrDefaultAsync<ComplexType>("foo");
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void HandlesFlexibleComplexTypeConversions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)ComplexType.CreateSample();
		cache.Set("foo", initialValue, TimeSpan.FromHours(24));
		var newValue = cache.GetOrDefault<ComplexType>("foo");
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesNotSetAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = await cache.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)));
		var bar = await cache.GetOrDefaultAsync<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)));
		var baz = await cache.TryGetAsync<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)));
		Assert.Equal(42, foo);
		Assert.Equal(21, bar);
		Assert.False(baz.HasValue);
	}

	[Fact]
	public void GetOrDefaultDoesNotSet()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = cache.GetOrDefault<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)));
		var bar = cache.GetOrDefault<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)));
		var baz = cache.TryGet<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)));
		Assert.Equal(42, foo);
		Assert.Equal(21, bar);
		Assert.False(baz.HasValue);
	}

	[Fact]
	public async Task GetOrSetWithDefaultValueWorksAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = 42;
		await cache.GetOrSetAsync<int>("foo", foo, TimeSpan.FromHours(24));
		var bar = await cache.GetOrDefaultAsync<int>("foo", 21);
		Assert.Equal(foo, bar);
	}

	[Fact]
	public void GetOrSetWithDefaultValueWorks()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = 42;
		cache.GetOrSet<int>("foo", foo, TimeSpan.FromHours(24));
		var bar = cache.GetOrDefault<int>("foo", 21);
		Assert.Equal(foo, bar);
	}

	[Fact]
	public async Task ThrottleDurationWorksCorrectlyAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var duration = TimeSpan.FromSeconds(1);
		var throttleDuration = TimeSpan.FromSeconds(3);

		// SET THE VALUE (WITH FAIL-SAFE ENABLED)
		await cache.SetAsync("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration));
		// LET IT EXPIRE
		await Task.Delay(duration.PlusALittleBit());
		// CHECK EXPIRED (WITHOUT FAIL-SAFE)
		var nope = await cache.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));
		// DO NOT ACTIVATE FAIL-SAFE AND THROTTLE DURATION
		var default1 = await cache.GetOrDefaultAsync("foo", 1);
		// ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
		var throttled1 = await cache.GetOrDefaultAsync("foo", 1, opt => opt.SetFailSafe(true, throttleDuration: throttleDuration));
		// WAIT A LITTLE BIT (LESS THAN THE DURATION)
		await Task.Delay(100);
		// GET THE THROTTLED (NON EXPIRED) VALUE
		var throttled2 = await cache.GetOrDefaultAsync("foo", 2, opt => opt.SetFailSafe(true));
		// LET THE THROTTLE DURATION PASS
		await Task.Delay(throttleDuration);
		// FALLBACK TO THE DEFAULT VALUE
		var default3 = await cache.GetOrDefaultAsync("foo", 3, opt => opt.SetFailSafe(false));

		Assert.False(nope.HasValue);
		Assert.Equal(1, default1);
		Assert.Equal(42, throttled1);
		Assert.Equal(42, throttled2);
		Assert.Equal(3, default3);
	}

	[Fact]
	public void ThrottleDurationWorksCorrectly()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var duration = TimeSpan.FromSeconds(1);
		var throttleDuration = TimeSpan.FromSeconds(3);

		// SET THE VALUE (WITH FAIL-SAFE ENABLED)
		cache.Set("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration));
		// LET IT EXPIRE
		Thread.Sleep(duration.PlusALittleBit());
		// CHECK EXPIRED (WITHOUT FAIL-SAFE)
		var nope = cache.TryGet<int>("foo", opt => opt.SetFailSafe(false));
		// DO NOT ACTIVATE FAIL-SAFE AND THROTTLE DURATION
		var default1 = cache.GetOrDefault("foo", 1);
		// ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
		var throttled1 = cache.GetOrDefault("foo", 1, opt => opt.SetFailSafe(true, throttleDuration: throttleDuration));
		// WAIT A LITTLE BIT (LESS THAN THE DURATION)
		Thread.Sleep(100);
		// GET THE THROTTLED (NON EXPIRED) VALUE
		var throttled2 = cache.GetOrDefault("foo", 2, opt => opt.SetFailSafe(true));
		// LET THE THROTTLE DURATION PASS
		Thread.Sleep(throttleDuration);
		// FALLBACK TO THE DEFAULT VALUE
		var default3 = cache.GetOrDefault("foo", 3, opt => opt.SetFailSafe(false));

		Assert.False(nope.HasValue);
		Assert.Equal(1, default1);
		Assert.Equal(42, throttled1);
		Assert.Equal(42, throttled2);
		Assert.Equal(3, default3);
	}

	[Fact]
	public async Task AdaptiveCachingAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;
		FusionCacheEntryOptions? innerOpt = null;

		var default3 = await cache.GetOrSetAsync<int>(
			"foo",
			async (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(1);

				innerOpt = ctx.Options;

				return 3;
			},
			opt => opt.SetFailSafe(false)
		);

		await Task.Delay(TimeSpan.FromSeconds(2));

		var maybeValue = await cache.TryGetAsync<int>("foo");

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(innerOpt!.Duration, TimeSpan.FromSeconds(1));
		Assert.False(maybeValue.HasValue);
	}

	[Fact]
	public void AdaptiveCaching()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;
		FusionCacheEntryOptions? innerOpt = null;

		var default3 = cache.GetOrSet<int>(
			"foo",
			(ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(1);

				innerOpt = ctx.Options;

				return 3;
			},
			opt => opt.SetFailSafe(false)
		);

		Thread.Sleep(TimeSpan.FromSeconds(2));

		var maybeValue = cache.TryGet<int>("foo");

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(innerOpt!.Duration, TimeSpan.FromSeconds(1));
		Assert.False(maybeValue.HasValue);
	}

	[Fact]
	public async Task AdaptiveCachingWithBackgroundFactoryCompletionAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;

		// SET WITH 1s DURATION + FAIL-SAFE
		await cache.SetAsync("foo", 21, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));

		// LET IT BECOME STALE
		await Task.Delay(TimeSpan.FromSeconds(2));

		// CALL GetOrSET WITH A 1s SOFT TIMEOUT AND A FACTORY RUNNING FOR AT LEAST 3s
		var value21 = await cache.GetOrSetAsync<int>(
			"foo",
			async (ctx, _) =>
			{
				// WAIT 3s
				await Task.Delay(TimeSpan.FromSeconds(3));

				// CHANGE THE OPTIONS (SET THE DURATION TO 5s AND DISABLE FAIL-SAFE
				ctx.Options.SetDuration(TimeSpan.FromSeconds(5)).SetFailSafe(false);

				return 42;
			},
			opt => opt.SetFactoryTimeouts(TimeSpan.FromSeconds(1)).SetFailSafe(true)
		);

		// WAIT FOR 3s (+ EXTRA 1s) SO THE FACTORY COMPLETES IN THE BACKGROUND
		await Task.Delay(TimeSpan.FromSeconds(3 + 1));

		// GET THE VALUE THAT HAS BEEN SET BY THE BACKGROUND COMPLETION OF THE FACTORY
		var value42 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(false));

		// LET THE CACHE ENTRY EXPIRES
		await Task.Delay(TimeSpan.FromSeconds(5));

		// SEE THAT FAIL-SAFE CANNOT BE ACTIVATED (BECAUSE IT WAS DISABLED IN THE FACTORY)
		var noValue = await cache.TryGetAsync<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(21, value21);
		Assert.Equal(42, value42);
		Assert.False(noValue.HasValue);
	}

	[Fact]
	public void AdaptiveCachingWithBackgroundFactoryCompletion()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;

		// SET WITH 1s DURATION + FAIL-SAFE
		cache.Set("foo", 21, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));

		// LET IT BECOME STALE
		Thread.Sleep(TimeSpan.FromSeconds(2));

		// CALL GetOrSET WITH A 1s SOFT TIMEOUT AND A FACTORY RUNNING FOR AT LEAST 3s
		var value21 = cache.GetOrSet<int>(
			"foo",
			(ctx, _) =>
			{
				// WAIT 3s
				Thread.Sleep(TimeSpan.FromSeconds(3));

				// CHANGE THE OPTIONS (SET THE DURATION TO 5s AND DISABLE FAIL-SAFE
				ctx.Options.SetDuration(TimeSpan.FromSeconds(5)).SetFailSafe(false);

				return 42;
			},
			opt => opt.SetFactoryTimeouts(TimeSpan.FromSeconds(1)).SetFailSafe(true)
		);

		// WAIT FOR 3s (+ EXTRA 1s) SO THE FACTORY COMPLETES IN THE BACKGROUND
		Thread.Sleep(TimeSpan.FromSeconds(3 + 1));

		// GET THE VALUE THAT HAS BEEN SET BY THE BACKGROUND COMPLETION OF THE FACTORY
		var value42 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(false));

		// LET THE CACHE ENTRY EXPIRES
		Thread.Sleep(TimeSpan.FromSeconds(5));

		// SEE THAT FAIL-SAFE CANNOT BE ACTIVATED (BECAUSE IT WAS DISABLED IN THE FACTORY)
		var noValue = cache.TryGet<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(21, value21);
		Assert.Equal(42, value42);
		Assert.False(noValue.HasValue);
	}

	[Fact]
	public async Task AdaptiveCachingDoesNotChangeOptionsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var options = new FusionCacheEntryOptions(TimeSpan.FromSeconds(10));

		_ = await cache.GetOrSetAsync<int>(
			"foo",
			async (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(20);
				return 42;
			},
			options
		);

		Assert.Equal(options.Duration, TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AdaptiveCachingDoesNotChangeOptions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var options = new FusionCacheEntryOptions(TimeSpan.FromSeconds(10));

		_ = cache.GetOrSet<int>(
			"foo",
			(ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(20);
				return 42;
			},
			options
		);

		Assert.Equal(options.Duration, TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task AdaptiveCachingCanWorkWithSkipMemoryCacheAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		cache.DefaultEntryOptions.FailSafeThrottleDuration = TimeSpan.FromSeconds(3);

		var foo1 = await cache.GetOrSetAsync<int>("foo", async _ => 1);

		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit());

		var foo2 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) =>
		{
			ctx.Options.SkipMemoryCacheRead = true;
			ctx.Options.SkipMemoryCacheWrite = true;

			return 2;
		});

		var foo3 = await cache.TryGetAsync<int>("foo");

		await Task.Delay(cache.DefaultEntryOptions.FailSafeThrottleDuration.PlusALittleBit());

		var foo4 = await cache.GetOrSetAsync<int>("foo", async _ => 4);

		Assert.Equal(1, foo1);
		Assert.Equal(2, foo2);
		Assert.True(foo3.HasValue);
		Assert.Equal(1, foo3.Value);
		Assert.Equal(4, foo4);
	}

	[Fact]
	public void AdaptiveCachingCanWorkWithSkipMemoryCache()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		cache.DefaultEntryOptions.FailSafeThrottleDuration = TimeSpan.FromSeconds(3);

		var foo1 = cache.GetOrSet<int>("foo", _ => 1);

		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());

		var foo2 = cache.GetOrSet<int>("foo", (ctx, _) =>
		{
			ctx.Options.SkipMemoryCacheRead = true;
			ctx.Options.SkipMemoryCacheWrite = true;

			return 2;
		});

		var foo3 = cache.TryGet<int>("foo");

		Thread.Sleep(cache.DefaultEntryOptions.FailSafeThrottleDuration.PlusALittleBit());

		var foo4 = cache.GetOrSet<int>("foo", _ => 4);

		Assert.Equal(1, foo1);
		Assert.Equal(2, foo2);
		Assert.True(foo3.HasValue);
		Assert.Equal(1, foo3.Value);
		Assert.Equal(4, foo4);
	}


	[Fact]
	public async Task AdaptiveCachingCanWorkOnExceptionAsync()
	{
		var options = new FusionCacheOptions();

		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(5);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;

		var cache = new FusionCache(options);
		var key = "foo";

		cache.GetOrSet(key, "bar");

		// LOGICALLY EXPIRE THE KEY SO THE FAIL-SAFE LOGIC TRIGGERS
		cache.Expire(key);

		await Assert.ThrowsAsync<Exception>(async () =>
		{
			await cache.GetOrSetAsync<string>(key, async (ctx, ct) =>
			{
				try
				{
					throw new Exception("Factory failed");
				}
				finally
				{
					// DISABLE FAIL SAFE
					ctx.Options.SetFailSafe(false);
				}
			});
		});

		await cache.GetOrSetAsync<string>(key, async (ctx, ct) =>
		{
			throw new Exception("Factory failed");
		});
	}

	[Fact]
	public void AdaptiveCachingCanWorkOnException()
	{
		var options = new FusionCacheOptions();

		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(5);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;

		var cache = new FusionCache(options);
		var key = "foo";

		cache.GetOrSet(key, "bar");

		// LOGICALLY EXPIRE THE KEY SO THE FAIL-SAFE LOGIC TRIGGERS
		cache.Expire(key);

		Assert.Throws<Exception>(() =>
		{
			cache.GetOrSet<string>(key, (ctx, ct) =>
			{
				try
				{
					throw new Exception("Factory failed");
				}
				finally
				{
					// DISABLE FAIL SAFE
					ctx.Options.SetFailSafe(false);
				}
			});
		});

		cache.GetOrSet<string>(key, (ctx, ct) =>
		{
			throw new Exception("Factory failed");
		});
	}


	[Fact]
	public async Task FailSafeMaxDurationNormalizationOccursAsync()
	{
		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		using var fusionCache = new FusionCache(new FusionCacheOptions());
		await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration));
		await Task.Delay(maxDuration.PlusALittleBit());
		var value = await fusionCache.GetOrDefaultAsync<int>("foo", opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Fact]
	public void FailSafeMaxDurationNormalizationOccurs()
	{
		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		using var fusionCache = new FusionCache(new FusionCacheOptions());
		fusionCache.Set<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration));
		Thread.Sleep(maxDuration.PlusALittleBit());
		var value = fusionCache.GetOrDefault<int>("foo", opt => opt.SetFailSafe(true));
		Assert.Equal(21, value);
	}

	[Fact]
	public async Task ReturnsStaleDataWithoutSavingItWhenNoFactoryAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));
		await Task.Delay(1_500);
		var maybeValue = await cache.TryGetAsync<int>("foo", opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		var defaultValue1 = await cache.GetOrDefaultAsync<int>("foo", 1);
		var defaultValue2 = await cache.GetOrDefaultAsync<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		var defaultValue3 = await cache.GetOrDefaultAsync<int>("foo", 3);

		Assert.True(maybeValue.HasValue);
		Assert.Equal(42, maybeValue.Value);
		Assert.Equal(1, defaultValue1);
		Assert.Equal(42, defaultValue2);
		Assert.Equal(3, defaultValue3);
	}

	[Fact]
	public void ReturnsStaleDataWithoutSavingItWhenNoFactory()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));
		Thread.Sleep(1_500);
		var maybeValue = cache.TryGet<int>("foo", opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		var defaultValue1 = cache.GetOrDefault<int>("foo", 1);
		var defaultValue2 = cache.GetOrDefault<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		var defaultValue3 = cache.GetOrDefault<int>("foo", 3);

		Assert.True(maybeValue.HasValue);
		Assert.Equal(42, maybeValue.Value);
		Assert.Equal(1, defaultValue1);
		Assert.Equal(42, defaultValue2);
		Assert.Equal(3, defaultValue3);
	}

	[Fact]
	public async Task CanHandleInfiniteOrSimilarDurationsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42, opt => opt.SetDuration(TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1)).SetJittering(TimeSpan.FromMinutes(10)));
		var foo = await cache.GetOrDefaultAsync<int>("foo", 0);
		Assert.Equal(42, foo);
	}

	[Fact]
	public void CanHandleInfiniteOrSimilarDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, opt => opt.SetDuration(TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1)).SetJittering(TimeSpan.FromMinutes(10)));
		var foo = cache.GetOrDefault<int>("foo", 0);
		Assert.Equal(42, foo);
	}

	[Fact]
	public async Task CanHandleZeroDurationsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 10, opt => opt.SetDuration(TimeSpan.Zero));
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1);

		await cache.SetAsync<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)));
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2);

		await cache.SetAsync<int>("foo", 30, opt => opt.SetDuration(TimeSpan.Zero));
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public void CanHandleZeroDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 10, opt => opt.SetDuration(TimeSpan.Zero));
		var foo1 = cache.GetOrDefault<int>("foo", 1);

		cache.Set<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)));
		var foo2 = cache.GetOrDefault<int>("foo", 2);

		cache.Set<int>("foo", 30, opt => opt.SetDuration(TimeSpan.Zero));
		var foo3 = cache.GetOrDefault<int>("foo", 3);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanHandleNegativeDurationsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 10, opt => opt.SetDuration(TimeSpan.FromSeconds(-100)));
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1);

		await cache.SetAsync<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)));
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2);

		await cache.SetAsync<int>("foo", 30, opt => opt.SetDuration(TimeSpan.FromDays(-100)));
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public void CanHandleNegativeDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 10, opt => opt.SetDuration(TimeSpan.FromSeconds(-100)));
		var foo1 = cache.GetOrDefault<int>("foo", 1);

		cache.Set<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)));
		var foo2 = cache.GetOrDefault<int>("foo", 2);

		cache.Set<int>("foo", 30, opt => opt.SetDuration(TimeSpan.FromDays(-100)));
		var foo3 = cache.GetOrDefault<int>("foo", 3);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanHandleConditionalRefreshAsync()
	{
		static async Task<int> FakeGetAsync(FusionCacheFactoryExecutionContext<int> ctx, FakeHttpEndpoint endpoint)
		{
			FakeHttpResponse resp;

			if (ctx.HasETag && ctx.HasStaleValue)
			{
				// ETAG + STALE VALUE -> TRY WITH A CONDITIONAL GET
				resp = endpoint.Get(ctx.ETag);

				if (resp.StatusCode == 304)
				{
					// NOT MODIFIED -> RETURN STALE VALUE
					return ctx.NotModified();
				}
			}
			else
			{
				// NO STALE VALUE OR NO ETAG -> NORMAL (FULL) GET
				resp = endpoint.Get();
			}

			return ctx.Modified(
				resp.Content.GetValueOrDefault(),
				resp.ETag
			);
		}

		var duration = TimeSpan.FromSeconds(1);
		var endpoint = new FakeHttpEndpoint(1);

		using var cache = new FusionCache(new FusionCacheOptions());
		// TOT REQ + 1 / FULL RESP + 1
		var v1 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// CACHED -> NO INCR
		var v2 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		Assert.Equal(4, endpoint.TotalRequestsCount);
		Assert.Equal(3, endpoint.ConditionalRequestsCount);
		Assert.Equal(2, endpoint.FullResponsesCount);
		Assert.Equal(2, endpoint.NotModifiedResponsesCount);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(1, v4);
		Assert.Equal(42, v5);
	}

	[Fact]
	public void CanHandleConditionalRefresh()
	{
		static int FakeGet(FusionCacheFactoryExecutionContext<int> ctx, FakeHttpEndpoint endpoint)
		{
			FakeHttpResponse resp;

			if (ctx.HasETag && ctx.HasStaleValue)
			{
				// ETAG + STALE VALUE -> TRY WITH A CONDITIONAL GET
				resp = endpoint.Get(ctx.ETag);

				if (resp.StatusCode == 304)
				{
					// NOT MODIFIED -> RETURN STALE VALUE
					return ctx.NotModified();
				}
			}
			else
			{
				// NO STALE VALUE OR NO ETAG -> NORMAL (FULL) GET
				resp = endpoint.Get();
			}

			return ctx.Modified(
				resp.Content.GetValueOrDefault(),
				resp.ETag
			);
		}

		var duration = TimeSpan.FromSeconds(1);
		var endpoint = new FakeHttpEndpoint(1);

		using var cache = new FusionCache(new FusionCacheOptions());
		// TOT REQ + 1 / FULL RESP + 1
		var v1 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// CACHED -> NO INCR
		var v2 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true));

		Assert.Equal(4, endpoint.TotalRequestsCount);
		Assert.Equal(3, endpoint.ConditionalRequestsCount);
		Assert.Equal(2, endpoint.FullResponsesCount);
		Assert.Equal(2, endpoint.NotModifiedResponsesCount);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(1, v4);
		Assert.Equal(42, v5);
	}

	[Fact]
	public async Task CanHandleEagerRefreshAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v6 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Fact]
	public void CanHandleEagerRefresh()
	{
		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var v3 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v6 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Fact]
	public async Task CanHandleEagerRefreshWithInfiniteDurationAsync()
	{
		var duration = TimeSpan.MaxValue;
		var eagerRefreshThreshold = 0.5f;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.True(v1 > 0);
	}

	[Fact]
	public void CanHandleEagerRefreshWithInfiniteDuration()
	{
		var duration = TimeSpan.MaxValue;
		var eagerRefreshThreshold = 0.5f;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		Assert.True(v1 > 0);
	}

	[Fact]
	public async Task CanHandleEagerRefreshNoCancellationAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var lockTimeout = TimeSpan.FromSeconds(10);
		var eagerRefreshThreshold = 0.1f;
		var eagerRefreshDelay = TimeSpan.FromSeconds(5);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// EAGER REFRESH KICKS IN
		var eagerRefreshIsStarted = false;
		var eagerRefreshIsEnded = false;
		using var cts = new CancellationTokenSource();
		long v3EagerResult = 0;
		var v3 = await cache.GetOrSetAsync<long>(
			"foo",
			async ct =>
			{
				eagerRefreshIsStarted = true;

				await Task.Delay(eagerRefreshDelay);

				ct.ThrowIfCancellationRequested();

				eagerRefreshIsEnded = true;

				return v3EagerResult = DateTimeOffset.UtcNow.Ticks;
			},
			token: cts.Token
		);

		// ALLOW EAGER REFRESH TO START
		await Task.Delay(TimeSpan.FromMilliseconds(50));

		// CANCEL
		cts.Cancel();

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(eagerRefreshDelay.PlusALittleBit());

		// GET THE REFRESHED VALUE
		var sw = Stopwatch.StartNew();
		var v4SupposedlyNot = DateTimeOffset.UtcNow.Ticks;
		var v4 = await cache.GetOrSetAsync<long>(
			"foo",
			async _ => v4SupposedlyNot,
			options =>
			{
				options.LockTimeout = lockTimeout;
			}
		);
		sw.Stop();
		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(eagerRefreshIsStarted);
		Assert.True(eagerRefreshIsEnded);
		Assert.True(elapsedMs < lockTimeout.TotalMilliseconds);
		Assert.True(v4 > v3);
		Assert.True(v4 == v3EagerResult);
		Assert.False(v4 == v4SupposedlyNot);
	}

	[Fact]
	public void CanHandleEagerRefreshNoCancellation()
	{
		var duration = TimeSpan.FromSeconds(2);
		var lockTimeout = TimeSpan.FromSeconds(10);
		var eagerRefreshThreshold = 0.1f;
		var eagerRefreshDelay = TimeSpan.FromSeconds(5);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var eagerRefreshIsStarted = false;
		var eagerRefreshIsEnded = false;
		using var cts = new CancellationTokenSource();
		long v3EagerResult = 0;
		var v3 = cache.GetOrSet<long>(
			"foo",
			ct =>
			{
				eagerRefreshIsStarted = true;

				Thread.Sleep(eagerRefreshDelay);

				ct.ThrowIfCancellationRequested();

				eagerRefreshIsEnded = true;

				return v3EagerResult = DateTimeOffset.UtcNow.Ticks;
			},
			token: cts.Token
		);

		// ALLOW EAGER REFRESH TO START
		Thread.Sleep(TimeSpan.FromMilliseconds(50));

		// CANCEL
		cts.Cancel();

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(eagerRefreshDelay.PlusALittleBit());

		// GET THE REFRESHED VALUE
		var sw = Stopwatch.StartNew();
		var v4SupposedlyNot = DateTimeOffset.UtcNow.Ticks;
		var v4 = cache.GetOrSet<long>(
			"foo",
			_ => v4SupposedlyNot,
			options =>
			{
				options.LockTimeout = lockTimeout;
			}
		);
		sw.Stop();
		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(eagerRefreshIsStarted);
		Assert.True(eagerRefreshIsEnded);
		Assert.True(elapsedMs < lockTimeout.TotalMilliseconds);
		Assert.True(v4 > v3);
		Assert.True(v4 == v3EagerResult);
		Assert.False(v4 == v4SupposedlyNot);
	}

	[Fact]
	public async Task NormalFactoryExecutionWaitsForInFlightEagerRefreshAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;
		var eagerRefreshThresholdDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold);
		var simulatedDelay = TimeSpan.FromSeconds(4);
		var value = 0;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		await Task.Delay(eagerRefreshThresholdDuration.Add(TimeSpan.FromMilliseconds(10)));

		// EAGER REFRESH KICKS IN (WITH DELAY)
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			await Task.Delay(simulatedDelay);

			Interlocked.Increment(ref value);
			return value;
		});

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit());

		// TRY TO GET EXPIRED ENTRY: NORMALLY THIS WOULD FIRE THE FACTORY, BUT SINCE IT
		// IS ALRADY RUNNING BECAUSE OF EAGER REFRESH, IT WILL WAIT FOR IT TO COMPLETE
		// AND USE THE RESULT, SAVING ONE FACTORY EXECUTION
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// USE CACHED VALUE
		var v5 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(2, v4);
		Assert.Equal(2, v5);
		Assert.Equal(2, value);
	}

	[Fact]
	public void NormalFactoryExecutionWaitsForInFlightEagerRefresh()
	{
		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;
		var eagerRefreshThresholdDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold);
		var simulatedDelay = TimeSpan.FromSeconds(4);
		var value = 0;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		Thread.Sleep(eagerRefreshThresholdDuration.Add(TimeSpan.FromMilliseconds(10)));

		// EAGER REFRESH KICKS IN (WITH DELAY)
		var v3 = cache.GetOrSet<long>("foo", _ =>
		{
			Thread.Sleep(simulatedDelay);

			Interlocked.Increment(ref value);
			return value;
		});

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// TRY TO GET EXPIRED ENTRY: NORMALLY THIS WOULD FIRE THE FACTORY, BUT SINCE IT
		// IS ALRADY RUNNING BECAUSE OF EAGER REFRESH, IT WILL WAIT FOR IT TO COMPLETE
		// AND USE THE RESULT, SAVING ONE FACTORY EXECUTION
		var v4 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		// USE CACHED VALUE
		var v5 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		});

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(2, v4);
		Assert.Equal(2, v5);
		Assert.Equal(2, value);
	}

	[Fact]
	public async Task CanExpireAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

		await cache.SetAsync<int>("foo", 42);
		var maybeFoo1 = await cache.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));
		await cache.ExpireAsync("foo");
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));
		var maybeFoo3 = await cache.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));
		Assert.True(maybeFoo1.HasValue);
		Assert.Equal(42, maybeFoo1.Value);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.Equal(42, maybeFoo3.Value);
	}

	[Fact]
	public void CanExpire()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

		cache.Set<int>("foo", 42);
		var maybeFoo1 = cache.TryGet<int>("foo", opt => opt.SetFailSafe(false));
		cache.Expire("foo");
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetFailSafe(false));
		var maybeFoo3 = cache.TryGet<int>("foo", opt => opt.SetFailSafe(true));
		Assert.True(maybeFoo1.HasValue);
		Assert.Equal(42, maybeFoo1.Value);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.Equal(42, maybeFoo3.Value);
	}

	[Fact]
	public async Task CanSkipMemoryCacheAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 42, opt => opt.SetSkipMemoryCache());
		var maybeFoo1 = await cache.TryGetAsync<int>("foo");
		await cache.SetAsync<int>("foo", 42);
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCache());
		var maybeFoo3 = await cache.TryGetAsync<int>("foo");
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCache());
		var maybeFoo4 = await cache.TryGetAsync<int>("foo");
		await cache.RemoveAsync("foo");
		var maybeFoo5 = await cache.TryGetAsync<int>("foo");

		await cache.GetOrSetAsync<int>("bar", 42, opt => opt.SetSkipMemoryCache());
		var maybeBar = await cache.TryGetAsync<int>("bar");

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.True(maybeFoo4.HasValue);
		Assert.False(maybeFoo5.HasValue);

		Assert.False(maybeBar.HasValue);
	}

	[Fact]
	public void CanSkipMemoryCache()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 42, opt => opt.SetSkipMemoryCache());
		var maybeFoo1 = cache.TryGet<int>("foo");
		cache.Set<int>("foo", 42);
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCache());
		var maybeFoo3 = cache.TryGet<int>("foo");
		cache.Remove("foo", opt => opt.SetSkipMemoryCache());
		var maybeFoo4 = cache.TryGet<int>("foo");
		cache.Remove("foo");
		var maybeFoo5 = cache.TryGet<int>("foo");

		cache.GetOrSet<int>("bar", 42, opt => opt.SetSkipMemoryCache());
		var maybeBar = cache.TryGet<int>("bar");

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.True(maybeFoo4.HasValue);
		Assert.False(maybeFoo5.HasValue);

		Assert.False(maybeBar.HasValue);
	}

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

		cache.Set<int>("foo", 42);

		var maybeFoo1 = cache.TryGet<int>("foo");

		cache.Remove("foo");

		var maybeBar1 = cache.TryGet<int>("bar");

		cache.Expire("qux");

		var qux1 = cache.GetOrSet("qux", _ => 1);
		var qux2 = cache.GetOrSet("qux", _ => 2);
		var qux3 = cache.GetOrSet("qux", _ => 3);
		var qux4 = cache.GetOrDefault("qux", 4);

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
			_ = cache.GetOrSet<int>("qux", _ => throw new UnreachableException("Sloths"));
		});
	}

	[Fact]
	public async Task FailSafeMaxDurationIsRespectedAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var throttleDuration = TimeSpan.FromSeconds(1);
		var maxDuration = TimeSpan.FromSeconds(5);
		var exceptionMessage = "Sloths are cool";

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;
		options.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;

		using var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());

		await fusionCache.SetAsync<int>("foo", 21);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit());
				await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception(exceptionMessage));
			} while (sw.Elapsed < maxDuration + throttleDuration);
		}
		catch (Exception exc) when (exc.Message == exceptionMessage)
		{
			didThrow = true;
		}
		TestOutput.WriteLine($"-- END AT {DateTime.UtcNow}");
		sw.Stop();

		Assert.True(didThrow);
	}

	[Fact]
	public void FailSafeMaxDurationIsRespected()
	{
		var duration = TimeSpan.FromSeconds(2);
		var throttleDuration = TimeSpan.FromSeconds(1);
		var maxDuration = TimeSpan.FromSeconds(5);
		var exceptionMessage = "Sloths are cool";

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;
		options.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;

		using var fusionCache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());

		fusionCache.Set<int>("foo", 21);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				Thread.Sleep(throttleDuration.PlusALittleBit());
				fusionCache.GetOrSet<int>("foo", _ => throw new Exception(exceptionMessage));
			} while (sw.Elapsed < maxDuration + throttleDuration);
		}
		catch (Exception exc) when (exc.Message == exceptionMessage)
		{
			didThrow = true;
		}
		TestOutput.WriteLine($"-- END AT {DateTime.UtcNow}");
		sw.Stop();

		Assert.True(didThrow);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanAutoCloneAsync(SerializerType serializerType)
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options.DefaultEntryOptions.EnableAutoClone = true;
		using var cache = new FusionCache(options);

		cache.SetupSerializer(TestsUtils.GetSerializer(serializerType));

		var foo = new ComplexType()
		{
			PropInt = -1
		};

		await cache.SetAsync("foo", foo);

		var foo1 = (await cache.GetOrDefaultAsync<ComplexType>("foo"))!;
		foo1.PropInt = 1;

		var foo2 = (await cache.GetOrDefaultAsync<ComplexType>("foo"))!;
		foo2.PropInt = 2;

		var foo3 = (await cache.GetOrDefaultAsync<ComplexType>("foo"))!;
		foo3.PropInt = 3;

		Assert.Equal(-1, foo.PropInt);

		Assert.NotNull(foo1);
		Assert.NotSame(foo, foo1);
		Assert.Equal(1, foo1.PropInt);

		Assert.NotNull(foo2);
		Assert.NotSame(foo, foo2);
		Assert.NotSame(foo1, foo2);
		Assert.Equal(2, foo2.PropInt);

		Assert.NotNull(foo3);
		Assert.NotSame(foo, foo3);
		Assert.NotSame(foo1, foo3);
		Assert.NotSame(foo2, foo3);
		Assert.Equal(3, foo3.PropInt);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanAutoClone(SerializerType serializerType)
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.EnableAutoClone = true;
		using var cache = new FusionCache(options);

		cache.SetupSerializer(TestsUtils.GetSerializer(serializerType));

		var foo = new ComplexType()
		{
			PropInt = -1
		};

		cache.Set("foo", foo);

		var foo1 = cache.GetOrDefault<ComplexType>("foo")!;
		foo1.PropInt = 1;

		var foo2 = cache.GetOrDefault<ComplexType>("foo")!;
		foo2.PropInt = 2;

		var foo3 = cache.GetOrDefault<ComplexType>("foo")!;
		foo3.PropInt = 3;

		Assert.Equal(-1, foo.PropInt);

		Assert.NotNull(foo1);
		Assert.False(object.ReferenceEquals(foo, foo1));
		Assert.Equal(1, foo1.PropInt);

		Assert.NotNull(foo2);
		Assert.False(object.ReferenceEquals(foo, foo2));
		Assert.Equal(2, foo2.PropInt);

		Assert.NotNull(foo3);
		Assert.False(object.ReferenceEquals(foo, foo3));
		Assert.Equal(3, foo3.PropInt);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task AutoCloneSkipsImmutableObjectsAsync(SerializerType serializerType)
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options.DefaultEntryOptions.EnableAutoClone = true;
		using var cache = new FusionCache(options);

		cache.SetupSerializer(TestsUtils.GetSerializer(serializerType));

		var imm = new SimpleImmutableObject
		{
			Name = "Imm",
			Age = 123
		};

		await cache.SetAsync("imm", imm);

		var imm1 = (await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm"))!;
		var imm2 = (await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm"))!;

		Assert.Same(imm, imm1);
		Assert.Same(imm, imm2);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void AutoCloneSkipsImmutableObjects(SerializerType serializerType)
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options.DefaultEntryOptions.EnableAutoClone = true;
		using var cache = new FusionCache(options);

		cache.SetupSerializer(TestsUtils.GetSerializer(serializerType));

		var imm = new SimpleImmutableObject
		{
			Name = "Imm",
			Age = 123
		};

		cache.Set("imm", imm);

		var imm1 = cache.GetOrDefault<SimpleImmutableObject>("imm")!;
		var imm2 = cache.GetOrDefault<SimpleImmutableObject>("imm")!;

		Assert.Same(imm, imm1);
		Assert.Same(imm, imm2);
	}

	[Fact]
	public async Task CanRemoveByTagAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, logger: logger);

		await cache.SetAsync<int>("foo", 1, tags: ["x", "y"]);
		await cache.SetAsync<int>("bar", 2, tags: ["y", "z"]);
		await cache.GetOrSetAsync<int>("baz", async (_, _) => 3, tags: ["x", "z"]);

		var foo1 = await cache.GetOrSetAsync<int>("foo", async (_, _) => 11, tags: ["x", "y"]);
		var bar1 = await cache.GetOrSetAsync<int>("bar", async (_, _) => 22, tags: ["y", "z"]);
		var baz1 = await cache.GetOrSetAsync<int>("baz", async (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		});

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		await cache.RemoveByTagAsync("x");

		var foo2 = await cache.GetOrDefaultAsync<int>("foo");
		var bar2 = await cache.GetOrSetAsync<int>("bar", async (_, _) => 222, tags: ["y", "z"]);
		var baz2 = await cache.GetOrSetAsync<int>("baz", async (_, _) => 333, tags: ["x", "z"]);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		await cache.RemoveByTagAsync("y");

		var foo3 = await cache.GetOrSetAsync<int>("foo", async (_, _) => 1111, tags: ["x", "y"]);
		var bar3 = await cache.GetOrSetAsync<int>("bar", async (_, _) => 2222, tags: ["y", "z"]);
		var baz3 = await cache.GetOrSetAsync<int>("baz", async (_, _) => 3333, tags: ["x", "z"]);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Fact]
	public void CanRemoveByTag()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.Set<int>("foo", 1, tags: ["x", "y"]);
		cache.Set<int>("bar", 2, tags: ["y", "z"]);
		cache.GetOrSet<int>("baz", (_, _) => 3, tags: ["x", "z"]);

		var foo1 = cache.GetOrSet<int>("foo", (_, _) => 11, tags: ["x", "y"]);
		var bar1 = cache.GetOrSet<int>("bar", (_, _) => 22, tags: ["y", "z"]);
		var baz1 = cache.GetOrSet<int>("baz", (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		});

		cache.RemoveByTag("x");

		var foo2 = cache.GetOrDefault<int>("foo");
		var bar2 = cache.GetOrSet<int>("bar", (_, _) => 222, tags: ["y", "z"]);
		var baz2 = cache.GetOrSet<int>("baz", (_, _) => 333, tags: ["x", "z"]);

		cache.RemoveByTag("y");

		var foo3 = cache.GetOrSet<int>("foo", (_, _) => 1111, tags: ["x", "y"]);
		var bar3 = cache.GetOrSet<int>("bar", (_, _) => 2222, tags: ["y", "z"]);
		var baz3 = cache.GetOrSet<int>("baz", (_, _) => 3333, tags: ["x", "z"]);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Fact]
	public async Task CanClearAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// CACHE A: PASSING A MEMORY CACHE -> CANNOT EXECUTE RAW CLEAR
		MemoryCache? mcA = new MemoryCache(new MemoryCacheOptions());
		using var cacheA = new FusionCache(new FusionCacheOptions() { CacheName = "CACHE_A" }, mcA, logger: logger);

		// CACHE B: NOT PASSING A MEMORY CACHE -> CAN EXECUTE RAW CLEAR
		using var cacheB = new FusionCache(new FusionCacheOptions() { CacheName = "CACHE_B" }, logger: logger);
		var mcB = TestsUtils.GetMemoryCache(cacheB) as MemoryCache;

		await cacheA.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cacheA.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cacheA.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		await cacheB.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cacheB.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		await cacheB.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		// BOTH CACHES HAVE 3 ITEMS
		Assert.Equal(3, mcA.Count);
		Assert.Equal(3, mcB?.Count);

		var fooA1 = await cacheA.GetOrDefaultAsync<int>("foo");
		var barA1 = await cacheA.GetOrDefaultAsync<int>("bar");
		var bazA1 = await cacheA.GetOrDefaultAsync<int>("baz");

		var fooB1 = await cacheB.GetOrDefaultAsync<int>("foo");
		var barB1 = await cacheB.GetOrDefaultAsync<int>("bar");
		var bazB1 = await cacheB.GetOrDefaultAsync<int>("baz");

		await cacheA.ClearAsync(false);
		await cacheB.ClearAsync(false);

		// CACHE A HAS 5 ITEMS (3 FOR ITEMS + 1 FOR THE * TAG + 1 FOR THE ** TAG)
		Assert.Equal(5, mcA.Count);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		var fooA2 = await cacheA.GetOrDefaultAsync<int>("foo");
		var barA2 = await cacheA.GetOrDefaultAsync<int>("bar");
		var bazA2 = await cacheA.GetOrDefaultAsync<int>("baz");

		var fooB2 = await cacheB.GetOrDefaultAsync<int>("foo");
		var barB2 = await cacheB.GetOrDefaultAsync<int>("bar");
		var bazB2 = await cacheB.GetOrDefaultAsync<int>("baz");

		Assert.Equal(1, fooA1);
		Assert.Equal(2, barA1);
		Assert.Equal(3, bazA1);

		Assert.Equal(1, fooB1);
		Assert.Equal(2, barB1);
		Assert.Equal(3, bazB1);

		Assert.Equal(0, fooA2);
		Assert.Equal(0, barA2);
		Assert.Equal(0, bazA2);

		Assert.Equal(0, fooB2);
		Assert.Equal(0, barB2);
		Assert.Equal(0, bazB2);

		// CACHE A HAS MORE THAN 0 ITEMS (CANNOT BE PRECISE, BECAUSE INTERNAL UNKNOWN BEHAVIOUR)
		Assert.True(mcA.Count > 0);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);
	}

	[Fact]
	public void CanClear()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// CACHE A: PASSING A MEMORY CACHE -> CANNOT EXECUTE RAW CLEAR
		MemoryCache? mcA = new MemoryCache(new MemoryCacheOptions());
		using var cacheA = new FusionCache(new FusionCacheOptions() { CacheName = "CACHE_A" }, mcA, logger: logger);

		// CACHE B: NOT PASSING A MEMORY CACHE -> CAN EXECUTE RAW CLEAR
		using var cacheB = new FusionCache(new FusionCacheOptions() { CacheName = "CACHE_B" }, logger: logger);
		var mcB = TestsUtils.GetMemoryCache(cacheB) as MemoryCache;

		cacheA.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cacheA.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cacheA.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		cacheB.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cacheB.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)));
		cacheB.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)));

		// BOTH CACHES HAVE 3 ITEMS
		Assert.Equal(3, mcA.Count);
		Assert.Equal(3, mcB?.Count);

		var fooA1 = cacheA.GetOrDefault<int>("foo");
		var barA1 = cacheA.GetOrDefault<int>("bar");
		var bazA1 = cacheA.GetOrDefault<int>("baz");

		var fooB1 = cacheB.GetOrDefault<int>("foo");
		var barB1 = cacheB.GetOrDefault<int>("bar");
		var bazB1 = cacheB.GetOrDefault<int>("baz");

		cacheA.Clear(false);
		cacheB.Clear(false);

		// CACHE A HAS 5 ITEMS (3 FOR ITEMS + 1 FOR THE * TAG + 1 FOR THE ** TAG)
		Assert.Equal(5, mcA.Count);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var fooA2 = cacheA.GetOrDefault<int>("foo");
		var barA2 = cacheA.GetOrDefault<int>("bar");
		var bazA2 = cacheA.GetOrDefault<int>("baz");

		var fooB2 = cacheB.GetOrDefault<int>("foo");
		var barB2 = cacheB.GetOrDefault<int>("bar");
		var bazB2 = cacheB.GetOrDefault<int>("baz");

		Assert.Equal(1, fooA1);
		Assert.Equal(2, barA1);
		Assert.Equal(3, bazA1);

		Assert.Equal(1, fooB1);
		Assert.Equal(2, barB1);
		Assert.Equal(3, bazB1);

		Assert.Equal(0, fooA2);
		Assert.Equal(0, barA2);
		Assert.Equal(0, bazA2);

		Assert.Equal(0, fooB2);
		Assert.Equal(0, barB2);
		Assert.Equal(0, bazB2);

		// CACHE A HAS MORE THAN 0 ITEMS (CANNOT BE PRECISE, BECAUSE INTERNAL UNKNOWN BEHAVIOUR)
		Assert.True(mcA.Count > 0);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);
	}

	[Fact]
	public async Task CanClearWithFailSafeAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// NOT PASSING A MEMORY CACHE -> CAN EXECUTE RAW CLEAR
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		await cache.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

		var foo1 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(1, foo1);

		await cache.ClearAsync();

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		var foo2 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(1, foo2);

		await cache.ClearAsync(false);

		await Task.Delay(TimeSpan.FromMilliseconds(100));

		var foo3 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(0, foo3);
	}

	[Fact]
	public void CanClearWithFailSafe()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// NOT PASSING A MEMORY CACHE -> CAN EXECUTE RAW CLEAR
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true));

		var foo1 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(1, foo1);

		cache.Clear();

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var foo2 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(1, foo2);

		cache.Clear(false);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var foo3 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(true));

		Assert.Equal(0, foo3);
	}

	[Fact]
	public async Task CanSkipMemoryCacheReadWriteAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 42, opt => opt.SetSkipMemoryCacheWrite());
		var maybeFoo1 = await cache.TryGetAsync<int>("foo");
		await cache.SetAsync<int>("foo", 42);
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo3 = await cache.TryGetAsync<int>("foo");
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCacheWrite());
		var maybeFoo4 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo5 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheWrite());
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo6 = await cache.TryGetAsync<int>("foo");

		await cache.GetOrSetAsync<int>("bar", 42, opt => opt.SetSkipMemoryCache());
		var maybeBar = await cache.TryGetAsync<int>("bar");

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.False(maybeFoo4.HasValue);
		Assert.True(maybeFoo5.HasValue);
		Assert.False(maybeFoo6.HasValue);

		Assert.False(maybeBar.HasValue);
	}

	[Fact]
	public void CanSkipMemoryCacheReadWrite()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 42, opt => opt.SetSkipMemoryCacheWrite());
		var maybeFoo1 = cache.TryGet<int>("foo");
		cache.Set<int>("foo", 42);
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo3 = cache.TryGet<int>("foo");
		cache.Remove("foo", opt => opt.SetSkipMemoryCacheWrite());
		var maybeFoo4 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo5 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheWrite());
		cache.Remove("foo", opt => opt.SetSkipMemoryCacheRead());
		var maybeFoo6 = cache.TryGet<int>("foo");

		cache.GetOrSet<int>("bar", 42, opt => opt.SetSkipMemoryCache());
		var maybeBar = cache.TryGet<int>("bar");

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.False(maybeFoo4.HasValue);
		Assert.True(maybeFoo5.HasValue);
		Assert.False(maybeFoo6.HasValue);

		Assert.False(maybeBar.HasValue);
	}

	[Fact]
	public async Task CanSoftFailWithSoftTimeoutAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var value1 = await cache.GetOrSetAsync<int?>("foo", async _ => 42, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Assert.True(value1.HasValue);
		Assert.Equal(42, value1.Value);

		await Task.Delay(1_100);

		var value2 = await cache.GetOrSetAsync<int?>("foo", async (ctx, _) => { await Task.Delay(1_000); return ctx.Fail("Some error"); }, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.True(value2.HasValue);
		Assert.Equal(42, value2.Value);

		await Task.Delay(1_100);

		var value3 = await cache.GetOrDefaultAsync<int?>("foo", options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.True(value3.HasValue);
		Assert.Equal(42, value3.Value);
	}

	[Fact]
	public void CanSoftFailWithSoftTimeout()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var value1 = cache.GetOrSet<int?>("foo", _ => 42, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true));
		Assert.True(value1.HasValue);
		Assert.Equal(42, value1.Value);

		Thread.Sleep(1_100);

		var value2 = cache.GetOrSet<int?>("foo", (ctx, _) => { Thread.Sleep(1_000); return ctx.Fail("Some error"); }, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.True(value2.HasValue);
		Assert.Equal(42, value2.Value);

		Thread.Sleep(1_100);

		var value3 = cache.GetOrDefault<int?>("foo", options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100));
		Assert.True(value3.HasValue);
		Assert.Equal(42, value3.Value);
	}

	[Fact]
	public async Task CanDisableTaggingAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { DisableTagging = true }, logger: logger);

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.SetAsync<int>("foo", 1, tags: ["x", "y"]);
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.GetOrSetAsync<int>("bar", async (_, _) => 3, tags: ["x", "z"]);
		});

		var foo1 = await cache.GetOrDefaultAsync<int>("foo");
		var bar1 = await cache.GetOrDefaultAsync<int>("bar");

		Assert.Equal(0, foo1);
		Assert.Equal(0, bar1);

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.RemoveByTagAsync("x");
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.ClearAsync(false);
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.ClearAsync();
		});
	}

	[Fact]
	public void CanDisableTagging()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { DisableTagging = true }, logger: logger);

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Set<int>("foo", 1, tags: ["x", "y"]);
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.GetOrSet<int>("bar", (_, _) => 3, tags: ["x", "z"]);
		});

		var foo1 = cache.GetOrDefault<int>("foo");
		var bar1 = cache.GetOrDefault<int>("bar");

		Assert.Equal(0, foo1);
		Assert.Equal(0, bar1);

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.RemoveByTag("x");
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Clear(false);
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Clear();
		});
	}

	[Fact]
	public async Task CanHandleEagerRefreshWithTagsAsync()
	{
		var duration = TimeSpan.FromSeconds(4);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration);

		// EAGER REFRESH KICKS IN
		var expectedValue = DateTimeOffset.UtcNow.Ticks;
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ => expectedValue, tags: ["c", "d"]);

		Assert.Equal(v2, v3);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(expectedValue, v4);
		Assert.True(v4 > v3);

		await cache.RemoveByTagAsync("c");

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrDefaultAsync<long>("foo");

		Assert.Equal(0, v5);
	}

	[Fact]
	public void CanHandleEagerRefreshWithTags()
	{
		var duration = TimeSpan.FromSeconds(4);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(v1, v2);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var expectedValue = DateTimeOffset.UtcNow.Ticks;
		var v3 = cache.GetOrSet<long>("foo", _ => expectedValue, tags: ["c", "d"]);

		Assert.Equal(v2, v3);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks);

		Assert.Equal(expectedValue, v4);
		Assert.True(v4 > v3);

		cache.RemoveByTag("c");

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrDefault<long>("foo");

		Assert.Equal(0, v5);
	}
}
