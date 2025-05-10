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
	public void CanRemove()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var foo1 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Remove("foo", token: TestContext.Current.CancellationToken);
		var foo2 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo1);
		Assert.Equal(0, foo2);
	}

	[Fact]
	public void ReturnsStaleDataWhenFactoryFails()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var cache = new FusionCache(options);
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(500);
		var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
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

		var initialValue = cache.GetOrSet<int>("foo", _ => 42, token: TestContext.Current.CancellationToken);

		Thread.Sleep(500);

		var newValue = cache.GetOrSet<int>("foo", (ctx, _) => ctx.Fail(errorMessage), token: TestContext.Current.CancellationToken);

		Assert.Equal(initialValue, newValue);

		Thread.Sleep(throttleDuration.PlusALittleBit());

		Exception? exc = null;
		try
		{
			_ = cache.GetOrSet<int>("foo", (ctx, _) => ctx.Fail(errorMessage), opt => opt.SetFailSafe(false)
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
	public void ThrowsWhenFactoryThrowsWithoutFailSafe()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_100);
		Assert.ThrowsAny<Exception>(() =>
		{
			var newValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false), token: TestContext.Current.CancellationToken);
			logger.LogInformation("NEW VALUE: {NewValue}", newValue);
		});
	}

	[Fact]
	public void ThrowsOnFactoryHardTimeoutWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		Assert.Throws<SyntheticTimeoutException>(() =>
		{
			var value = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(2_000, 100), token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public void ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_100);
		var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void DoesNotSoftTimeoutWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, initialValue);
	}

	[Fact]
	public void DoesHardTimeoutEvenWithoutStaleData()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		Assert.ThrowsAny<Exception>(() =>
		{
			var initialValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500), token: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public void ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_100);
		var newValue = cache.GetOrSet<int>("foo", _ => { Thread.Sleep(1_000); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100, 500), token: TestContext.Current.CancellationToken);
		Assert.Equal(42, newValue);
	}

	[Fact]
	public void SetOverwritesAnExistingValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		var newValue = 21;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		cache.Set<int>("foo", newValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		var actualValue = cache.GetOrDefault<int>("foo", -1, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), TestContext.Current.CancellationToken);
		Assert.Equal(newValue, actualValue);
	}

	[Fact]
	public void GetOrSetDoesNotOverwriteANonExpiredValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void DoesNotReturnStaleDataIfFactorySucceeds()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		var newValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public void GetOrDefaultDoesReturnStaleDataWithFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)) { IsFailSafeEnabled = true }, token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void GetOrDefaultDoesNotReturnStaleWithoutFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = 42;
		cache.Set<int>("foo", initialValue, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		var newValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(false), token: TestContext.Current.CancellationToken);
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public void FactoryTimedOutButSuccessfulDoesUpdateCachedValue()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromMinutes(1)), token: TestContext.Current.CancellationToken);
		var initialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		var middleValue = cache.GetOrSet<int>("foo", ct => { Thread.Sleep(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(500), token: TestContext.Current.CancellationToken);
		var interstitialValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		Thread.Sleep(3_000);
		var finalValue = cache.GetOrDefault<int>("foo", options: new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.Equal(42, initialValue);
		Assert.Equal(42, middleValue);
		Assert.Equal(42, interstitialValue);
		Assert.Equal(21, finalValue);
	}

	[Fact]
	public void TryGetReturnsCorrectly()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		var res1 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var res2 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.False(res1.HasValue);
		Assert.Throws<InvalidOperationException>(() =>
		{
			var foo = res1.Value;
		});
		Assert.True(res2.HasValue);
		Assert.Equal(42, res2.Value);
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
	public void HandlesFlexibleSimpleTypeConversions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)42;
		cache.Set("foo", initialValue, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var newValue = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void HandlesFlexibleComplexTypeConversions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = (object)ComplexType.CreateSample();
		cache.Set("foo", initialValue, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var newValue = cache.GetOrDefault<ComplexType>("foo", token: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public void GetOrDefaultDoesNotSet()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = cache.GetOrDefault<int>("foo", 42, opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		var bar = cache.GetOrDefault<int>("foo", 21, opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		var baz = cache.TryGet<int>("foo", opt => opt.SetDuration(TimeSpan.FromHours(24)), token: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo);
		Assert.Equal(21, bar);
		Assert.False(baz.HasValue);
	}

	[Fact]
	public void GetOrSetWithDefaultValueWorks()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var foo = 42;
		cache.GetOrSet<int>("foo", foo, TimeSpan.FromHours(24), token: TestContext.Current.CancellationToken);
		var bar = cache.GetOrDefault<int>("foo", 21, token: TestContext.Current.CancellationToken);
		Assert.Equal(foo, bar);
	}

	[Fact]
	public void ThrottleDurationWorksCorrectly()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var duration = TimeSpan.FromSeconds(1);
		var throttleDuration = TimeSpan.FromSeconds(2);

		// SET THE VALUE (WITH FAIL-SAFE ENABLED)
		cache.Set("foo", 42, opt => opt.SetDuration(duration).SetFailSafe(true, throttleDuration: throttleDuration), token: TestContext.Current.CancellationToken);
		// LET IT EXPIRE
		Thread.Sleep(duration.PlusALittleBit());
		// CHECK EXPIRED (WITHOUT FAIL-SAFE)
		var nope = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		// DO NOT ACTIVATE FAIL-SAFE AND THROTTLE DURATION
		var default1 = cache.GetOrDefault("foo", 1, token: TestContext.Current.CancellationToken);
		// ACTIVATE FAIL-SAFE AND RE-STORE THE VALUE WITH THROTTLE DURATION
		var throttled1 = cache.GetOrDefault("foo", 1, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		// WAIT A LITTLE BIT (LESS THAN THE DURATION)
		Thread.Sleep(100);
		// GET THE THROTTLED (NON EXPIRED) VALUE
		var throttled2 = cache.GetOrDefault("foo", 2, opt => opt.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		// LET THE THROTTLE DURATION PASS
		Thread.Sleep(throttleDuration);
		// FALLBACK TO THE DEFAULT VALUE
		var default3 = cache.GetOrDefault("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.False(nope.HasValue);
		Assert.Equal(1, default1);
		Assert.Equal(42, throttled1);
		Assert.Equal(42, throttled2);
		Assert.Equal(3, default3);
	}

	[Fact]
	public void AdaptiveCaching()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;
		FusionCacheEntryOptions? innerOpt = null;

		var default3 = cache.GetOrSet<int>("foo", (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(1);

				innerOpt = ctx.Options;

				return 3;
			}, opt => opt.SetFailSafe(false)
, token: TestContext.Current.CancellationToken);

		Thread.Sleep(TimeSpan.FromSeconds(2));

		var maybeValue = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(innerOpt!.Duration, TimeSpan.FromSeconds(1));
		Assert.False(maybeValue.HasValue);
	}

	[Fact]
	public void AdaptiveCachingWithBackgroundFactoryCompletion()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var dur = TimeSpan.FromMinutes(5);
		cache.DefaultEntryOptions.Duration = dur;

		// SET WITH 1s DURATION + FAIL-SAFE
		cache.Set("foo", 21, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET IT BECOME STALE
		Thread.Sleep(TimeSpan.FromSeconds(2));

		// CALL GetOrSET WITH A 1s SOFT TIMEOUT AND A FACTORY RUNNING FOR AT LEAST 3s
		var value21 = cache.GetOrSet<int>("foo", (ctx, _) =>
			{
				// WAIT 3s
				Thread.Sleep(TimeSpan.FromSeconds(3));

				// CHANGE THE OPTIONS (SET THE DURATION TO 5s AND DISABLE FAIL-SAFE
				ctx.Options.SetDuration(TimeSpan.FromSeconds(5)).SetFailSafe(false);

				return 42;
			}, opt => opt.SetFactoryTimeouts(TimeSpan.FromSeconds(1)).SetFailSafe(true)
, token: TestContext.Current.CancellationToken);

		// WAIT FOR 3s (+ EXTRA 1s) SO THE FACTORY COMPLETES IN THE BACKGROUND
		Thread.Sleep(TimeSpan.FromSeconds(3 + 1));

		// GET THE VALUE THAT HAS BEEN SET BY THE BACKGROUND COMPLETION OF THE FACTORY
		var value42 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(false), token: TestContext.Current.CancellationToken);

		// LET THE CACHE ENTRY EXPIRES
		Thread.Sleep(TimeSpan.FromSeconds(5));

		// SEE THAT FAIL-SAFE CANNOT BE ACTIVATED (BECAUSE IT WAS DISABLED IN THE FACTORY)
		var noValue = cache.TryGet<int>("foo", options => options.SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(dur, TimeSpan.FromMinutes(5));
		Assert.Equal(cache.DefaultEntryOptions.Duration, TimeSpan.FromMinutes(5));
		Assert.Equal(21, value21);
		Assert.Equal(42, value42);
		Assert.False(noValue.HasValue);
	}

	[Fact]
	public void AdaptiveCachingDoesNotChangeOptions()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var options = new FusionCacheEntryOptions(TimeSpan.FromSeconds(10));

		_ = cache.GetOrSet<int>("foo", (ctx, _) =>
			{
				ctx.Options.Duration = TimeSpan.FromSeconds(20);
				return 42;
			}, options
, token: TestContext.Current.CancellationToken);

		Assert.Equal(options.Duration, TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AdaptiveCachingCanWorkWithSkipMemoryCache()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		cache.DefaultEntryOptions.FailSafeThrottleDuration = TimeSpan.FromSeconds(3);

		var foo1 = cache.GetOrSet<int>("foo", _ => 1, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);

		Thread.Sleep(TimeSpan.FromSeconds(1).PlusALittleBit());

		var foo2 = cache.GetOrSet<int>("foo", (ctx, _) =>
		{
			ctx.Options.SkipMemoryCacheRead = true;
			ctx.Options.SkipMemoryCacheWrite = true;

			return 2;
		}, token: TestContext.Current.CancellationToken);


		Assert.Equal(2, foo2);

		var foo3 = cache.TryGet<int>("foo", options => options.SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);

		Assert.True(foo3.HasValue);
		Assert.Equal(1, foo3.Value);

		Thread.Sleep(cache.DefaultEntryOptions.FailSafeThrottleDuration.PlusALittleBit());

		var foo4 = cache.GetOrSet<int>("foo", _ => 4, token: TestContext.Current.CancellationToken);

		Assert.Equal(4, foo4);
	}

	[Fact]
	public void AdaptiveCachingCanWorkOnException()
	{
		var options = new FusionCacheOptions();

		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(5);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;

		var cache = new FusionCache(options);
		var key = "foo";

		cache.GetOrSet(key, "bar", token: TestContext.Current.CancellationToken);

		// LOGICALLY EXPIRE THE KEY SO THE FAIL-SAFE LOGIC TRIGGERS
		cache.Expire(key, token: TestContext.Current.CancellationToken);

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
			}, token: TestContext.Current.CancellationToken);
		});

		cache.GetOrSet<string>(key, (ctx, ct) =>
		{
			throw new Exception("Factory failed");
		}, token: TestContext.Current.CancellationToken);
	}

	[Fact]
	public void FailSafeMaxDurationNormalizationOccurs()
	{
		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		using var fusionCache = new FusionCache(new FusionCacheOptions());
		fusionCache.Set<int>("foo", 21, opt => opt.SetDuration(duration).SetFailSafe(true, maxDuration), token: TestContext.Current.CancellationToken);
		Thread.Sleep(maxDuration.PlusALittleBit());
		var value = fusionCache.GetOrDefault<int>("foo", opt => opt.SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Fact]
	public void ReturnsStaleDataWithoutSavingItWhenNoFactory()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var initialValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)), token: TestContext.Current.CancellationToken);
		Thread.Sleep(1_500);
		var maybeValue = cache.TryGet<int>("foo", opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var defaultValue1 = cache.GetOrDefault<int>("foo", 1, token: TestContext.Current.CancellationToken);
		var defaultValue2 = cache.GetOrDefault<int>("foo", 2, opt => opt.SetDuration(TimeSpan.FromSeconds(1)).SetAllowStaleOnReadOnly(), token: TestContext.Current.CancellationToken);
		var defaultValue3 = cache.GetOrDefault<int>("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.True(maybeValue.HasValue);
		Assert.Equal(42, maybeValue.Value);
		Assert.Equal(1, defaultValue1);
		Assert.Equal(42, defaultValue2);
		Assert.Equal(3, defaultValue3);
	}

	[Fact]
	public void CanHandleInfiniteOrSimilarDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.Set<int>("foo", 42, opt => opt.SetDuration(TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1)).SetJittering(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo = cache.GetOrDefault<int>("foo", 0, token: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo);
	}

	[Fact]
	public void CanHandleZeroDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 10, opt => opt.SetDuration(TimeSpan.Zero), token: TestContext.Current.CancellationToken);
		var foo1 = cache.GetOrDefault<int>("foo", 1, token: TestContext.Current.CancellationToken);

		cache.Set<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo2 = cache.GetOrDefault<int>("foo", 2, token: TestContext.Current.CancellationToken);

		cache.Set<int>("foo", 30, opt => opt.SetDuration(TimeSpan.Zero), token: TestContext.Current.CancellationToken);
		var foo3 = cache.GetOrDefault<int>("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public void CanHandleNegativeDurations()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 10, opt => opt.SetDuration(TimeSpan.FromSeconds(-100)), token: TestContext.Current.CancellationToken);
		var foo1 = cache.GetOrDefault<int>("foo", 1, token: TestContext.Current.CancellationToken);

		cache.Set<int>("foo", 20, opt => opt.SetDuration(TimeSpan.FromMinutes(10)), token: TestContext.Current.CancellationToken);
		var foo2 = cache.GetOrDefault<int>("foo", 2, token: TestContext.Current.CancellationToken);

		cache.Set<int>("foo", 30, opt => opt.SetDuration(TimeSpan.FromDays(-100)), token: TestContext.Current.CancellationToken);
		var foo3 = cache.GetOrDefault<int>("foo", 3, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public void CanConditionalRefresh()
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
		var v1 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// CACHED -> NO INCR
		var v2 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v3 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / NOT MOD RESP + 1
		var v4 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// SET VALUE -> CHANGE LAST MODIFIED
		endpoint.SetValue(42);

		// LET THE CACHE EXPIRE
		Thread.Sleep(duration.PlusALittleBit());

		// TOT REQ + 1 / COND REQ + 1 / FULL RESP + 1
		var v5 = cache.GetOrSet<int>("foo", (ctx, _) => FakeGet(ctx, endpoint), opt => opt.SetDuration(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

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
	public void CanEagerRefresh()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var eagerRefreshValue = DateTimeOffset.UtcNow.Ticks;
		logger.LogInformation("EAGER REFRESH VALUE: {0}", eagerRefreshValue);
		var v3 = cache.GetOrSet<long>("foo", _ => eagerRefreshValue, token: TestContext.Current.CancellationToken);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v6 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(v1, v2);
		Assert.Equal(v2, v3);
		Assert.True(v4 > v3);
		Assert.Equal(eagerRefreshValue, v4);
		Assert.True(v5 > v4);
		Assert.Equal(v5, v6);
	}

	[Fact]
	public void CanEagerRefreshWithInfiniteDuration()
	{
		var duration = TimeSpan.MaxValue;
		var eagerRefreshThreshold = 0.5f;

		using var cache = new FusionCache(new FusionCacheOptions());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.True(v1 > 0);
	}

	[Fact]
	public void CanEagerRefreshNoCancellation()
	{
		var duration = TimeSpan.FromSeconds(2);
		var lockTimeout = TimeSpan.FromSeconds(10);
		var eagerRefreshThreshold = 0.1f;
		var eagerRefreshDelay = TimeSpan.FromSeconds(5);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;

		// EXECUTE FACTORY
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

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
		var v4 = cache.GetOrSet<long>("foo", _ => v4SupposedlyNot, options =>
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
		}, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		Thread.Sleep(eagerRefreshThresholdDuration.Add(TimeSpan.FromMilliseconds(10)));

		// EAGER REFRESH KICKS IN (WITH DELAY)
		var v3 = cache.GetOrSet<long>("foo", _ =>
		{
			Thread.Sleep(simulatedDelay);

			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		Thread.Sleep(duration.PlusALittleBit());

		// TRY TO GET EXPIRED ENTRY: NORMALLY THIS WOULD FIRE THE FACTORY, BUT SINCE IT
		// IS ALRADY RUNNING BECAUSE OF EAGER REFRESH, IT WILL WAIT FOR IT TO COMPLETE
		// AND USE THE RESULT, SAVING ONE FACTORY EXECUTION
		var v4 = cache.GetOrSet<long>("foo", _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v5 = cache.GetOrSet<long>("foo", _ =>
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
	public void CanExpire()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);

		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo1 = cache.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly(false), token: TestContext.Current.CancellationToken);
		cache.Expire("foo", token: TestContext.Current.CancellationToken);
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly(false), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = cache.TryGet<int>("foo", opt => opt.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);
		Assert.True(maybeFoo1.HasValue);
		Assert.Equal(42, maybeFoo1.Value);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.Equal(42, maybeFoo3.Value);
	}

	[Fact]
	public void CanSkipMemoryCache()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo1 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Remove("foo", opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeFoo4 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Remove("foo", token: TestContext.Current.CancellationToken);
		var maybeFoo5 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);

		cache.GetOrSet<int>("bar", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeBar = cache.TryGet<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.True(maybeFoo4.HasValue);
		Assert.False(maybeFoo5.HasValue);

		Assert.False(maybeBar.HasValue);
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

		fusionCache.Set<int>("foo", 21, token: TestContext.Current.CancellationToken);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				Thread.Sleep(throttleDuration.PlusALittleBit());
				fusionCache.GetOrSet<int>("foo", _ => throw new Exception(exceptionMessage), token: TestContext.Current.CancellationToken);
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

		cache.Set("foo", foo, token: TestContext.Current.CancellationToken);

		foo.PropInt = 0;

		var foo0 = cache.GetOrDefault<ComplexType>("foo", token: TestContext.Current.CancellationToken);

		var foo1 = cache.GetOrDefault<ComplexType>("foo", token: TestContext.Current.CancellationToken)!;
		foo1.PropInt = 1;

		var foo2 = cache.GetOrDefault<ComplexType>("foo", token: TestContext.Current.CancellationToken)!;
		foo2.PropInt = 2;

		var foo3 = cache.GetOrDefault<ComplexType>("foo", token: TestContext.Current.CancellationToken)!;
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

		cache.Set("imm", imm, token: TestContext.Current.CancellationToken);

		var imm1 = cache.GetOrDefault<SimpleImmutableObject>("imm", token: TestContext.Current.CancellationToken)!;
		var imm2 = cache.GetOrDefault<SimpleImmutableObject>("imm", token: TestContext.Current.CancellationToken)!;

		Assert.Same(imm, imm1);
		Assert.Same(imm, imm2);
	}

	[Fact]
	public void CanRemoveByTag()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.Set<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		cache.Set<int>("bar", 2, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		cache.GetOrSet<int>("baz", _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		var foo1 = cache.GetOrSet<int>("foo", _ => 11, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar1 = cache.GetOrSet<int>("bar", _ => 22, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz1 = cache.GetOrSet<int>("baz", (ctx, _) =>
		{
			ctx.Tags = ["x", "z"];
			return 33;
		}, token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		cache.RemoveByTag("x", token: TestContext.Current.CancellationToken);

		var foo2 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = cache.GetOrSet<int>("bar", _ => 222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz2 = cache.GetOrSet<int>("baz", _ => 333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		cache.RemoveByTag("y", token: TestContext.Current.CancellationToken);

		var foo3 = cache.GetOrSet<int>("foo", _ => 1111, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		var bar3 = cache.GetOrSet<int>("bar", _ => 2222, tags: ["y", "z"], token: TestContext.Current.CancellationToken);
		var baz3 = cache.GetOrSet<int>("baz", _ => 3333, tags: ["x", "z"], token: TestContext.Current.CancellationToken);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
	}

	[Fact]
	public void CanRemoveByTagMulti()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, logger: logger);

		cache.Set<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		cache.Set<int>("bar", 2, tags: ["y"], token: TestContext.Current.CancellationToken);
		cache.GetOrSet<int>("baz", _ => 3, tags: ["z"], token: TestContext.Current.CancellationToken);

		var foo1 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1 = cache.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz1 = cache.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		cache.RemoveByTag(["x", "z"], token: TestContext.Current.CancellationToken);

		var foo2 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar2 = cache.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz2 = cache.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(0, baz2);

		cache.RemoveByTag((string[])null!, token: TestContext.Current.CancellationToken);
		cache.RemoveByTag([], token: TestContext.Current.CancellationToken);

		var foo4 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar4 = cache.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz4 = cache.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo4);
		Assert.Equal(2, bar4);
		Assert.Equal(0, baz4);

		cache.RemoveByTag(["y", "non-existing"], token: TestContext.Current.CancellationToken);

		var foo5 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar5 = cache.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var baz5 = cache.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo5);
		Assert.Equal(0, bar5);
		Assert.Equal(0, baz5);
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

		cacheA.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		cacheA.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		cacheA.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		cacheB.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		cacheB.Set<int>("bar", 2, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);
		cacheB.Set<int>("baz", 3, options => options.SetDuration(TimeSpan.FromSeconds(10)), token: TestContext.Current.CancellationToken);

		// BOTH CACHES HAVE 3 ITEMS
		Assert.Equal(3, mcA.Count);
		Assert.Equal(3, mcB?.Count);

		var fooA1 = cacheA.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var barA1 = cacheA.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var bazA1 = cacheA.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		var fooB1 = cacheB.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var barB1 = cacheB.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var bazB1 = cacheB.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		cacheA.Clear(false, token: TestContext.Current.CancellationToken);
		cacheB.Clear(false, token: TestContext.Current.CancellationToken);

		// CACHE A HAS 5 ITEMS (3 FOR ITEMS + 1 FOR THE * TAG + 1 FOR THE ** TAG)
		Assert.Equal(5, mcA.Count);

		// CACHE B HAS 0 ITEMS (BECAUSE A RAW CLEAR HAS BEEN EXECUTED)
		Assert.Equal(0, mcB?.Count);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var fooA2 = cacheA.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var barA2 = cacheA.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var bazA2 = cacheA.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

		var fooB2 = cacheB.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var barB2 = cacheB.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);
		var bazB2 = cacheB.GetOrDefault<int>("baz", token: TestContext.Current.CancellationToken);

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
	public void CanClearWithFailSafe()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// NOT PASSING A MEMORY CACHE -> CAN EXECUTE RAW CLEAR
		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);

		cache.Set<int>("foo", 1, options => options.SetDuration(TimeSpan.FromSeconds(10)).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		var foo1 = cache.GetOrDefault<int>("foo", options => options.SetFailSafe(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);

		cache.Clear(token: TestContext.Current.CancellationToken);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var foo2 = cache.GetOrDefault<int>("foo", options => options.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo2);

		cache.Clear(false, token: TestContext.Current.CancellationToken);

		Thread.Sleep(TimeSpan.FromMilliseconds(100));

		var foo3 = cache.GetOrDefault<int>("foo", options => options.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo3);
	}

	[Fact]
	public void CanSkipMemoryCacheReadWrite()
	{
		using var cache = new FusionCache(new FusionCacheOptions());

		cache.Set<int>("foo", 42, opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		var maybeFoo1 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Set<int>("foo", 42, token: TestContext.Current.CancellationToken);
		var maybeFoo2 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo3 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);
		cache.Remove("foo", opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		var maybeFoo4 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo5 = cache.TryGet<int>("foo", opt => opt.SetSkipMemoryCacheWrite(), token: TestContext.Current.CancellationToken);
		cache.Remove("foo", opt => opt.SetSkipMemoryCacheRead(), token: TestContext.Current.CancellationToken);
		var maybeFoo6 = cache.TryGet<int>("foo", token: TestContext.Current.CancellationToken);

		cache.GetOrSet<int>("bar", 42, opt => opt.SetSkipMemoryCache(), token: TestContext.Current.CancellationToken);
		var maybeBar = cache.TryGet<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.False(maybeFoo1.HasValue);
		Assert.False(maybeFoo2.HasValue);
		Assert.True(maybeFoo3.HasValue);
		Assert.False(maybeFoo4.HasValue);
		Assert.True(maybeFoo5.HasValue);
		Assert.False(maybeFoo6.HasValue);

		Assert.False(maybeBar.HasValue);
	}

	[Fact]
	public void CanSoftFailWithSoftTimeout()
	{
		using var cache = new FusionCache(new FusionCacheOptions());
		var value1 = cache.GetOrSet<int?>("foo", _ => 42, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		Assert.True(value1.HasValue);
		Assert.Equal(42, value1.Value);

		Thread.Sleep(1_100);

		var value2 = cache.GetOrSet<int?>("foo", (ctx, _) => { Thread.Sleep(1_000); return ctx.Fail("Some error"); }, options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.True(value2.HasValue);
		Assert.Equal(42, value2.Value);

		Thread.Sleep(1_100);

		var value3 = cache.GetOrDefault<int?>("foo", options => options.SetDuration(TimeSpan.FromSeconds(1)).SetFailSafe(true).SetFactoryTimeoutsMs(100), token: TestContext.Current.CancellationToken);
		Assert.True(value3.HasValue);
		Assert.Equal(42, value3.Value);
	}

	[Fact]
	public void CanDisableTagging()
	{
		var logger = CreateXUnitLogger<FusionCache>();
		using var cache = new FusionCache(new FusionCacheOptions() { DisableTagging = true }, logger: logger);

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Set<int>("foo", 1, tags: ["x", "y"], token: TestContext.Current.CancellationToken);
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.GetOrSet<int>("bar", _ => 3, tags: ["x", "z"], token: TestContext.Current.CancellationToken);
		});

		var foo1 = cache.GetOrDefault<int>("foo", token: TestContext.Current.CancellationToken);
		var bar1 = cache.GetOrDefault<int>("bar", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo1);
		Assert.Equal(0, bar1);

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.RemoveByTag("x", token: TestContext.Current.CancellationToken);
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Clear(false, token: TestContext.Current.CancellationToken);
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			cache.Clear(token: TestContext.Current.CancellationToken);
		});
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
		var v1 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(v1, v2);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		Thread.Sleep(eagerDuration);

		// EAGER REFRESH KICKS IN
		var expectedValue = DateTimeOffset.UtcNow.Ticks;
		var v3 = cache.GetOrSet<long>("foo", _ => expectedValue, tags: ["c", "d"], token: TestContext.Current.CancellationToken);

		Assert.Equal(v2, v3);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		// GET THE REFRESHED VALUE
		var v4 = cache.GetOrSet<long>("foo", _ => DateTimeOffset.UtcNow.Ticks, token: TestContext.Current.CancellationToken);

		Assert.Equal(expectedValue, v4);
		Assert.True(v4 > v3);

		cache.RemoveByTag("c", token: TestContext.Current.CancellationToken);

		// EXECUTE FACTORY AGAIN
		var v5 = cache.GetOrDefault<long>("foo", token: TestContext.Current.CancellationToken);

		Assert.Equal(0, v5);
	}

	[Fact]
	public void JitteringIsNotUsedWhenActivatingFailSafe()
	{
		using var cache = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		cache.DefaultEntryOptions
			.SetDuration(TimeSpan.FromMinutes(180))
			.SetJittering(TimeSpan.FromMinutes(30))
			.SetFailSafe(true, throttleDuration: TimeSpan.Zero);

		var expectedNegOne = cache.GetOrSet<int>("foo", (ctx, _) => ctx.Fail("test"), failSafeDefaultValue: -1
, token: TestContext.Current.CancellationToken);

		Thread.Sleep(TimeSpan.FromMilliseconds(250));

		var expectedOne = cache.GetOrSet<int>("foo", (ctx, _) => ctx.Modified(1), failSafeDefaultValue: -1
, token: TestContext.Current.CancellationToken);

		Assert.Equal(-1, expectedNegOne);
		Assert.Equal(1, expectedOne);
	}

	[Fact]
	public void CanAccessCacheKeyInsideFactory()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		// WITH PREFIX
		var options1 = new FusionCacheOptions();
		options1.CacheKeyPrefix = "MyPrefix:";
		using var cache1 = new FusionCache(options1, logger: logger);

		string? key1 = null;
		string? originalKey1 = null;
		cache1.GetOrSet<int>(
			"foo",
			(ctx, _) =>
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
		cache2.GetOrSet<int>(
			"foo",
			(ctx, _) =>
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
