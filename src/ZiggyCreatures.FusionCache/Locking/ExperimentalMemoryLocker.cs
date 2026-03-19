using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Locking;

/// <summary>
/// A standard implementation of <see cref="IFusionCacheMemoryLocker"/>.
/// </summary>
internal sealed class ExperimentalMemoryLocker
	: IFusionCacheMemoryLocker
{
	private Dictionary<string, TaskCompletionSource<bool>> _tcsCache;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExperimentalMemoryLocker"/> class.
	/// </summary>
	public ExperimentalMemoryLocker()
	{
		_tcsCache = [];
	}

	private (bool Created, TaskCompletionSource<bool> Tcs) GetTcs(string key)
	{
		if (_tcsCache.TryGetValue(key, out var tcs))
			return (false, tcs);

		lock (_tcsCache)
		{
			if (_tcsCache.TryGetValue(key, out tcs))
				return (false, tcs);

			tcs = new TaskCompletionSource<bool>();

			_tcsCache[key] = tcs;

			return (true, tcs);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var (created, tcs) = GetTcs(key);

		if (created)
		{
			return tcs;
		}

		//try
		//{

		// TODO: WHAT DO?
		//await tcs.Task.WaitAsync(timeout, token);
		await tcs.Task.ConfigureAwait(false);

		return tcs.Task;

		//}
		//catch (Exception exc)
		//{
		//	if (logger?.IsEnabled(LogLevel.Warning) ?? false)
		//		logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to acquire a TaskCompletionSource in the memory locker", cacheName, cacheInstanceId, operationId, key);

		//	return null;
		//}
	}

	/// <inheritdoc/>
	public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key, TimeSpan timeout, ILogger? logger, CancellationToken token)
	{
		var (created, tcs) = GetTcs(key);

		if (created)
		{
			return tcs;
		}

		//try
		//{

		// TODO: WHAT DO?
		//tcs.Task.Wait((int)timeout.TotalMilliseconds, token);
		tcs.Task.Wait();
		return tcs.Task;

		//}
		//catch (Exception exc)
		//{
		//	if (logger?.IsEnabled(LogLevel.Warning) ?? false)
		//		logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to acquire a TaskCompletionSource in the memory locker", cacheName, cacheInstanceId, operationId, key);

		//	return null;
		//}
	}

	/// <inheritdoc/>
	public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key, object? lockObj, ILogger? logger)
	{
		if (lockObj is null)
			return;

		try
		{
			if (lockObj is TaskCompletionSource<bool> tcs)
			{
				tcs.SetResult(true);
				_tcsCache.Remove(key);
			}
		}
		catch (Exception exc)
		{
			if (logger?.IsEnabled(LogLevel.Warning) ?? false)
				logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to release a SemaphoreSlim in the experimental memory locker", cacheName, cacheInstanceId, operationId, key);
		}
	}

	// IDISPOSABLE
	private bool _disposedValue;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposedValue)
		{
			return;
		}

		_tcsCache.Clear();

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
		_tcsCache = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		_disposedValue = true;
	}
}
