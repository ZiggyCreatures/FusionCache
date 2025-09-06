﻿using System.Diagnostics;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Hybrid;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

namespace FusionCacheTests.FusionHybridCacheTests;

public class HybridL1Tests
	: AbstractTests
{
	public HybridL1Tests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	[Fact]
	public async Task CanRemoveAsync()
	{
		using var fc = new FusionCache(new FusionCacheOptions());
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 42, cancellationToken: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		await cache.RemoveAsync("foo", TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo1);
		Assert.Equal(0, foo2);
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryFailsAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task ThrowsWhenFactoryThrowsWithoutFailSafeAsync()
	{
		var options = new FusionCacheOptions();
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public async Task ThrowsOnFactoryHardTimeoutWithoutStaleDataAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(2_000);
		options.DefaultEntryOptions.FactoryHardTimeout = TimeSpan.FromMilliseconds(100);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
		{
			var value = await cache.GetOrCreateAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactorySoftTimeoutWithFailSafeAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotSoftTimeoutWithoutStaleDataAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(21, initialValue);
	}

	[Fact]
	public async Task DoesHardTimeoutEvenWithoutStaleDataAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.FactoryHardTimeout = TimeSpan.FromMilliseconds(500);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		});
	}

	[Fact]
	public async Task ReturnsStaleDataWhenFactoryHitHardTimeoutWithFailSafeAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(100);
		options.DefaultEntryOptions.FactoryHardTimeout = TimeSpan.FromMilliseconds(500);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(1_100, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => { await Task.Delay(1_000); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(42, newValue);
	}

	[Fact]
	public async Task SetOverwritesAnExistingValueAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(10);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = 42;
		var newValue = 21;
		await cache.SetAsync<int>("foo", initialValue, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("foo", newValue, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		var actualValue = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(newValue, actualValue);
	}

	[Fact]
	public async Task GetOrSetDoesNotOverwriteANonExpiredValueAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(10);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => 21, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task DoesNotReturnStaleDataIfFactorySucceedsAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = await cache.GetOrCreateAsync<int>("foo", async _ => 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrCreateAsync<int>("foo", async _ => 21, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		Assert.NotEqual(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesReturnStaleDataWithAllowStaleOnReadOnlyAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.AllowStaleOnReadOnly = true;
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = 42;
		await cache.SetAsync<int>("foo", initialValue, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task FactoryTimedOutButSuccessfulDoesUpdateCachedValueAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromMinutes(1);
		options.DefaultEntryOptions.FactorySoftTimeout = TimeSpan.FromMilliseconds(500);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 42, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(1) }, cancellationToken: TestContext.Current.CancellationToken);
		var initialValue = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		await Task.Delay(1_500, TestContext.Current.CancellationToken);
		var interstitialValue1 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		var middleValue = await cache.GetOrCreateAsync<int>("foo", async ct => { await Task.Delay(2_000); ct.ThrowIfCancellationRequested(); return 21; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(10) }, cancellationToken: TestContext.Current.CancellationToken);
		var interstitialValue2 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		await Task.Delay(3_000, TestContext.Current.CancellationToken);
		var finalValue = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);

		Assert.Equal(42, initialValue);
		Assert.Equal(42, middleValue);
		Assert.Equal(0, interstitialValue1);
		Assert.Equal(42, interstitialValue2);
		Assert.Equal(21, finalValue);
	}

	[Fact]
	public async Task CanCancelAnOperationAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(60);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		int res = -1;
		var sw = Stopwatch.StartNew();
		var outerCancelDelayMs = 200;
		var factoryDelayMs = 5_000;
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			var cts = new CancellationTokenSource(outerCancelDelayMs);
			res = await cache.GetOrCreateAsync<int>("foo", async ct => { await Task.Delay(factoryDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(60) }, cancellationToken: cts.Token);
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
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromHours(24);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = (object)42;
		await cache.SetAsync("foo", initialValue, cancellationToken: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task HandlesFlexibleComplexTypeConversionsAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromHours(24);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var initialValue = (object)ComplexType.CreateSample();
		await cache.SetAsync("foo", initialValue, cancellationToken: TestContext.Current.CancellationToken);
		var newValue = await cache.GetOrDefaultAsync<ComplexType>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(initialValue, newValue);
	}

	[Fact]
	public async Task GetOrDefaultDoesNotSetAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromHours(24);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		var foo = await cache.GetOrDefaultAsync<int>("foo", 21, ct: TestContext.Current.CancellationToken);
		var bar = await cache.GetOrDefaultAsync<int>("foo", 42, ct: TestContext.Current.CancellationToken);
		Assert.Equal(21, foo);
		Assert.Equal(42, bar);
	}

	[Fact]
	public async Task FailSafeMaxDurationNormalizationOccursAsync()
	{
		var duration = TimeSpan.FromSeconds(5);
		var maxDuration = TimeSpan.FromSeconds(1);

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.IsFailSafeEnabled = true;
		options.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 21, new HybridCacheEntryOptions { Expiration = duration }, cancellationToken: TestContext.Current.CancellationToken);
		await Task.Delay(maxDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
		var value = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(21, value);
	}

	[Fact]
	public async Task CanHandleInfiniteOrSimilarDurationsAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1);
		options.DefaultEntryOptions.JitterMaxDuration = TimeSpan.FromMinutes(10);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 42, cancellationToken: TestContext.Current.CancellationToken);
		var foo = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		Assert.Equal(42, foo);
	}

	[Fact]
	public async Task CanHandleZeroDurationsAsync()
	{
		var options = new FusionCacheOptions();
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 10, new HybridCacheEntryOptions { Expiration = TimeSpan.Zero }, cancellationToken: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1, ct: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 20, new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) }, cancellationToken: TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2, ct: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 30, new HybridCacheEntryOptions { Expiration = TimeSpan.Zero }, cancellationToken: TestContext.Current.CancellationToken);
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3, ct: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanHandleNegativeDurationsAsync()
	{
		var options = new FusionCacheOptions();
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 10, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(-100) }, cancellationToken: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", 1, ct: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 20, new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) }, cancellationToken: TestContext.Current.CancellationToken);
		var foo2 = await cache.GetOrDefaultAsync<int>("foo", 2, ct: TestContext.Current.CancellationToken);

		await cache.SetAsync<int>("foo", 30, new HybridCacheEntryOptions { Expiration = TimeSpan.FromDays(-100) }, cancellationToken: TestContext.Current.CancellationToken);
		var foo3 = await cache.GetOrDefaultAsync<int>("foo", 3, ct: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(20, foo2);
		Assert.Equal(3, foo3);
	}

	[Fact]
	public async Task CanHandleEagerRefreshAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var eagerRefreshThreshold = 0.2f;

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN
		var v3 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR THE BACKGROUND FACTORY (EAGER REFRESH) TO COMPLETE
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

		// GET THE REFRESHED VALUE
		var v4 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// EXECUTE FACTORY AGAIN
		var v5 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v6 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

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

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		Assert.True(v1 > 0);
	}

	[Fact]
	public async Task CanHandleEagerRefreshNoCancellationAsync()
	{
		var duration = TimeSpan.FromSeconds(2);
		var lockTimeout = TimeSpan.FromSeconds(10);
		var eagerRefreshThreshold = 0.1f;
		var eagerRefreshDelay = TimeSpan.FromSeconds(5);

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;
		options.DefaultEntryOptions.LockTimeout = lockTimeout;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrCreateAsync<long>("foo", async _ => DateTimeOffset.UtcNow.Ticks, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		var eagerDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * eagerRefreshThreshold).Add(TimeSpan.FromMilliseconds(10));
		await Task.Delay(eagerDuration, TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN
		var eagerRefreshIsStarted = false;
		var eagerRefreshIsEnded = false;
		using var cts = new CancellationTokenSource();
		long v3EagerResult = 0;
		var v3 = await cache.GetOrCreateAsync<long>(
			"foo",
			async ct =>
			{
				eagerRefreshIsStarted = true;

				await Task.Delay(eagerRefreshDelay);

				ct.ThrowIfCancellationRequested();

				eagerRefreshIsEnded = true;

				return v3EagerResult = DateTimeOffset.UtcNow.Ticks;
			},
			cancellationToken: cts.Token
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
		var v4 = await cache.GetOrCreateAsync<long>("foo", async _ => v4SupposedlyNot
, cancellationToken: TestContext.Current.CancellationToken);
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

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = duration;
		options.DefaultEntryOptions.EagerRefreshThreshold = eagerRefreshThreshold;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		// EXECUTE FACTORY
		var v1 = await cache.GetOrCreateAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, cancellationToken: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v2 = await cache.GetOrCreateAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR EAGER REFRESH THRESHOLD TO BE HIT
		await Task.Delay(eagerRefreshThresholdDuration.Add(TimeSpan.FromMilliseconds(10)), TestContext.Current.CancellationToken);

		// EAGER REFRESH KICKS IN (WITH DELAY)
		var v3 = await cache.GetOrCreateAsync<long>("foo", async _ =>
		{
			await Task.Delay(simulatedDelay);

			Interlocked.Increment(ref value);
			return value;
		}, cancellationToken: TestContext.Current.CancellationToken);

		// WAIT FOR EXPIRATION
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// TRY TO GET EXPIRED ENTRY: NORMALLY THIS WOULD FIRE THE FACTORY, BUT SINCE IT
		// IS ALRADY RUNNING BECAUSE OF EAGER REFRESH, IT WILL WAIT FOR IT TO COMPLETE
		// AND USE THE RESULT, SAVING ONE FACTORY EXECUTION
		var v4 = await cache.GetOrCreateAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, cancellationToken: TestContext.Current.CancellationToken);

		// USE CACHED VALUE
		var v5 = await cache.GetOrCreateAsync<long>("foo", async _ =>
		{
			Interlocked.Increment(ref value);
			return value;
		}, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(1, v1);
		Assert.Equal(1, v2);
		Assert.Equal(1, v3);
		Assert.Equal(2, v4);
		Assert.Equal(2, v5);
		Assert.Equal(2, value);
	}

	[Fact]
	public async Task CanSkipMemoryCacheAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromHours(24);
		using var fc = new FusionCache(options);
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 42, new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache }, cancellationToken: TestContext.Current.CancellationToken);
		var foo1 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);

		await cache.GetOrCreateAsync<int>("bar", async _ => 42, new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache }, cancellationToken: TestContext.Current.CancellationToken);
		var bar1 = await cache.GetOrDefaultAsync<int>("bar", ct: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo1);
		Assert.Equal(0, bar1);
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
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 21, cancellationToken: TestContext.Current.CancellationToken);
		TestOutput.WriteLine($"-- SET AT {DateTime.UtcNow}, THEO PHY EXP AT {DateTime.UtcNow + maxDuration}");

		var didThrow = false;
		var sw = Stopwatch.StartNew();

		try
		{
			do
			{
				await Task.Delay(throttleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);
				await cache.GetOrCreateAsync<int>("foo", async _ => throw new Exception(exceptionMessage), cancellationToken: TestContext.Current.CancellationToken);
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
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupSerializer(TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var foo = new ComplexType()
		{
			PropInt = -1
		};

		await cache.SetAsync("foo", foo, cancellationToken: TestContext.Current.CancellationToken);

		var foo1 = await cache.GetOrDefaultAsync<ComplexType>("foo", ct: TestContext.Current.CancellationToken);
		foo1.PropInt = 1;

		var foo2 = await cache.GetOrDefaultAsync<ComplexType>("foo", ct: TestContext.Current.CancellationToken);
		foo2.PropInt = 2;

		var foo3 = await cache.GetOrDefaultAsync<ComplexType>("foo", ct: TestContext.Current.CancellationToken);
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

	/* -------------------------------------------------- */







	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task AutoCloneSkipsImmutableObjectsAsync(SerializerType serializerType)
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(10);
		options.DefaultEntryOptions.EnableAutoClone = true;
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		fc.SetupSerializer(TestsUtils.GetSerializer(serializerType));
		var cache = new FusionHybridCache(fc);

		var imm = new SimpleImmutableObject
		{
			Name = "Imm",
			Age = 123
		};

		await cache.SetAsync("imm", imm, cancellationToken: TestContext.Current.CancellationToken);

		var imm1 = await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm", ct: TestContext.Current.CancellationToken);
		var imm2 = await cache.GetOrDefaultAsync<SimpleImmutableObject>("imm", ct: TestContext.Current.CancellationToken);

		Assert.Same(imm, imm1);
		Assert.Same(imm, imm2);
	}

	[Fact]
	public async Task CanRemoveByTagAsync()
	{
		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(10);
		using var fc = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		var cache = new FusionHybridCache(fc);

		await cache.SetAsync<int>("foo", 1, tags: ["x", "y"], cancellationToken: TestContext.Current.CancellationToken);
		await cache.SetAsync<int>("bar", 2, tags: ["y", "z"], cancellationToken: TestContext.Current.CancellationToken);
		await cache.GetOrCreateAsync<int>("baz", async _ => 3, tags: ["x", "z"], cancellationToken: TestContext.Current.CancellationToken);

		var foo1 = await cache.GetOrCreateAsync<int>("foo", async _ => 11, tags: ["x", "y"], cancellationToken: TestContext.Current.CancellationToken);
		var bar1 = await cache.GetOrCreateAsync<int>("bar", async _ => 22, tags: ["y", "z"], cancellationToken: TestContext.Current.CancellationToken);
		var baz1 = await cache.GetOrCreateAsync<int>("baz", async _ => 33, tags: ["x", "z"], cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(1, foo1);
		Assert.Equal(2, bar1);
		Assert.Equal(3, baz1);

		await cache.RemoveByTagAsync("x", TestContext.Current.CancellationToken);

		var foo2 = await cache.GetOrDefaultAsync<int>("foo", ct: TestContext.Current.CancellationToken);
		var bar2 = await cache.GetOrCreateAsync<int>("bar", async _ => 222, tags: ["y", "z"], cancellationToken: TestContext.Current.CancellationToken);
		var baz2 = await cache.GetOrCreateAsync<int>("baz", async _ => 333, tags: ["x", "z"], cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(0, foo2);
		Assert.Equal(2, bar2);
		Assert.Equal(333, baz2);

		await cache.RemoveByTagAsync("y", TestContext.Current.CancellationToken);

		var foo3 = await cache.GetOrCreateAsync<int>("foo", async _ => 1111, tags: ["x", "y"], cancellationToken: TestContext.Current.CancellationToken);
		var bar3 = await cache.GetOrCreateAsync<int>("bar", async _ => 2222, tags: ["y", "z"], cancellationToken: TestContext.Current.CancellationToken);
		var baz3 = await cache.GetOrCreateAsync<int>("baz", async _ => 3333, tags: ["x", "z"], cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(1111, foo3);
		Assert.Equal(2222, bar3);
		Assert.Equal(333, baz3);
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
	public async Task CanHandleSpeficicLocalExpirationAsync()
	{
		var defaultDuration = TimeSpan.FromMinutes(4);
		var localExpiration = TimeSpan.FromSeconds(1);

		using var fc = new FusionCache(new FusionCacheOptions(), logger: CreateXUnitLogger<FusionCache>());
		fc.DefaultEntryOptions.Duration = defaultDuration;

		var cache = new FusionHybridCache(fc);

		// SET VALUE
		await cache.SetAsync<int>(
			"foo",
			1,
			new HybridCacheEntryOptions
			{
				LocalCacheExpiration = localExpiration
			},
			cancellationToken: TestContext.Current.CancellationToken
		);

		// GETORCREATE (CACHE HIT -> NO FACTORY)
		var foo1 = await cache.GetOrCreateAsync<int>(
			"foo",
			async _ => 2,
			new HybridCacheEntryOptions
			{
				LocalCacheExpiration = localExpiration
			},
			cancellationToken: TestContext.Current.CancellationToken
		);

		Assert.Equal(1, foo1);

		// WAIT FOR THE EXPIRATION
		await Task.Delay(localExpiration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// GETORCREATE (CACHE MISS -> FACTORY)
		var foo2 = await cache.GetOrCreateAsync<int>(
			"foo",
			async _ => 3,
			new HybridCacheEntryOptions
			{
				LocalCacheExpiration = localExpiration
			},
			cancellationToken: TestContext.Current.CancellationToken
		);

		Assert.Equal(3, foo2);
	}
}
