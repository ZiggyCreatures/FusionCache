using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests;

public class TagInvalidationConcurrencyTests
{
	[Theory]
	[InlineData(false, false, false)]
	[InlineData(false, false, true)]
	[InlineData(false, true, false)]
	[InlineData(false, true, true)]
	[InlineData(true, false, false)]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(true, true, true)]
	public async Task ConcurrentInvalidationsDoNotRestoreAnInvalidatedEntry(bool firstSync, bool secondSync, bool useDistributedCache)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		using var memory = new StallingMemoryCache("prefix:__fc:t:tag", deadline.Token);
		using var locker = new TrackingMemoryLocker("prefix:__fc:t:tag");
		using var cache = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "prefix:" }, memory, memoryLocker: locker);
		var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		if (useDistributedCache)
			cache.SetupDistributedCache(distributedCache, new FusionCacheSystemTextJsonSerializer());

		Task RemoveTag(bool sync) => Task.Run(async () =>
		{
			if (sync)
				cache.RemoveByTag("tag", token: deadline.Token);
			else
				await cache.RemoveByTagAsync("tag", token: deadline.Token);
		}, deadline.Token);

		var first = RemoveTag(firstSync);
		Task? second = null;
		try
		{
			await memory.Stalled.Task.WaitAsync(deadline.Token);
			await cache.RemoveByTagAsync("unrelated", token: deadline.Token);
			await Task.Delay(10, deadline.Token);
			await cache.SetAsync("key", 42, tags: ["tag"], token: deadline.Token);
			await Task.Delay(10, deadline.Token);
			second = RemoveTag(secondSync);
			await Task.WhenAny(second, locker.SecondAttempt.Task).WaitAsync(deadline.Token);
		}
		finally
		{
			memory.Release();
		}
		await first.WaitAsync(deadline.Token);
		if (second is not null)
			await second.WaitAsync(deadline.Token);

		Assert.False((await cache.TryGetAsync<int>("key", token: deadline.Token)).HasValue);
		if (useDistributedCache)
		{
			using var reader = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "prefix:" });
			reader.SetupDistributedCache(distributedCache, new FusionCacheSystemTextJsonSerializer());
			Assert.False((await reader.TryGetAsync<int>("key", token: deadline.Token)).HasValue);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DistributedMarkerWritesCompleteInInvalidationOrder(bool sync)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		using var distributed = new StallingDistributedCache(deadline.Token);
		using var locker = new TrackingMemoryLocker("prefix:__fc:t:tag");
		using var cache = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "prefix:" }, memoryLocker: locker);
		cache.SetupDistributedCache(distributed, new FusionCacheSystemTextJsonSerializer());

		Task RemoveTag() => Task.Run(async () =>
		{
			if (sync)
				cache.RemoveByTag("tag", token: deadline.Token);
			else
				await cache.RemoveByTagAsync("tag", token: deadline.Token);
		}, deadline.Token);

		var first = RemoveTag();
		Task? second = null;
		try
		{
			await distributed.Stalled.Task.WaitAsync(deadline.Token);
			await Task.Delay(10, deadline.Token);
			await cache.SetAsync("key", 42, tags: ["tag"], token: deadline.Token);
			await Task.Delay(10, deadline.Token);
			second = RemoveTag();
			await Task.WhenAny(second, locker.SecondAttempt.Task).WaitAsync(deadline.Token);
		}
		finally
		{
			distributed.Release();
		}
		await first.WaitAsync(deadline.Token);
		if (second is not null)
			await second.WaitAsync(deadline.Token);

		using var reader = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "prefix:" });
		reader.SetupDistributedCache(distributed, new FusionCacheSystemTextJsonSerializer());
		Assert.False((await reader.TryGetAsync<int>("key", token: deadline.Token)).HasValue);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WaitingInvalidationCanBeCancelled(bool sync)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
		using var locker = new TrackingMemoryLocker("prefix:__fc:t:tag");
		using var cache = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "prefix:" }, memoryLocker: locker);
		var held = locker.AcquireLock(cache.CacheName, cache.InstanceId, "test", "prefix:__fc:t:tag", Timeout.InfiniteTimeSpan, null, deadline.Token);
		try
		{
			var waiting = Task.Run(async () =>
			{
				if (sync)
					cache.RemoveByTag("tag", token: cancelled.Token);
				else
					await cache.RemoveByTagAsync("tag", token: cancelled.Token);
			}, deadline.Token);
			await locker.SecondAttempt.Task.WaitAsync(deadline.Token);
			cancelled.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
		}
		finally
		{
			locker.ReleaseLock(cache.CacheName, cache.InstanceId, "test", "prefix:__fc:t:tag", held, null);
		}
		await cache.RemoveByTagAsync("tag", token: deadline.Token);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FailedMarkerWriteDoesNotBlockLaterInvalidations(bool sync)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		using var memory = new StallingMemoryCache("__fc:t:tag", deadline.Token, failFirstWrite: true);
		memory.Release();
		using var cache = new FusionCache(new FusionCacheOptions(), memory);
		await cache.SetAsync("key", 42, tags: ["tag"], token: deadline.Token);
		if (sync)
			Assert.Throws<IOException>(() => cache.RemoveByTag("tag", token: deadline.Token));
		else
			await Assert.ThrowsAsync<IOException>(async () => await cache.RemoveByTagAsync("tag", token: deadline.Token));

		await cache.RemoveByTagAsync("tag", token: deadline.Token);
		Assert.False((await cache.TryGetAsync<int>("key", token: deadline.Token)).HasValue);
	}

	private sealed class StallingMemoryCache(string markerKey, CancellationToken token, bool failFirstWrite = false) : IMemoryCache
	{
		private readonly MemoryCache _inner = new(new MemoryCacheOptions());
		private readonly ManualResetEventSlim _release = new();
		private int _writes;
		public TaskCompletionSource Stalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public ICacheEntry CreateEntry(object key)
		{
			if (Equals(key, markerKey) && Interlocked.Increment(ref _writes) == 1)
			{
				Stalled.TrySetResult();
				_release.Wait(token);
				if (failFirstWrite)
					throw new IOException("Marker write failed");
			}
			return _inner.CreateEntry(key);
		}

		public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
		public void Remove(object key) => _inner.Remove(key);
		public void Release() => _release.Set();
		public void Dispose()
		{
			_release.Dispose();
			_inner.Dispose();
		}
	}

	private sealed class StallingDistributedCache(CancellationToken token) : IDistributedCache, IDisposable
	{
		private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _writes;
		public TaskCompletionSource Stalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		private async Task WaitForRelease(string key)
		{
			if (key.EndsWith("prefix:__fc:t:tag", StringComparison.Ordinal) && Interlocked.Increment(ref _writes) == 1)
			{
				Stalled.TrySetResult();
				await _release.Task.WaitAsync(token);
			}
		}

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			WaitForRelease(key).GetAwaiter().GetResult();
			_inner.Set(key, value, options);
		}

		public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			await WaitForRelease(key);
			await _inner.SetAsync(key, value, options, token);
		}

		public byte[]? Get(string key) => _inner.Get(key);
		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _inner.GetAsync(key, token);
		public void Refresh(string key) => _inner.Refresh(key);
		public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);
		public void Remove(string key) => _inner.Remove(key);
		public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
		public void Release() => _release.TrySetResult();
		public void Dispose() => Release();
	}

	private sealed class TrackingMemoryLocker(string markerKey) : IFusionCacheMemoryLocker
	{
		private readonly StandardMemoryLocker _inner = new();
		private int _attempts;
		public TaskCompletionSource SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		private void Track(string key)
		{
			if (key == markerKey && Interlocked.Increment(ref _attempts) == 2)
				SecondAttempt.TrySetResult();
		}

		public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			Track(key);
			return _inner.AcquireLock(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
		}

		public ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
		{
			Track(key);
			return _inner.AcquireLockAsync(cacheName, cacheInstanceId, operationId, key, timeout, logger, token);
		}

		public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
			=> _inner.ReleaseLock(cacheName, cacheInstanceId, operationId, key, lockObj, logger);
		public void Dispose() => _inner.Dispose();
	}
}
