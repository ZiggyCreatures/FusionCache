using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests
{
	public class ExecutionUtilsTests
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
		public async Task CancelingAsyncFuncActuallyCancelsItAsync()
		{
			int res = -1;
			var factoryTerminated = false;
			var outerCancelDelayMs = 500;
			var innerDelayMs = 2_000;
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			{
				var cts = new CancellationTokenSource(outerCancelDelayMs);
				res = await RunUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
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
			Assert.ThrowsAny<OperationCanceledException>(() =>
			{
				var cts = new CancellationTokenSource(outerCancelDelayMs);
				res = RunUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
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
				res = RunUtils.RunSyncFuncWithTimeout(ct => { Thread.Sleep(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryTerminated = true; return 42; }, Timeout.InfiniteTimeSpan, true, token: cts.Token);
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
				res = await RunUtils.RunAsyncFuncWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, TimeSpan.FromMilliseconds(timeoutMs));
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
				res = RunUtils.RunAsyncFuncWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); return 42; }, TimeSpan.FromMilliseconds(timeoutMs));
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
				await RunUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), true);
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
				RunUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), true);
			});
			Thread.Sleep(innerDelayMs);

			Assert.False(factoryCompleted);
		}

		[Fact]
		public async Task DoNotCancelWhenTimeoutActuallyWorksAsync()
		{
			var factoryCompleted = false;
			var timeoutMs = 100;
			var innerDelayMs = 2_000;
			await Assert.ThrowsAnyAsync<TimeoutException>(async () =>
			{
				await RunUtils.RunAsyncActionWithTimeoutAsync(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), false);
			});
			await Task.Delay(innerDelayMs + timeoutMs);

			Assert.True(factoryCompleted);
		}

		[Fact]
		public void DoNotCancelWhenTimeoutActuallyWorks()
		{
			var factoryCompleted = false;
			var timeoutMs = 100;
			var innerDelayMs = 2_000;
			Assert.ThrowsAny<TimeoutException>(() =>
			{
				RunUtils.RunAsyncActionWithTimeout(async ct => { await Task.Delay(innerDelayMs); ct.ThrowIfCancellationRequested(); factoryCompleted = true; }, TimeSpan.FromMilliseconds(timeoutMs), false);
			});
			Thread.Sleep(innerDelayMs + timeoutMs);

			Assert.True(factoryCompleted);
		}
	}
}
