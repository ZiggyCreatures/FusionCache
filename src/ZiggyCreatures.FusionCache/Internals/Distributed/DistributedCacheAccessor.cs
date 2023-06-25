using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal sealed partial class DistributedCacheAccessor
{
	private const string WireFormatVersion = "v1";
	private const char WireFormatSeparator = ':';

	public DistributedCacheAccessor(IDistributedCache distributedCache, IFusionCacheSerializer serializer, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
	{
		if (distributedCache is null)
			throw new ArgumentNullException(nameof(distributedCache));

		if (serializer is null)
			throw new ArgumentNullException(nameof(serializer));

		_cache = distributedCache;
		_serializer = serializer;

		_options = options;

		_logger = logger;
		_events = events;

		// CIRCUIT-BREAKER
		_breaker = new SimpleCircuitBreaker(distributedCache is null ? TimeSpan.Zero : options.DistributedCacheCircuitBreakerDuration);

		// WIRE FORMAT SETUP
		_wireFormatToken = _options.DistributedCacheKeyModifierMode == CacheKeyModifierMode.Prefix
			? (WireFormatVersion + WireFormatSeparator)
			: _options.DistributedCacheKeyModifierMode == CacheKeyModifierMode.Suffix
				? WireFormatSeparator + WireFormatVersion
				: string.Empty;
	}

	private readonly IDistributedCache _cache;
	private readonly IFusionCacheSerializer _serializer;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheDistributedEventsHub _events;
	private readonly SimpleCircuitBreaker _breaker;
	private readonly string _wireFormatToken;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string MaybeProcessCacheKey(string key)
	{
		switch (_options.DistributedCacheKeyModifierMode)
		{
			case CacheKeyModifierMode.Prefix:
				return _wireFormatToken + key;
			case CacheKeyModifierMode.Suffix:
				return key + _wireFormatToken;
			default:
				return key;
		}
	}

	private void UpdateLastError(string key, string operationId)
	{
		// NO DISTRIBUTEC CACHE
		if (_cache is null)
			return;

		var res = _breaker.TryOpen(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): distributed cache temporarily de-activated for {BreakDuration}", _options.CacheName, operationId, key, _breaker.BreakDuration);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, false);
		}
	}

	public bool IsCurrentlyUsable(string? operationId, string? key)
	{
		var res = _breaker.IsClosed(out var hasChanged);

		if (res && hasChanged)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): distributed cache activated again", _options.CacheName, operationId, key);

			// EVENT
			_events.OnCircuitBreakerChange(operationId, key, true);
		}

		return res;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessError(string operationId, string key, Exception exc, string actionDescription)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while " + actionDescription, _options.CacheName, operationId, key);

			return;
		}

		UpdateLastError(key, operationId);

		if (_logger?.IsEnabled(_options.DistributedCacheErrorsLogLevel) ?? false)
			_logger.Log(_options.DistributedCacheErrorsLogLevel, exc, "FUSION [{CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while " + actionDescription, _options.CacheName, operationId, key);
	}
}
