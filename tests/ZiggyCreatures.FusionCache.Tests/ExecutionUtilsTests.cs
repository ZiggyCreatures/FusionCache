using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.FusionCaching.Internals;

namespace FusionCaching.Tests
{
	public class ExecutionUtilsTests
	{

		[Fact]
		public async Task CancelingAsyncFuncActuallyCancelsItAsync()
		{
			int res = -1;
			var factoryTerminated = false;
			var outerCancelDelayMs = 500;
			var innerDelayMs = 2_000;
			await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			{
				var cts = new CancellationTokenSource(outerCancelDelayMs);
				res = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
			});
			await Task.Delay(innerDelayMs);

			Assert.Equal(-1, res);
			Assert.False(factoryTerminated);
		}

		[Fact]
		public void CancelingAsyncFuncActuallyCancelsIt()
		{
			int res = -1;
			var factoryTerminated = false;
			var outerCancelDelayMs = 500;
			var innerDelayMs = 2_000;
			Assert.Throws<OperationCanceledException>(() =>
			{
				var cts = new CancellationTokenSource(outerCancelDelayMs);
				res = FusionCacheExecutionUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
			});

			Assert.Equal(-1, res);
			Assert.False(factoryTerminated);
		}

		[Fact]
		public void CancelingSyncFuncActuallyCancelsIt()
		{
			int res = -1;
			var factoryTerminated = false;
			var outerCancelDelayMs = 500;
			var innerDelayMs = 2_000;
			Assert.Throws<OperationCanceledException>(() =>
			{
				var cts = new CancellationTokenSource(outerCancelDelayMs);
				res = FusionCacheExecutionUtils.RunSyncFuncWithTimeout(ct => { Thread.Sleep(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
			});

			Assert.Equal(-1, res);
			Assert.False(factoryTerminated);
		}

		[Fact]
		public async Task TimeoutEffectivelyWorksAsync()
		{
			int res = -1;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			var sw = Stopwatch.StartNew();
			await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
			{
				res = await FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, TimeSpan.FromMilliseconds(timeoutMs));
			});
			sw.Stop();

			Assert.Equal(-1, res);
			Assert.True(sw.ElapsedMilliseconds >= timeoutMs);
			Assert.True(sw.ElapsedMilliseconds < innerDelayMs);
		}

		[Fact]
		public void TimeoutEffectivelyWorks()
		{
			int res = -1;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			var sw = Stopwatch.StartNew();
			Assert.ThrowsAny<TimeoutException>(() =>
			{
				res = FusionCacheExecutionUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, TimeSpan.FromMilliseconds(timeoutMs));
			});
			sw.Stop();

			Assert.Equal(-1, res);
			Assert.True(sw.ElapsedMilliseconds >= timeoutMs);
			Assert.True(sw.ElapsedMilliseconds < innerDelayMs);
		}

		[Fact]
		public async Task CancelWhenTimeoutActuallyWorksAsync()
		{
			var factoryCompleted = false;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
			{
				await FusionCacheExecutionUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), true);
			});
			await Task.Delay(innerDelayMs);

			Assert.False(factoryCompleted);
		}

		[Fact]
		public void CancelWhenTimeoutActuallyWorks()
		{
			var factoryCompleted = false;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			Assert.ThrowsAny<TimeoutException>(() =>
			{
				FusionCacheExecutionUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), true);
			});
			Thread.Sleep(innerDelayMs);

			Assert.False(factoryCompleted);
		}

		[Fact]
		public async Task DoNotCancelWhenTimeoutActuallyWorksAsync()
		{
			var factoryCompleted = false;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
			{
				await FusionCacheExecutionUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), false);
			});
			await Task.Delay(innerDelayMs);

			Assert.True(factoryCompleted);
		}

		[Fact]
		public void DoNotCancelWhenTimeoutActuallyWorks()
		{
			var factoryCompleted = false;
			var timeoutMs = 500;
			var innerDelayMs = 2_000;
			Assert.ThrowsAny<TimeoutException>(() =>
			{
				FusionCacheExecutionUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), false);
			});
			Thread.Sleep(innerDelayMs);

			Assert.True(factoryCompleted);
		}

	}
}
