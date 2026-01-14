using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Internals.DistributedLocker;

internal partial class DistributedLockerAccessor
{
	public async ValueTask<object?> AcquireLockAsync(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] waiting to acquire the DISTRIBUTED LOCK", _options.CacheName, _options.InstanceId, operationId, key);

		try
		{
			var lockObj = await _locker.AcquireLockAsync(_options.CacheName, _options.InstanceId!, operationId, key, GetLockName(key), timeout, _logger, token).ConfigureAwait(false);

			if (lockObj is not null)
			{
				// LOCK ACQUIRED
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK acquired", _options.CacheName, _options.InstanceId, operationId, key);
			}
			else
			{
				// LOCK TIMEOUT
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK timeout", _options.CacheName, _options.InstanceId, operationId, key);
			}

			return lockObj;
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] acquiring the DISTRIBUTED LOCK has thrown an exception", _options.CacheName, _options.InstanceId, operationId, key);

			return null;
		}
	}

	public async ValueTask ReleaseDistributedLockAsync(string operationId, string key, object? lockObj, CancellationToken token)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing DISTRIBUTED LOCK", _options.CacheName, _options.InstanceId, operationId, key);

		try
		{
			await _locker.ReleaseLockAsync(_options.CacheName, _options.InstanceId!, operationId, key, GetLockName(key), lockObj, _logger, token).ConfigureAwait(false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK released", _options.CacheName, _options.InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing the DISTRIBUTED LOCK has thrown an exception", _options.CacheName, _options.InstanceId, operationId, key);
		}
	}
}
