﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public class RunUtilsTests
{
	[Fact]
	public async Task ZeroTimeoutDoesNotStartAsyncFuncAsync()
	{
		bool _hasRun = false;

		await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
		{
			await RunUtils.RunAsyncFuncWithTimeoutAsync(async ct => { _hasRun = true; return 42; }, TimeSpan.Zero, false, t => { });
		});
		Assert.False(_hasRun);
	}

	[Fact]
	public void ZeroTimeoutDoesNotStartAsyncFunc()
	{
		bool _hasRun = false;

		Assert.Throws<SyntheticTimeoutException>(() =>
		{
			RunUtils.RunAsyncFuncWithTimeout(async ct => { _hasRun = true; return 42; }, TimeSpan.Zero, false, t => { });
		});
		Assert.False(_hasRun);
	}

	[Fact]
	public async Task ZeroTimeoutDoesNotStartAsyncActionAsync()
	{
		bool _hasRun = false;

		await Assert.ThrowsAsync<SyntheticTimeoutException>(async () =>
		{
			await RunUtils.RunAsyncActionWithTimeoutAsync(async ct => { _hasRun = true; }, TimeSpan.Zero, false, t => { });
		});
		Assert.False(_hasRun);
	}

	[Fact]
	public void ZeroTimeoutDoesNotStartAsyncAction()
	{
		bool _hasRun = false;

		Assert.Throws<SyntheticTimeoutException>(() =>
		{
			RunUtils.RunAsyncActionWithTimeout(async ct => { _hasRun = true; }, TimeSpan.Zero, false, t => { });
		});
		Assert.False(_hasRun);
	}

	[Fact]
	public async Task CanCancelAnAsyncFuncAsync()
	{
		int res = -1;
		var factoryTerminated = false;
		var outerCancelDelay = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			var cts = new CancellationTokenSource(outerCancelDelay);
			res = await RunUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
		});
		await Task.Delay(innerDelay.PlusALittleBit());

		Assert.Equal(-1, res);
		Assert.False(factoryTerminated);
	}

	[Fact]
	public void CanCancelAnAsyncFunc()
	{
		int res = -1;
		var factoryTerminated = false;
		var outerCancelDelay = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		Assert.ThrowsAny<OperationCanceledException>(() =>
		{
			var cts = new CancellationTokenSource(outerCancelDelay);
			res = RunUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
		});
		Thread.Sleep(innerDelay.PlusALittleBit());

		Assert.Equal(-1, res);
		Assert.False(factoryTerminated);
	}

	[Fact]
	public void CanCancelASyncFunc()
	{
		int res = -1;
		var factoryTerminated = false;
		var outerCancelDelay = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		Assert.Throws<OperationCanceledException>(() =>
		{
			var cts = new CancellationTokenSource(outerCancelDelay);
			res = RunUtils.RunSyncFuncWithTimeout(ct => { Thread.Sleep(innerDelay); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
		});
		Thread.Sleep(innerDelay.PlusALittleBit());

		Assert.Equal(-1, res);
		Assert.False(factoryTerminated);
	}

	[Fact]
	public async Task TimeoutEffectivelyWorksAsync()
	{
		int res = -1;
		var timeout = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		var sw = Stopwatch.StartNew();
		await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
		{
			res = await RunUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); return 42; }, timeout);
		});
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		Assert.Equal(-1, res);
		Assert.True(elapsedMs >= timeout.TotalMilliseconds, $"Elapsed ({elapsedMs}ms) is less than specified timeout ({timeout.TotalMilliseconds}ms)");
		Assert.True(elapsedMs < innerDelay.TotalMilliseconds, $"Elapsed ({elapsedMs}ms) is greater than or equal to inner delay ({innerDelay.TotalMilliseconds}ms)");
	}

	[Fact]
	public void TimeoutEffectivelyWorks()
	{
		int res = -1;
		var timeout = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		var sw = Stopwatch.StartNew();
		Assert.ThrowsAny<TimeoutException>(() =>
		{
			res = RunUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); return 42; }, timeout);
		});
		sw.Stop();

		var elapsedMs = sw.GetElapsedWithSafePad().TotalMilliseconds;

		Assert.Equal(-1, res);
		Assert.True(elapsedMs >= timeout.TotalMilliseconds, $"Elapsed ({elapsedMs}ms) is less than specified timeout ({timeout.TotalMilliseconds}ms)");
		Assert.True(elapsedMs < innerDelay.TotalMilliseconds, $"Elapsed ({elapsedMs}ms) is greater than or equal to inner delay ({innerDelay.TotalMilliseconds}ms)");
	}

	[Fact]
	public async Task CancelWhenTimeoutActuallyWorksAsync()
	{
		var factoryCompleted = false;
		var timeout = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
		{
			await RunUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, timeout, true);
		});
		await Task.Delay(innerDelay.PlusALittleBit());

		Assert.False(factoryCompleted);
	}

	[Fact]
	public void CancelWhenTimeoutActuallyWorks()
	{
		var factoryCompleted = false;
		var timeout = TimeSpan.FromMilliseconds(500);
		var innerDelay = TimeSpan.FromSeconds(2);
		Assert.ThrowsAny<TimeoutException>(() =>
		{
			RunUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, timeout, true);
		});
		Thread.Sleep(innerDelay.PlusALittleBit());

		Assert.False(factoryCompleted);
	}

	[Fact]
	public async Task DoNotCancelWhenTimeoutActuallyWorksAsync()
	{
		var factoryCompleted = false;
		var timeout = TimeSpan.FromMilliseconds(100);
		var innerDelay = TimeSpan.FromSeconds(2);
		await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
		{
			await RunUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, timeout, false);
		});
		await Task.Delay((innerDelay + timeout).PlusALittleBit());

		Assert.True(factoryCompleted);
	}

	[Fact]
	public void DoNotCancelWhenTimeoutActuallyWorks()
	{
		var factoryCompleted = false;
		var timeout = TimeSpan.FromMilliseconds(100);
		var innerDelay = TimeSpan.FromSeconds(2);
		Assert.ThrowsAny<TimeoutException>(() =>
		{
			RunUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelay); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, timeout, false);
		});
		Thread.Sleep((innerDelay + timeout).PlusALittleBit());

		Assert.True(factoryCompleted);
	}
}
