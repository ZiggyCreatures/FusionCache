using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

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

	public void SetEntry<TValue>(string operationId, string key, IFusionCacheMemoryEntry entry, FusionCacheEntryOptions options)
	{
		// ACTIVITY
		using var activity = Activities.SourceMemoryLevel.StartActivityWithCommonTags(Activities.Names.MemorySet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Memory);

		// IF FAIL-SAFE IS DISABLED AND DURATION IS <= ZERO -> REMOVE ENTRY (WILL SAVE RESOURCES)
		if (options.IsFailSafeEnabled == false && options.Duration <= TimeSpan.Zero)
		{
			RemoveEntry(operationId, key, options);
			return;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] saving entry in memory {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString());

		var (memoryEntryOptions, absoluteExpiration) = options.ToMemoryCacheEntryOptionsOrAbsoluteExpiration(_events, _options, _logger, operationId, key);

		if (memoryEntryOptions is not null)
		{
			entry.PhysicalExpiration = memoryEntryOptions.AbsoluteExpiration!.Value;

			_cache.Set<IFusionCacheMemoryEntry>(key, entry, memoryEntryOptions);
		}
		else if (absoluteExpiration is not null)
		{
			entry.PhysicalExpiration = absoluteExpiration.Value;

			_cache.Set<IFusionCacheMemoryEntry>(key, entry, absoluteExpiration.Value);
		}
		else
		{
			throw new InvalidOperationException("No MemoryCacheEntryOptions or AbsoluteExpiration was determined: this should not be possible, WTH!?");
		}

		// EVENT
		_events.OnSet(operationId, key);
	}

	public IFusionCacheMemoryEntry? GetEntryOrNull(string operationId, string key)
	{
		Metrics.CounterMemoryGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		// ACTIVITY
		using var activity = Activities.SourceMemoryLevel.StartActivityWithCommonTags(Activities.Names.MemoryGet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Memory);

		var entry = _cache.Get<IFusionCacheMemoryEntry?>(key);

		// EVENT
		if (entry is not null)
		{
			_events.OnHit(operationId, key, entry.IsLogicallyExpired());
		}
		else
		{
			_events.OnMiss(operationId, key);
		}

		return entry;
	}

	public (IFusionCacheMemoryEntry? entry, bool isValid) TryGetEntry(string operationId, string key)
	{
		Metrics.CounterMemoryGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		// ACTIVITY
		using var activity = Activities.SourceMemoryLevel.StartActivityWithCommonTags(Activities.Names.MemoryGet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Memory);

		IFusionCacheMemoryEntry? entry;
		bool isValid = false;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] trying to get from memory", _options.CacheName, _options.InstanceId, operationId, key);

		if (_cache.TryGetValue<IFusionCacheMemoryEntry>(key, out entry) == false)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] memory entry not found", _options.CacheName, _options.InstanceId, operationId, key);
		}
		else
		{
			if (entry.IsLogicallyExpired())
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] memory entry found (expired) {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString());
			}
			else
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] memory entry found {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString());

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
		// ACTIVITY
		using var activity = Activities.SourceMemoryLevel.StartActivityWithCommonTags(Activities.Names.MemoryRemove, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Memory);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] removing data (from memory)", _options.CacheName, _options.InstanceId, operationId, key);

		_cache.Remove(key);

		// EVENT
		_events.OnRemove(operationId, key);
	}

	public bool ExpireEntry(string operationId, string key, bool allowFailSafe, long? timestampThreshold)
	{
		// ACTIVITY
		using var activity = Activities.SourceMemoryLevel.StartActivityWithCommonTags(Activities.Names.MemoryExpire, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Memory);

		var entry = _cache.Get<IFusionCacheMemoryEntry>(key);

		if (entry is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] memory entry not found: not necessary to expire", _options.CacheName, _options.InstanceId, operationId, key);

			return false;
		}

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] Metadata {Metadata}", _options.CacheName, _options.InstanceId, operationId, key, entry.Metadata);

		if (timestampThreshold is not null && entry.Timestamp >= timestampThreshold.Value)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] timestamp of cached entry {TimestampCached} was greater than the specified threshold {TimestampThreshold}", _options.CacheName, _options.InstanceId, operationId, key, entry.Timestamp, timestampThreshold.Value);

			return false;
		}

		if (allowFailSafe && entry.Metadata is not null && entry.Metadata.IsLogicallyExpired() == false)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] expiring data (from memory)", _options.CacheName, _options.InstanceId, operationId, key);

			// MAKE THE ENTRY LOGICALLY EXPIRE
			entry.Metadata.LogicalExpiration = DateTimeOffset.UtcNow.AddMilliseconds(-10);

			// EVENT
			_events.OnExpire(operationId, key);
		}
		else
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [MC] removing data (from memory)", _options.CacheName, _options.InstanceId, operationId, key);

			// REMOVE THE ENTRY
			_cache.Remove(key);

			// EVENT
			_events.OnRemove(operationId, key);
		}

		return true;
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
