//using System.Runtime.CompilerServices;
//using Microsoft.Extensions.Logging;
//using ZiggyCreatures.Caching.Fusion.Events;
//using ZiggyCreatures.Caching.Fusion.Locking;

//namespace ZiggyCreatures.Caching.Fusion.Internals.DistributedLocker;

//internal sealed partial class DistributedLockerAccessor
//{
//	private readonly IFusionCacheDistributedLocker _locker;
//	private readonly FusionCacheOptions _options;
//	private readonly ILogger? _logger;
//	private readonly FusionCacheDistributedEventsHub _events;
//	private readonly SimpleCircuitBreaker _breaker;
//	private readonly string _wireFormatToken;

//	public DistributedLockerAccessor(IFusionCacheDistributedLocker distributedLocker, FusionCacheOptions options, ILogger? logger, FusionCacheDistributedEventsHub events)
//	{
//		if (distributedLocker is null)
//			throw new ArgumentNullException(nameof(distributedLocker));

//		_locker = distributedLocker;

//		_options = options;

//		_logger = logger;
//		_events = events;

//		// CIRCUIT-BREAKER
//		_breaker = new SimpleCircuitBreaker(options.DistributedCacheCircuitBreakerDuration);

//		// WIRE FORMAT SETUP
//		_wireFormatToken = _options.DistributedCacheKeyModifierMode switch
//		{
//			CacheKeyModifierMode.Prefix => FusionCacheOptions.DistributedCacheWireFormatVersion + _options.InternalStrings.DistributedCacheWireFormatSeparator,
//			CacheKeyModifierMode.Suffix => _options.InternalStrings.DistributedCacheWireFormatSeparator + FusionCacheOptions.DistributedCacheWireFormatVersion,
//			CacheKeyModifierMode.None => string.Empty,
//			_ => throw new NotImplementedException(),
//		};
//	}

//	public IFusionCacheDistributedLocker DistributedLocker
//	{
//		get { return _locker; }
//	}

//	[MethodImpl(MethodImplOptions.AggressiveInlining)]
//	private string MaybeProcessCacheKey(string key)
//	{
//		return _options.DistributedCacheKeyModifierMode switch
//		{
//			CacheKeyModifierMode.Prefix => _wireFormatToken + key,
//			CacheKeyModifierMode.Suffix => key + _wireFormatToken,
//			_ => key,
//		};
//	}

//	private void UpdateLastError(string operationId, string key)
//	{
//		// NO DISTRIBUTEC CACHE
//		if (_locker is null)
//			return;

//		var res = _breaker.TryOpen(out var hasChanged);

//		if (res && hasChanged)
//		{
//			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
//				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed cache temporarily de-activated for {BreakDuration}", _options.CacheName, _options.InstanceId, operationId, key, _breaker.BreakDuration);

//			// EVENT
//			_events.OnCircuitBreakerChange(operationId, key, false);
//		}
//	}

//	public bool IsCurrentlyUsable(string? operationId, string? key)
//	{
//		var res = _breaker.IsClosed(out var hasChanged);

//		if (res && hasChanged)
//		{
//			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
//				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed cache activated again", _options.CacheName, _options.InstanceId, operationId, key);

//			// EVENT
//			_events.OnCircuitBreakerChange(operationId, key, true);
//		}

//		return res;
//	}

//	[MethodImpl(MethodImplOptions.AggressiveInlining)]
//	private void ProcessError(string operationId, string key, Exception exc, string actionDescription)
//	{
//		if (exc is SyntheticTimeoutException)
//		{
//			if (_logger?.IsEnabled(_options.DistributedCacheSyntheticTimeoutsLogLevel) ?? false)
//				_logger.Log(_options.DistributedCacheSyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] a synthetic timeout occurred while " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);

//			return;
//		}

//		UpdateLastError(operationId, key);

//		if (_logger?.IsEnabled(_options.DistributedCacheErrorsLogLevel) ?? false)
//			_logger.Log(_options.DistributedCacheErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);
//	}
//}
