using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory
{

	internal class MemoryCacheAccessor
		: IDisposable
	{

		public MemoryCacheAccessor(IMemoryCache? memoryCache, FusionCacheOptions options, ILogger? logger, FusionCacheBaseEvents events)
		{
			if (memoryCache is object)
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
		private bool _cacheShouldBeDisposed;
		private readonly FusionCacheOptions _options;
		private readonly ILogger? _logger;
		private readonly FusionCacheBaseEvents _events;

		public void SetEntry<TValue>(string operationId, string key, FusionCacheMemoryEntry entry, FusionCacheEntryOptions options)
		{
			var memoryOptions = options.ToMemoryCacheEntryOptions();

			options.MemoryOptionsModifier?.Invoke(memoryOptions, entry.GetValue<TValue>());

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): saving entry in memory {Options} {Entry}", key, operationId, memoryOptions.ToLogString(), entry.ToLogString());

			_cache.Set<FusionCacheMemoryEntry>(key, entry, memoryOptions);

			// EVENT
			_events.OnSet(operationId, key);
		}

		public (FusionCacheMemoryEntry? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key)
		{
			FusionCacheMemoryEntry? entry;
			bool isValid = false;

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (K={CacheKey} OP={CacheOperationId}): trying to get from memory", key, operationId);

			if (_cache.TryGetValue<FusionCacheMemoryEntry>(key, out entry) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry not found", key, operationId);
			}
			else
			{
				if (entry.IsLogicallyExpired())
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry found (expired) {Entry}", key, operationId, entry.ToLogString());
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): memory entry found {Entry}", key, operationId, entry.ToLogString());

					isValid = true;
				}
			}

			// EVENT
			if (entry is object)
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
				_logger.LogDebug("FUSION (K={CacheKey} OP={CacheOperationId}): removing data (from memory)", key, operationId);

			_cache.Remove(key);

			// EVENT
			_events.OnRemove(operationId, key);
		}

		// IDISPOSABLE
		private bool disposedValue = false;
		protected virtual void Dispose(bool disposing)
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

}
