using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal sealed partial class DistributedCacheAccessor
{
	private readonly IDistributedCache _cache;
	private readonly IFusionCacheSerializer _serializer;
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly FusionCacheDistributedEventsHub _events;
	private readonly SimpleCircuitBreaker _breaker;
	private readonly string _wireFormatToken;

	private static readonly MethodInfo __methodInfoSetEntryAsyncOpenGeneric = typeof(DistributedCacheAccessor).GetMethod(nameof(SetEntryAsync), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
	private static readonly ConcurrentDictionary<Type, MethodInfo> __methodInfoSetEntryAsyncCache = new ConcurrentDictionary<Type, MethodInfo>();

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
			? (FusionCacheOptions.DistributedCacheWireFormatVersion + FusionCacheOptions.DistributedCacheWireFormatSeparator)
			: _options.DistributedCacheKeyModifierMode == CacheKeyModifierMode.Suffix
				? FusionCacheOptions.DistributedCacheWireFormatSeparator + FusionCacheOptions.DistributedCacheWireFormatVersion
				: string.Empty;

		_wireFormatToken = _options.DistributedCacheKeyModifierMode switch
		{
			CacheKeyModifierMode.Prefix => FusionCacheOptions.DistributedCacheWireFormatVersion + FusionCacheOptions.DistributedCacheWireFormatSeparator,
			CacheKeyModifierMode.Suffix => FusionCacheOptions.DistributedCacheWireFormatSeparator + FusionCacheOptions.DistributedCacheWireFormatVersion,
			CacheKeyModifierMode.None => string.Empty,
			_ => throw new NotImplementedException(),
		};
	}

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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed cache temporarily de-activated for {BreakDuration}", _options.CacheName, _options.InstanceId, operationId, key, _breaker.BreakDuration);

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
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed cache activated again", _options.CacheName, _options.InstanceId, operationId, key);

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
				_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] a synthetic timeout occurred while " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);

			return;
		}

		UpdateLastError(key, operationId);

		if (_logger?.IsEnabled(_options.DistributedCacheErrorsLogLevel) ?? false)
			_logger.Log(_options.DistributedCacheErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);
	}

	public async ValueTask<bool> SetEntryUntypedAsync(string operationId, string key, FusionCacheMemoryEntry memoryEntry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		try
		{
			if (memoryEntry is null)
				return false;

			var methodInfo = __methodInfoSetEntryAsyncCache.GetOrAdd(memoryEntry.ValueType, x => __methodInfoSetEntryAsyncOpenGeneric.MakeGenericMethod(x));

			// SIGNATURE PARAMS: string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token
			return await ((ValueTask<bool>)methodInfo.Invoke(this, new object[] { operationId, key, memoryEntry, options, isBackground, token })).ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while calling SetEntryUntypedAsync() to try to set a distributed entry without knowing the TValue type", _options.CacheName, _options.InstanceId, operationId, key);

			return false;
		}
	}
}
