using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory;

internal sealed class MemoryCacheAccessor
	: IDisposable
{
	public MemoryCacheAccessor(IMemoryCache? memoryCache, FusionCacheOptions options, ILogger? logger, FusionCacheMemoryEventsHub events)
	{
		if (memoryCache is not null)
		{
			_cache = memoryCache;
		}
		else
		{
			_cache = new MemoryCache(new MemoryCacheOptions());
			_cacheShouldBeDisposed = true;
		}
		_options = options;
		_logger = logger;
		_events = events;
	}

	private IMemoryCache _cache;
	private readonly bool _cacheShouldBeDisposed;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheMemoryEventsHub _events;

	public void SetEntry<TValue>(string operationId, string key, FusionCacheMemoryEntry entry, FusionCacheEntryOptions options)
	{
		// IF FAIL-SAFE IS DISABLED AND DURATION IS <= ZERO -> REMOVE ENTRY (WILL SAVE RESOURCES)
		if (options.IsFailSafeEnabled == false && options.Duration <= TimeSpan.Zero)
		{
			RemoveEntry(operationId, key, options);
			return;
		}

		var memoryOptions = options.ToMemoryCacheEntryOptions(_events, _options, _logger, operationId, key);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): saving entry in memory {Options} {Entry}", _options.CacheName, operationId, key, memoryOptions.ToLogString(), entry.ToLogString());

		_cache.Set<FusionCacheMemoryEntry>(key, entry, memoryOptions);

		// EVENT
		_events.OnSet(operationId, key);
	}

	public (FusionCacheMemoryEntry? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key)
	{
		FusionCacheMemoryEntry? entry;
		bool isValid = false;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): trying to get from memory", _options.CacheName, operationId, key);

		if (_cache.TryGetValue<FusionCacheMemoryEntry>(key, out entry) == false)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): memory entry not found", _options.CacheName, operationId, key);
		}
		else
		{
			if (entry.IsLogicallyExpired())
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): memory entry found (expired) {Entry}", _options.CacheName, operationId, key, entry.ToLogString());
			}
			else
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): memory entry found {Entry}", _options.CacheName, operationId, key, entry.ToLogString());

				isValid = true;
			}
		}

		// EVENT
		if (entry is not null)
		{
			_events.OnHit(operationId, key, isValid == false);
		}
		else
		{
			_events.OnMiss(operationId, key);
		}

		return (entry, isValid);
	}

	public void RemoveEntry(string operationId, string key, FusionCacheEntryOptions options)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): removing data (from memory)", _options.CacheName, operationId, key);

		_cache.Remove(key);

		// EVENT
		_events.OnRemove(operationId, key);
	}

	public void ExpireEntry(string operationId, string key, bool allowFailSafe)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): expiring data (from memory)", _options.CacheName, operationId, key);

		if (_cache.TryGetValue<IFusionCacheEntry>(key, out var entry) == false)
			return;

		if (entry is null)
			return;

		if (allowFailSafe && entry.Metadata is not null && entry.Metadata.IsLogicallyExpired() == false)
		{
			// MAKE THE ENTRY LOGICALLY EXPIRE
			entry.Metadata.LogicalExpiration = DateTimeOffset.UtcNow.AddMilliseconds(-10);

			// EVENT
			_events.OnExpire(operationId, key);
		}
		else
		{
			// REMOVE THE ENTRY
			_cache.Remove(key);

			// EVENT
			_events.OnRemove(operationId, key);
		}
	}

	// IDISPOSABLE
	private bool disposedValue = false;
	private void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (_cacheShouldBeDisposed)
				{
					(_cache as MemoryCache)?.Compact(1);
					_cache.Dispose();
				}
			}
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_cache = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(true);
	}
}
