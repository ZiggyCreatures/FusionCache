using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;

namespace FusionCacheTests;

public class DistributedLockerTests
	: AbstractTests
{
	public DistributedLockerTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private const string DistributedLockAcquireErrorMessage = "[DL] acquiring the DISTRIBUTED LOCK has thrown an exception";

	[Fact]
	public async Task CallerCancellationWhileAcquiringDistributedLockIsNotLoggedAsErrorAsync()
	{
		var logger = CreateListLogger<FusionCache>(LogLevel.Trace);
		using var callerCts = new CancellationTokenSource();
		var locker = new CallerCancelingDistributedLocker(callerCts);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		cache.SetupDistributedLocker(locker);

		// The caller's own token is canceled while parked waiting for the distributed lock
		// (eg: HttpContext.RequestAborted): this is a normal caller cancellation, not a locker error.
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await cache.GetOrSetAsync<int>(
				"foo",
				_ => Task.FromResult(42),
				new FusionCacheEntryOptions().SetDurationSec(10),
				token: callerCts.Token
			);
		});

		// The distributed lock path must actually have been exercised...
		Assert.True(locker.AcquireCalls > 0);
		// ...and the caller cancellation must NOT be logged as a distributed lock error.
		Assert.DoesNotContain(logger.Items, x => x.Message.Contains(DistributedLockAcquireErrorMessage));
	}

	[Fact]
	public void CallerCancellationWhileAcquiringDistributedLockIsNotLoggedAsError()
	{
		var logger = CreateListLogger<FusionCache>(LogLevel.Trace);
		using var callerCts = new CancellationTokenSource();
		var locker = new CallerCancelingDistributedLocker(callerCts);

		using var cache = new FusionCache(new FusionCacheOptions(), logger: logger);
		cache.SetupDistributedLocker(locker);

		// The caller's own token is canceled while parked waiting for the distributed lock
		// (eg: HttpContext.RequestAborted): this is a normal caller cancellation, not a locker error.
		Assert.ThrowsAny<OperationCanceledException>(() =>
		{
			cache.GetOrSet<int>(
				"foo",
				_ => 42,
				new FusionCacheEntryOptions().SetDurationSec(10),
				token: callerCts.Token
			);
		});

		// The distributed lock path must actually have been exercised...
		Assert.True(locker.AcquireCalls > 0);
		// ...and the caller cancellation must NOT be logged as a distributed lock error.
		Assert.DoesNotContain(logger.Items, x => x.Message.Contains(DistributedLockAcquireErrorMessage));
	}

	// A distributed locker that mimics Medallion.DistributedLock's BusyWaitHelper when the caller's
	// own CancellationToken is canceled while waiting to acquire the lock: it surfaces an
	// OperationCanceledException driven by that same token (a timeout would instead return null).
	private sealed class CallerCancelingDistributedLocker
		: IFusionCacheDistributedLocker
	{
		private readonly CancellationTokenSource _callerCts;

		public CallerCancelingDistributedLocker(CancellationTokenSource callerCts)
		{
			_callerCts = callerCts;
		}

		public int AcquireCalls;

		public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			Interlocked.Increment(ref AcquireCalls);
			_callerCts.Cancel();
			token.ThrowIfCancellationRequested();
			return null;
		}

		public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			Interlocked.Increment(ref AcquireCalls);
			_callerCts.Cancel();
			await Task.Yield();
			token.ThrowIfCancellationRequested();
			return null;
		}

		public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
		{
			// EMPTY
		}

		public ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
		{
			return default;
		}

		public void Dispose()
		{
			// EMPTY
		}
	}
}
