using System.Diagnostics;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;

namespace FusionCacheTests;

public partial class L1Tests
{
	[Fact]
	public async Task CanRemoveAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", token: TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
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
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
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

		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, token: TestContext.Current.CancellationToken);

		await Task.Delay(500, TestContext.Current.CancellationToken);

		var newValue = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => ctx.Fail(errorMessage), token: TestContext.Current.CancellationToken);

		Assert.Equal(initialValue, newValue);

		await Task.Delay(throttleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);

		Exception? exc = null;
		try
		{
			_ = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => ctx.Fail(errorMessage), opt => opt.SetFailSafe(false)
, token: TestContext.Current.CancellationToken);
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
		var logger = CreateXUnitLogger<FusionCache>();

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var newValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false), token: TestContext.Current.CancellationToken);
			logger.LogInformation("NEW VALUE: {NewValue}", newValue);
		});
	}

	[Fact]
	public async Task ThrowsOnFactoryHardTimeoutWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
		{
			var value = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100), token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotSoftTimeoutWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, initialValue);
	}

	[Fact]
	public async Task DoesHardTimeoutEvenWithoutStaleDataAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500), token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500), token: TestContext.Current.CancellationToken);
		Assert.Equal(42, newValue);
	}

	[Fact]
	public async Task SetOverwritesAnExistingValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		var newValue = 21;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		var actualValue = await cache.GetOrDefaultAsync<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), TestContext.Current.CancellationToken);
		Assert.Equal(newValue, actualValue);
	}

	[Fact]
	public async Task GetOrSetDoesNotOverwriteANonExpiredValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotReturnStaleDataIfFactorySucceedsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesReturnStaleDataWithFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesNotReturnStaleDataWithoutFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		await cache.SetAsync<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = false }, token: TestContext.Current.CancellationToken);
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public async Task FactoryTimedOutButSuccessfulDoesUpdateCachedValueAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		await cache.SetAsync<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var middleValue = await cache.GetOrSetAsync<int>("foo", async ct => { await Task.Delay(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500), token: TestContext.Current.CancellationToken);
		var interstitialValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		await Task.Delay(3_000, TestContext.Current.CancellationToken);
		var finalValue = await cache.GetOrDefaultAsync<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

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
		var res1 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var res2 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
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
	public async Task HandlesFlexibleSimpleTypeConversionsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)42;
		await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task HandlesFlexibleComplexTypeConversionsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)ComplexType.CreateSample();
		await cache.SetAsync("foo", initialValue, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<ComplexType>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesNotSetAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = await cache.GetOrDefaultAsync<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		var bar = await cache.GetOrDefaultAsync<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		var baz = await cache.TryGetAsync<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo);
		Assert.Equal(21, bar);
		Assert.False(baz.HasValue);
	}

	[Fact]
	public async Task GetOrSetWithDefaultValueWorksAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = 42;
		await cache.GetOrSetAsync<int>("foo", foo, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var bar = await cache.GetOrDefaultAsync<int>("foo", 21, token: TestContext.Current.CancellationToken);
		Assert.Equal(foo, bar);
	}

	[Fact]
	public async Task ThrottleDurationWorksCorrectlyAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var duration = TimeSpan.FromSeconds(1);
		var throttleDuration = TimeSpan.FromSeconds(2);

		// SET THE VALUE (WITH FAIL-SAFE ENABLED)
		await cache.SetAsync("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration), token: TestContext.Current.CancellationToken);
		// LET IT EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);
		// CHECK EXPIRED (WITHOUT FAIL-SAFE)
		var nope = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		// DO NOT ACTIVATE FAIL-SAFE AND THROTTLE DURATION
		var default1 = await cache.GetOrDefaultAsync("foo", 1, token: TestContext.Current.CancellationToken);
		// ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
		var throttled1 = await cache.GetOrDefaultAsync("foo", 1, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		// WAIT A LITTLE BIT (LESS THAN THE DURATION)
		await Task.Delay(100, TestContext.Current.CancellationToken);
		// GET THE THROTTLED (NON EXPIRED) VALUE
		var throttled2 = await cache.GetOrDefaultAsync("foo", 2, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		// LET THE THROTTLE DURATION PASS
		await Task.Delay(throttleDuration, TestContext.Current.CancellationToken);
		// FALLBACK TO THE DEFAULT VALUE
		var default3 = await cache.GetOrDefaultAsync("foo", 3, token: TestContext.Current.CancellationToken);

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

		var default3 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(1);

				innerOpt = ctx.Options;

				return 3;
			}, opt => opt.SetFailSafe(false)
, token: TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

		var maybeValue = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

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
		await cache.SetAsync("foo", 21, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET IT BECOME STALE
		await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

		// CALL GetOrSET WITH A 1s SOFT TIMEOUT AND A FACTORY RUNNING FOR AT LEAST 3s
		var value21 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) =>
			{
				// WAIT 3s
				await Task.Delay(TimeSpan.FromSeconds(3));

				// CHANGE THE OPTIONS (SET THE DURATION TO 5s AND DISABLE FAIL-SAFE
				ctx.Options.SetDuration(TimeSpan.FromSeconds(5)).SetFailSafe(false);

				return 42;
			}, opt => opt.SetFactoryTimeouts(TimeSpan.FromSeconds(1)).SetFailSafe(true)
, token: TestContext.Current.CancellationToken);

		// WAIT FOR 3s (+ EXTRA 1s) SO THE FACTORY COMPLETES IN THE BACKGROUND
		await Task.Delay(TimeSpan.FromSeconds(3 + 1), TestContext.Current.CancellationToken);

		// GET THE VALUE THAT HAS BEEN SET BY THE BACKGROUND COMPLETION OF THE FACTORY
		var value42 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(false), token: TestContext.Current.CancellationToken);

		// LET THE CACHE ENTRY EXPIRES
		await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

		// SEE THAT FAIL-SAFE CANNOT BE ACTIVATED (BECAUSE IT WAS DISABLED IN THE FACTORY)
		var noValue = await cache.TryGetAsync<int>("foo", options => options.SetFailSafe(true), token: TestContext.Current.CancellationToken);

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

		_ = await cache.GetOrSetAsync<int>("foo", async (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(20);
				return 42;
			}, options
, token: TestContext.Current.CancellationToken);

		Assert.Equal(options.Duration, TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task AdaptiveCachingCanWorkWithSkipMemoryCacheAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		cache.DefaultEntryOptions.FailSafeThrottleDuration = TimeSpan.FromSeconds(3);

		var foo1 = await cache.GetOrSetAsync<int>("foo", async _ => 1, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);

		await Task.Delay(TimeSpan.FromSeconds(1).PlusALittleBit(), TestContext.Current.CancellationToken);

		var foo2 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) =>
		{
			ctx.Options.SkipMemoryCacheRead = true;
			ctx.Options.SkipMemoryCacheWrite = true;

			return 2;
		}, token: TestContext.Current.CancellationToken);

		Assert.Equal(2, foo2);

		var foo3 = await cache.TryGetAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.True(foo3.HasValue);
		Assert.Equal(1, foo3.Value);

		await Task.Delay(cache.DefaultEntryOptions.FailSafeThrottleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);

		var foo4 = await cache.GetOrSetAsync<int>("foo", async _ => 4, token: TestContext.Current.CancellationToken);

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

		cache.GetOrSet(key, "bar", token: TestContext.Current.CancellationToken);

		// LOGICALLY EXPIRE THE KEY SO THE FAIL-SAFE LOGIC TRIGGERS
		cache.Expire(key, token: TestContext.Current.CancellationToken);

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
			}, token: TestContext.Current.CancellationToken);
		});

		await cache.GetOrSetAsync<string>(key, async (ctx, ct) =>
		{
			throw new Exception("Factory failed");
		}, token: TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task FailSafeMaxDurationNormalizationOccursAsync()
	{
		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		using var fusionCache = new FusionCache(new FusionCacheOptions());
		await fusionCache.SetAsync<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration), token: TestContext.Current.CancellationToken);
		await Task.Delay(maxDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
		var value = await fusionCache.GetOrDefaultAsync<int>("foo", opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Fact]
	public async Task ReturnsStaleDataWithoutSavingItWhenNoFactoryAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)), token: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var maybeValue = await cache.TryGetAsync<int>("foo", opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var defaultValue1 = await cache.GetOrDefaultAsync<int>("foo", 1, token: TestContext.Current.CancellationToken);
		var defaultValue2 = await cache.GetOrDefaultAsync<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var defaultValue3 = await cache.GetOrDefaultAsync<int>("foo", 3, token: TestContext.Current.CancellationToken);

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
		await cache.SetAsync<int>("foo", 42, opt => opt.SetDuration(TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1)).SetJittering(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo = await cache.GetOrDefaultAsync<int>("foo", 0, token: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo);
	}

	[Fact]
	public async Task CanHandleZeroDurationsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 10, opt => opt.SetDuration(TimeSpan.Zero), token: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1, token: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2, token: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 30, opt => opt.SetDuration(TimeSpan.Zero), token: TestContext.Current.CancellationToken);
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanHandleNegativeDurationsAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 10, opt => opt.SetDuration(TimeSpan.FromSeconds(-100)), token: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1, token: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2, token: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 30, opt => opt.SetDuration(TimeSpan.FromDays(-100)), token: TestContext.Current.CancellationToken);
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanConditionalRefreshAsync()
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
		var v1 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// CACHED -> NO INCR
		var v2 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => await FakeGetAsync(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

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
	public async Task CanEagerRefreshAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN
		var eagerRefreshValue = DateTimeOffset.UtcNow.Ticks;
		logger.LogInformation("EAGER REFRESH VALUE: {0}", eagerRefreshValue);
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ => eagerRefreshValue, token: TestContext.Current.CancellationToken);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v6 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.Equal(eagerRefreshValue, v4);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Fact]
	public async Task CanEagerRefreshWithInfiniteDurationAsync()
	{
		var duration = TimeSpan.MaxValue;
		var eagerRefreshThreshold = 0.5f;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.True(v1 > 0);
	}

	[Fact]
	public async Task CanEagerRefreshNoCancellationAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var lockTimeout = TimeSpan.FromSeconds(10);
		var eagerRefreshThreshold = 0.1f;
		var eagerRefreshDelay = TimeSpan.FromSeconds(5);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

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
		await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

		// CANCEL
		cts.Cancel();

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(eagerRefreshDelay.PlusALittleBit(), TestContext.Current.CancellationToken);

		// GET THE REFRESHED VALUE
		var sw = Stopwatch.StartNew();
		var v4SupposedlyNot = DateTimeOffset.UtcNow.Ticks;
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ => v4SupposedlyNot, options =>
			{
				options.LockTimeout = lockTimeout;
			}
, token: TestContext.Current.CancellationToken);
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
		}, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		await Task.Delay(eagerRefreshThresholdDuration.Add(TimeSpan.FromMilliseconds(10)), TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN (WITH DELAY)
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			await Task.Delay(simulatedDelay);

			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TRY TO GET EXPIRED ENTRY: NORMALLY THIS WOULD FIRE THE FACTORY, BUT SINCE IT
		// IS ALRADY RUNNING BECAUSE OF EAGER REFRESH, IT WILL WAIT FOR IT TO COMPLETE
		// AND USE THE RESULT, SAVING ONE FACTORY EXECUTION
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v5 = await cache.GetOrSetAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

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

		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo1 = await cache.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(false), token: TestContext.Current.CancellationToken);
		await cache.ExpireAsync("foo", token: TestContext.Current.CancellationToken);
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(false), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = await cache.TryGetAsync<int>("foo", opt => opt.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);
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

		await cache.SetAsync<int>("foo", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo1 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo4 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", token: TestContext.Current.CancellationToken);
		var maybeFoo5 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		await cache.GetOrSetAsync<int>("bar", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeBar = await cache.TryGetAsync<int>("bar", token: TestContext.Current.CancellationToken);

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

		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);

		var maybeFoo1 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		await cache.RemoveAsync("foo", token: TestContext.Current.CancellationToken);

		var maybeBar1 = await cache.TryGetAsync<int>("bar", token: TestContext.Current.CancellationToken);

		await cache.ExpireAsync("qux", token: TestContext.Current.CancellationToken);

		var qux1 = await cache.GetOrSetAsync("qux", async _ => 1, token: TestContext.Current.CancellationToken);
		var qux2 = await cache.GetOrSetAsync("qux", async _ => 2, token: TestContext.Current.CancellationToken);
		var qux3 = await cache.GetOrSetAsync("qux", async _ => 3, token: TestContext.Current.CancellationToken);
		var qux4 = await cache.GetOrDefaultAsync("qux", 4, token: TestContext.Current.CancellationToken);

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
			_ = await cache.GetOrSetAsync<int>("qux", async _ => throw new UnreachableException("Sloths"), token: TestContext.Current.CancellationToken);
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

		await fusionCache.SetAsync<int>("foo", 21, token: TestContext.Current.CancellationToken);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
				await fusionCache.GetOrSetAsync<int>("foo", async _ => throw new Exception(exceptionMessage), token: TestContext.Current.CancellationToken);
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

		await cache.SetAsync("foo", foo, token: TestContext.Current.CancellationToken);

		foo.PropInt = 0;

		var foo0 = (await cache.GetOrDefaultAsync<ComplexType>("foo", token: TestContext.Current.CancellationToken))!;

		var foo1 = (await cache.GetOrDefaultAsync<ComplexType>("foo", token: TestContext.Current.CancellationToken))!;
		foo1.PropInt = 1;

		var foo2 = (await cache.GetOrDefaultAsync<ComplexType>("foo", token: TestContext.Current.CancellationToken))!;
		foo2.PropInt = 2;

		var foo3 = (await cache.GetOrDefaultAsync<ComplexType>("foo", token: TestContext.Current.CancellationToken))!;
		foo3.PropInt = 3;

		Assert.Equal(0, foo.PropInt);

		Assert.NotNull(foo0);
		Assert.NotSame(foo, foo0);
		Assert.Equal(-1, foo0.PropInt);

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

		await cache.SetAsync("imm", imm, token: TestContext.Current.CancellationToken);

		var imm1 = (await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm", token: TestContext.Current.CancellationToken))!;
		var imm2 = (await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm", token: TestContext.Current.CancellationToken))!;

		Assert.Same(imm, imm1);
		Assert.Same(imm, imm2);
	}

	[Fact]
	public async Task CanRemoveByTagAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, logger: logger);

		await cache.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("bar", 2, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		await cache.GetOrSetAsync<int>("baz", async _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		var foo1 = await cache.GetOrSetAsync<int>("foo", async _ => 11, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar1 = await cache.GetOrSetAsync<int>("bar", async _ => 22, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz1 = await cache.GetOrSetAsync<int>("baz", async (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		}, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		await cache.RemoveByTagAsync("x", token: TestContext.Current.CancellationToken);

		var foo2 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = await cache.GetOrSetAsync<int>("bar", async _ => 222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz2 = await cache.GetOrSetAsync<int>("baz", async _ => 333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		await cache.RemoveByTagAsync("y", token: TestContext.Current.CancellationToken);

		var foo3 = await cache.GetOrSetAsync<int>("foo", async _ => 1111, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar3 = await cache.GetOrSetAsync<int>("bar", async _ => 2222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz3 = await cache.GetOrSetAsync<int>("baz", async _ => 3333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Fact]
	public async Task CanRemoveByTagMultiAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, logger: logger);

		await cache.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("bar", 2, tags: ["y"], token: TestContext.Current.CancellationToken);
		await cache.GetOrSetAsync<int>("baz", async _ => 3, tags: ["z"], token: TestContext.Current.CancellationToken);

		var foo1 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1 = await cache.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz1 = await cache.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		await cache.RemoveByTagAsync(["x", "z"], token: TestContext.Current.CancellationToken);

		var foo2 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = await cache.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz2 = await cache.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(0, baz2);

		await cache.RemoveByTagAsync((string[])null!, token: TestContext.Current.CancellationToken);
		await cache.RemoveByTagAsync([], token: TestContext.Current.CancellationToken);

		var foo4 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar4 = await cache.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz4 = await cache.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo4);
		Assert.Equal(2, bar4);
		Assert.Equal(0, baz4);

		await cache.RemoveByTagAsync(["y", "non-existing"], token: TestContext.Current.CancellationToken);

		var foo5 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar5 = await cache.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var baz5 = await cache.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo5);
		Assert.Equal(0, bar5);
		Assert.Equal(0, baz5);
	}

	[Fact]
	public async Task CanRemoveByTagWithChangingTagsAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, logger: logger);

		var foo1 = await cache.GetOrSetAsync("foo", async _ => 1, tags: ["t1", "t2"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);

		// THIS SHOULD REMOVE foo, SINCE IT HAD THE TAG t1
		await cache.RemoveByTagAsync("t1", token: TestContext.Current.CancellationToken);

		// THIS SHOULD ADD TAG t3
		var foo2 = await cache.GetOrSetAsync("foo", async _ => 2, tags: ["t1", "t2", "t3"], token: TestContext.Current.CancellationToken);

		Assert.Equal(2, foo2);

		// THIS SHOULD REMOVE foo, SINCE IT HAD THE TAG t3
		await cache.RemoveByTagAsync("t3", token: TestContext.Current.CancellationToken);

		// THIS SHOULD SET THE TAGS TO ONLY t1
		var foo3 = await cache.GetOrSetAsync("foo", async _ => 3, tags: ["t1"], token: TestContext.Current.CancellationToken);

		Assert.Equal(3, foo3);

		// THIS SHOULD -NOT- REMOVE foo, SINCE IT ONLY HAD THE TAG t1
		await cache.RemoveByTagAsync("t2", token: TestContext.Current.CancellationToken);

		// THIS SHOULD NOT SET ANYTHING
		var foo4 = await cache.GetOrSetAsync("foo", async _ => 4, token: TestContext.Current.CancellationToken);

		Assert.Equal(3, foo4);

		// THIS SHOULD REMOVE foo, SINCE IT HAD (ONLY) THE TAG t1
		await cache.RemoveByTagAsync(["t1", "t2", "t3", "t4"], token: TestContext.Current.CancellationToken);

		// THIS SHOULD SET THE TAGS TO NOTHING
		var foo5 = await cache.GetOrSetAsync("foo", async _ => 5, token: TestContext.Current.CancellationToken);

		Assert.Equal(5, foo5);

		// THIS SHOULD -NOT- REMOVE foo, SINCE IT NOW HAS NO TAGS
		await cache.RemoveByTagAsync(["t1", "t2", "t3", "t4"], token: TestContext.Current.CancellationToken);

		// THIS SHOULD GET THE EXISTING foo, SINCE WE DID NOT REMOVE IT
		var maybeFooo = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		Assert.True(maybeFooo.HasValue);
		Assert.Equal(5, maybeFooo.Value);
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

		await cacheA.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cacheA.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cacheA.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		await cacheB.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cacheB.SetAsync<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		await cacheB.SetAsync<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		// BOTH CACHES HAVE 3 ITEMS
		Assert.Equal(3, mcA.Count);
		Assert.Equal(3, mcB?.Count);

		var fooA1 = await cacheA.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var barA1 = await cacheA.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var bazA1 = await cacheA.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		var fooB1 = await cacheB.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var barB1 = await cacheB.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var bazB1 = await cacheB.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		await cacheA.ClearAsync(false, token: TestContext.Current.CancellationToken);
		await cacheB.ClearAsync(false, token: TestContext.Current.CancellationToken);

		// CACHE A HAS 5 ITEMS (3 FOR ITEMS + 1 FOR THE * TAG + 1 FOR THE ** TAG)
		Assert.Equal(5, mcA.Count);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);

		await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

		var fooA2 = await cacheA.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var barA2 = await cacheA.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var bazA2 = await cacheA.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

		var fooB2 = await cacheB.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var barB2 = await cacheB.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);
		var bazB2 = await cacheB.GetOrDefaultAsync<int>("baz", token: TestContext.Current.CancellationToken);

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

		await cache.SetAsync<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		var foo1 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);

		await cache.ClearAsync(token: TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

		var foo2 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2);

		await cache.ClearAsync(false, token: TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

		var foo3 = await cache.GetOrDefaultAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo3);
	}

	[Fact]
	public async Task CanSkipMemoryCacheReadWriteAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		await cache.SetAsync<int>("foo", 42, opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		var maybeFoo1 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo2 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		var maybeFoo4 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo5 = await cache.TryGetAsync<int>("foo", opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo6 = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		await cache.GetOrSetAsync<int>("bar", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeBar = await cache.TryGetAsync<int>("bar", token: TestContext.Current.CancellationToken);

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
		var value1 = await cache.GetOrSetAsync<int?>("foo", async _ => 42, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.True(value1.HasValue);
		Assert.Equal(42, value1.Value);

		await Task.Delay(1_100, TestContext.Current.CancellationToken);

		var value2 = await cache.GetOrSetAsync<int?>("foo", async (ctx, _) => { await Task.Delay(1_000); return ctx.Fail("Some error"); }, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.True(value2.HasValue);
		Assert.Equal(42, value2.Value);

		await Task.Delay(1_100, TestContext.Current.CancellationToken);

		var value3 = await cache.GetOrDefaultAsync<int?>("foo", options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
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
			await cache.SetAsync<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.GetOrSetAsync<int>("bar", async _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);
		});

		var foo1 = await cache.GetOrDefaultAsync<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1 = await cache.GetOrDefaultAsync<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo1);
		Assert.Equal(0, bar1);

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.RemoveByTagAsync("x", token: TestContext.Current.CancellationToken);
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.ClearAsync(false, token: TestContext.Current.CancellationToken);
		});

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await cache.ClearAsync(token: TestContext.Current.CancellationToken);
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
		var v1 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(v1, v2);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN
		var expectedValue = DateTimeOffset.UtcNow.Ticks;
		var v3 = await cache.GetOrSetAsync<long>("foo", async _ => expectedValue, tags: ["c", "d"], token: TestContext.Current.CancellationToken);

		Assert.Equal(v2, v3);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrSetAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(expectedValue, v4);
		Assert.True(v4 > v3);

		await cache.RemoveByTagAsync("c", token: TestContext.Current.CancellationToken);

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrDefaultAsync<long>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, v5);
	}

	[Fact]
	public async Task JitteringIsNotUsedWhenActivatingFailSafeAsync()
	{
		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.DefaultEntryOptions
			.SetDuration(TimeSpan.FromMinutes(180))
			.SetJittering(TimeSpan.FromMinutes(30))
			.SetFailSafe(true, throttleDuration: TimeSpan.Zero);

		var expectedNegOne = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => ctx.Fail("test"), failSafeDefaultValue: -1
, token: TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

		var expectedOne = await cache.GetOrSetAsync<int>("foo", async (ctx, _) => ctx.Modified(1), failSafeDefaultValue: -1
, token: TestContext.Current.CancellationToken);

		Assert.Equal(-1, expectedNegOne);
		Assert.Equal(1, expectedOne);
	}

	[Fact]
	public async Task CanAccessCacheKeyInsideFactoryAsync()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// WITH PREFIX
		var options1 = new FusionCacheOptions();
		options1.CacheKeyPrefix = "MyPrefix:";
		using var cache1 = new FusionCache(options1, logger: logger);

		string? key1 = null;
		string? originalKey1 = null;
		await cache1.GetOrSetAsync<int>(
			"foo",
			async (ctx, _) =>
			{
				key1 = ctx.Key;
				originalKey1 = ctx.OriginalKey;

				return 42;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal("MyPrefix:foo", key1);
		Assert.Equal("foo", originalKey1);

		// WITHOUT PREFIX
		var options2 = new FusionCacheOptions();
		using var cache2 = new FusionCache(options2, logger: logger);

		string? key2 = null;
		string? originalKey2 = null;
		await cache2.GetOrSetAsync<int>(
			"foo",
			async (ctx, _) =>
			{
				key2 = ctx.Key;
				originalKey2 = ctx.OriginalKey;

				return 42;
			},
			token: TestContext.Current.CancellationToken
		);

		Assert.Equal("foo", key2);
		Assert.Equal("foo", originalKey2);
	}
}
