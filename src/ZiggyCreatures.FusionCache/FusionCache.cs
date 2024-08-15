﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.AutoRecovery;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Reactors;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The standard implementation of <see cref="IFusionCache"/>.
/// </summary>
[DebuggerDisplay("NAME: {CacheName} - ID: {InstanceId} - DC: {HasDistributedCache} - BP: {HasBackplane}")]
public partial class FusionCache
	: IFusionCache
{
	private readonly FusionCacheOptions _options;
	private readonly string? _cacheKeyPrefix;
	private readonly ILogger<FusionCache>? _logger;
	private IFusionCacheMemoryLocker _memoryLocker;
	private MemoryCacheAccessor _mca;
	private DistributedCacheAccessor? _dca;
	private IFusionCacheSerializer? _serializer;
	private BackplaneAccessor? _bpa;
	private readonly object _backplaneLock = new object();
	private AutoRecoveryService? _autoRecovery;
	private readonly object _autoRecoveryLock = new object();
	private FusionCacheEventsHub _events;
	private List<IFusionCachePlugin>? _plugins;
	private readonly object _pluginsLock = new object();

	private FusionCacheEntryOptions? _tryUpdateOptions;

	/// <summary>
	/// Creates a new <see cref="FusionCache"/> instance.
	/// </summary>
	/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
	/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use. If null, one will be automatically created and managed.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	/// <param name="reactor">The <see cref="IFusionCacheReactor"/> instance to use (advanced). If null, a standard one will be automatically created and managed.</param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please stop using this constructor, it will be removed in future versions.")]
	public FusionCache(IOptions<FusionCacheOptions> optionsAccessor, IMemoryCache? memoryCache, ILogger<FusionCache>? logger, IFusionCacheReactor? reactor)
		: this(optionsAccessor, memoryCache, logger, (IFusionCacheMemoryLocker?)null)
	{
		// EMPTY
	}

	/// <summary>
	/// Creates a new <see cref="FusionCache"/> instance.
	/// </summary>
	/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
	/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use. If null, one will be automatically created and managed.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	/// <param name="memoryLocker">The <see cref="IFusionCacheMemoryLocker"/> instance to use. If <see langword="null"/>, a standard one will be automatically created and managed.</param>
	public FusionCache(IOptions<FusionCacheOptions> optionsAccessor, IMemoryCache? memoryCache = null, ILogger<FusionCache>? logger = null, IFusionCacheMemoryLocker? memoryLocker = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// DUPLICATE OPTIONS (TO AVOID EXTERNAL MODIFICATIONS)
		_options = _options.Duplicate();

		// GLOBALLY UNIQUE INSTANCE ID
		if (string.IsNullOrWhiteSpace(_options.InstanceId))
		{
			_options.SetInstanceId(Guid.NewGuid().ToString("N"));
			//_options.SetInstanceId(FusionCacheInternalUtils.GenerateOperationId());
		}
		InstanceId = _options.InstanceId!;

		// CACHE KEY PREFIX
		if (string.IsNullOrEmpty(_options.CacheKeyPrefix) == false)
			_cacheKeyPrefix = _options.CacheKeyPrefix;

		// LOGGING
		if (logger is NullLogger<FusionCache>)
		{
			// IGNORE NULL LOGGER (FOR BETTER PERF)
			_logger = null;
		}
		else
		{
			_logger = logger;
		}

		// MEMORY LOCKER
		_memoryLocker = memoryLocker ?? new StandardMemoryLocker();

		// EVENTS
		_events = new FusionCacheEventsHub(this, _options, _logger);

		// PLUGINS
		_plugins = [];

		// MEMORY CACHE
		_mca = new MemoryCacheAccessor(memoryCache, _options, _logger, _events.Memory);

		// DISTRIBUTED CACHE
		_dca = null;

		// BACKPLANE
		_bpa = null;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: instance created", CacheName, InstanceId);

		// MICRO OPTIMIZATION: WARM UP OBSERVABILITY STUFF
		_ = Activities.Source;
		_ = Metrics.Meter;
	}

	/// <inheritdoc/>
	public string CacheName
	{
		get { return _options.CacheName; }
	}

	/// <inheritdoc/>
	public string InstanceId { get; private set; }

	/// <inheritdoc/>
	public FusionCacheEntryOptions DefaultEntryOptions
	{
		get { return _options.DefaultEntryOptions; }
	}

	internal AutoRecoveryService AutoRecovery
	{
		get
		{
			if (_autoRecovery is null)
			{
				lock (_autoRecoveryLock)
				{
					if (_autoRecovery is null)
					{
						_autoRecovery = new AutoRecoveryService(this, _options, _logger);
					}
				}
			}

			return _autoRecovery;
		}
	}

	internal FusionCacheEntryOptions TryUpdateOptions
	{
		get
		{
			return _tryUpdateOptions ??= new FusionCacheEntryOptions()
			{
				DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
				DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
				AllowBackgroundDistributedCacheOperations = false,
				ReThrowDistributedCacheExceptions = true,
				ReThrowSerializationExceptions = true,
			};
		}
	}

	/// <inheritdoc/>
	public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
	{
		var res = _options.DefaultEntryOptions.Duplicate(duration);
		setupAction?.Invoke(res);
		return res;
	}

	private static void ValidateCacheKey(string key)
	{
		if (key is null)
			throw new ArgumentNullException(nameof(key));
	}

	private void MaybePreProcessCacheKey(ref string key)
	{
		if (_cacheKeyPrefix is not null)
			key = _cacheKeyPrefix + key;
	}

	private string MaybeGenerateOperationId()
	{
		return FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
	}

	internal MemoryCacheAccessor GetCurrentMemoryAccessor()
	{
		return _mca;
	}

	internal MemoryCacheAccessor? GetCurrentMemoryAccessor(FusionCacheEntryOptions options)
	{
		return options.SkipMemoryCache ? null : _mca;
	}

	internal DistributedCacheAccessor? GetCurrentDistributedAccessor(FusionCacheEntryOptions? options)
	{
		if (options is null)
			return _dca;

		return options.SkipDistributedCache ? null : _dca;
	}

	internal BackplaneAccessor? GetCurrentBackplaneAccessor(FusionCacheEntryOptions? options)
	{
		if (options is null)
			return _bpa;

		return options.SkipBackplaneNotifications ? null : _bpa;
	}

	private IFusionCacheMemoryEntry? TryActivateFailSafe<TValue>(string operationId, string key, FusionCacheDistributedEntry<TValue>? distributedEntry, IFusionCacheMemoryEntry? memoryEntry, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions options)
	{
		// FAIL-SAFE NOT ENABLED
		if (options.IsFailSafeEnabled == false)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE not enabled", CacheName, InstanceId, operationId, key);

			return null;
		}

		// FAIL-SAFE ENABLED
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to activate FAIL-SAFE", CacheName, InstanceId, operationId, key);

		IFusionCacheMemoryEntry? entry = null;

		if (distributedEntry is not null && (memoryEntry is null || distributedEntry.Timestamp > memoryEntry.Timestamp))
		{
			// TRY WITH DISTRIBUTED CACHE ENTRY
			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from distributed)", CacheName, InstanceId, operationId, key);

			//entry = FusionCacheMemoryEntry<TValue>.CreateFromOtherEntry(distributedEntry, options);
			entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(distributedEntry.GetValue<TValue>(), options, true, distributedEntry.Metadata?.LastModified, distributedEntry.Metadata?.ETag, distributedEntry.Timestamp);
		}
		else if (memoryEntry is not null && memoryEntry.Metadata is not null)
		{
			// TRY WITH MEMORY CACHE ENTRY
			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from memory)", CacheName, InstanceId, operationId, key);

			var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(options.FailSafeThrottleDuration, options, true);
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(true, options.EagerRefreshThreshold, exp);

			_logger?.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): SHIFTING A MEMORY ENTRY FROM {OldExp} TO {NewExp} ({Diff} DIFF)", CacheName, InstanceId, operationId, key, memoryEntry.Metadata.LogicalExpiration, exp, exp - memoryEntry.Metadata.LogicalExpiration);

			memoryEntry.Metadata.IsFromFailSafe = true;
			memoryEntry.Metadata.LogicalExpiration = exp;
			memoryEntry.Metadata.EagerExpiration = eagerExp;
			entry = memoryEntry;
		}
		else if (failSafeDefaultValue.HasValue)
		{
			// TRY WITH FAIL-SAFE DEFAULT VALUE
			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from fail-safe default value)", CacheName, InstanceId, operationId, key);

			entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(failSafeDefaultValue.Value, options, true, null, null, null);
		}

		if (entry is not null)
		{
			// EVENT
			_events.OnFailSafeActivate(operationId, key);

			return entry;
		}

		// UNABLE TO ACTIVATE FAIL-SAFE
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): unable to activate FAIL-SAFE (no entries in memory or distributed, nor fail-safe default value)", CacheName, InstanceId, operationId, key);

		return null;
	}

	private void MaybeBackgroundCompleteTimedOutFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue>? factoryTask, FusionCacheEntryOptions options, Activity? activity)
	{
		if (factoryTask is null)
		{
			// ACTIVITY
			activity?.Dispose();

			return;
		}

		if (factoryTask.IsFaulted)
		{
			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, factoryTask.Exception?.Message);
			activity?.Dispose();

			return;
		}

		if (options.AllowTimedOutFactoryBackgroundCompletion == false)
		{
			// ACTIVITY
			activity?.AddEvent(new ActivityEvent(Activities.EventNames.FactoryBackgroundMoveNotAllowed));
			activity?.Dispose();

			return;
		}

		activity?.AddEvent(new ActivityEvent(Activities.EventNames.FactoryBackgroundMove));
		CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, null, activity);
	}

	private void CompleteBackgroundFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue> factoryTask, FusionCacheEntryOptions options, object? memoryLockObj, Activity? activity)
	{
		if (factoryTask.IsFaulted)
		{
			try
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, factoryTask.Exception?.GetSingleInnerExceptionOrSelf(), "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a background factory thrown an exception", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.OnBackgroundFactoryError(operationId, key);
			}
			finally
			{
				// MEMORY LOCK
				if (memoryLockObj is not null)
					ReleaseMemoryLock(operationId, key, memoryLockObj);

				// ACTIVITY
				activity?.SetStatus(ActivityStatusCode.Error, factoryTask.Exception?.Message);
				activity?.Dispose();
			}

			return;
		}

		// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to complete a background factory", CacheName, InstanceId, operationId, key);

		_ = factoryTask.ContinueWith(async antecedent =>
		{
			try
			{
				if (antecedent.Status == TaskStatus.Faulted)
				{
					if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
						_logger.Log(_options.FactoryErrorsLogLevel, antecedent.Exception?.GetSingleInnerExceptionOrSelf(), "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a background factory thrown an exception", CacheName, InstanceId, operationId, key);

					// ACTIVITY
					activity?.SetStatus(ActivityStatusCode.Error, factoryTask.Exception?.Message);
					activity?.Dispose();

					// EVENT
					_events.OnBackgroundFactoryError(operationId, key);
				}
				else if (antecedent.Status == TaskStatus.RanToCompletion)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a background factory successfully completed, keeping the result", CacheName, InstanceId, operationId, key);

					// ACTIVITY
					activity?.Dispose();

					// UPDATE ADAPTIVE OPTIONS
					var maybeNewOptions = ctx.GetOptions();
					if (ReferenceEquals(options, maybeNewOptions) == false)
					{
						options = maybeNewOptions;
					}
					else
					{
						options = options.Duplicate();
					}

					options.AllowBackgroundDistributedCacheOperations = false;
					options.AllowBackgroundBackplaneOperations = false;
					options.ReThrowDistributedCacheExceptions = false;
					options.ReThrowSerializationExceptions = false;
					options.ReThrowBackplaneExceptions = false;

					// ADAPTIVE CACHING UPDATE
					var lateEntry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(antecedent.Result, options, false, ctx.LastModified, ctx.ETag, null);

					var mca = GetCurrentMemoryAccessor(options);
					if (mca is not null)
					{
						mca.SetEntry<TValue>(operationId, key, lateEntry, options);
					}

					if (RequiresDistributedOperations(options))
					{
						await DistributedSetEntryAsync<TValue>(operationId, key, lateEntry, options, default).ConfigureAwait(false);
					}

					// EVENT
					_events.OnBackgroundFactorySuccess(operationId, key);
					_events.OnSet(operationId, key);
				}
			}
			finally
			{
				// MEMORY LOCK
				if (memoryLockObj is not null)
					ReleaseMemoryLock(operationId, key, memoryLockObj);
			}
		});
	}

	private async ValueTask<object?> AcquireMemoryLockAsync(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", CacheName, InstanceId, operationId, key);

		var lockObj = await _memoryLocker.AcquireLockAsync(CacheName, InstanceId, operationId, key, timeout, _logger, token);

		if (lockObj is not null)
		{
			// LOCK ACQUIRED
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", CacheName, InstanceId, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK timeout", CacheName, InstanceId, operationId, key);
		}

		return lockObj;
	}

	private object? AcquireMemoryLock(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): waiting to acquire the LOCK", CacheName, InstanceId, operationId, key);

		var lockObj = _memoryLocker.AcquireLock(CacheName, InstanceId, operationId, key, timeout, _logger, token);

		if (lockObj is not null)
		{
			// LOCK ACQUIRED
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK acquired", CacheName, InstanceId, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): LOCK timeout", CacheName, InstanceId, operationId, key);
		}

		return lockObj;
	}

	private void ReleaseMemoryLock(string operationId, string key, object? lockObj)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): releasing MEMORY LOCK", CacheName, InstanceId, operationId, key);

		try
		{
			_memoryLocker.ReleaseLock(CacheName, InstanceId, operationId, key, lockObj, _logger);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): MEMORY LOCK released", CacheName, InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): releasing the MEMORY LOCK has thrown an exception", CacheName, InstanceId, operationId, key);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessFactoryError(string operationId, string key, Exception exc)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.FactorySyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.FactorySyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while calling the factory", CacheName, InstanceId, operationId, key);

			// EVENT
			_events.OnFactorySyntheticTimeout(operationId, key);

			return;
		}

		if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
			_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling the factory", CacheName, InstanceId, operationId, key);

		// EVENT
		_events.OnFactoryError(operationId, key);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessFactoryError(string operationId, string key, string errorMessage)
	{
		if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
			_logger.Log(_options.FactoryErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling the factory: {ErrorMessage}", CacheName, InstanceId, operationId, key, errorMessage);

		// EVENT
		_events.OnFactoryError(operationId, key);
	}

	internal bool MaybeExpireMemoryEntryInternal(string operationId, string key, bool allowFailSafe, long? timestampThreshold)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling MaybeExpireMemoryEntryInternal (allowFailSafe={AllowFailSafe}, timestampThreshold={TimestampThreshold})", CacheName, InstanceId, operationId, key, allowFailSafe, timestampThreshold);

		return _mca.ExpireEntry(operationId, key, allowFailSafe, timestampThreshold);
	}

	/// <inheritdoc/>
	public IFusionCache SetupSerializer(IFusionCacheSerializer serializer)
	{
		if (serializer is null)
			throw new ArgumentNullException(nameof(serializer));

		_serializer = serializer;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup serializer (SERIALIZER={SerializerType})", CacheName, InstanceId, _serializer.GetType().FullName);

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache)
	{
		if (distributedCache is null)
			throw new ArgumentNullException(nameof(distributedCache));

		if (_serializer is null)
			throw new InvalidOperationException("The serializer must be set before setting up the distributed cache");

		_dca = new DistributedCacheAccessor(distributedCache, _serializer, _options, _logger, _events.Distributed);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup distributed cache (CACHE={DistributedCacheType})", CacheName, InstanceId, distributedCache.GetType().FullName);

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer)
	{
		SetupSerializer(serializer);
		SetupDistributedCache(distributedCache);

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveDistributedCache()
	{
		if (_dca is not null)
		{
			_dca = null;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: distributed cache removed", CacheName, InstanceId);
		}

		return this;
	}

	/// <inheritdoc/>
	public bool HasDistributedCache
	{
		get { return _dca is not null; }
	}

	/// <inheritdoc/>
	public IFusionCache SetupBackplane(IFusionCacheBackplane backplane)
	{
		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		if (_bpa is not null)
		{
			RemoveBackplane();
		}

		lock (_backplaneLock)
		{
			_bpa = new BackplaneAccessor(this, backplane, _options, _logger);
		}

		RunUtils.RunSyncActionAdvanced(
			_ =>
			{
				lock (_backplaneLock)
				{
					_bpa.Subscribe();

					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup backplane (BACKPLANE={BackplaneType})", CacheName, InstanceId, backplane.GetType().FullName);
				}
			},
			Timeout.InfiniteTimeSpan,
			false,
			DefaultEntryOptions.AllowBackgroundBackplaneOperations == false,
			null,
			false
		);

		// CHECK: WARN THE USER IN CASE OF
		// - HAS A MEMORY CACHE (ALWAYS)
		// - HAS A BACKPLANE
		// - DOES *NOT* HAVE A DISTRIBUTED CACHE
		// - THE OPTION DefaultEntryOptions.SkipBackplaneNotifications IS FALSE
		if (HasBackplane && HasDistributedCache == false && DefaultEntryOptions.SkipBackplaneNotifications == false)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, "FUSION [N={CacheName} I={CacheInstanceId}]: it has been detected a situation where there *IS* a backplane (BACKPLANE={BackplaneType}), there is *NOT* a distributed cache and the DefaultEntryOptions.SkipBackplaneNotifications option is set to false. This will probably cause problems, since a notification will be sent automatically at every change in the cache (Set, Remove, Expire and also GetOrSet when the factory is called) but there is not a distributed cache that different nodes can use, basically resulting in a situation where the cache will keep invalidating itself at every change. It is suggested to either (1) add a distributed cache or (2) change the DefaultEntryOptions.SkipBackplaneNotifications to true.", CacheName, InstanceId, backplane.GetType().FullName);
		}

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveBackplane()
	{
		lock (_backplaneLock)
		{
			if (_bpa is not null)
			{
				_bpa.Unsubscribe();
				_bpa = null;

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: backplane removed", CacheName, InstanceId);
			}
		}

		return this;
	}

	/// <inheritdoc/>
	public bool HasBackplane
	{
		get { return _bpa is not null; }
	}

	/// <inheritdoc/>
	public FusionCacheEventsHub Events { get { return _events; } }

	/// <inheritdoc/>
	public void AddPlugin(IFusionCachePlugin plugin)
	{
		if (plugin is null)
			throw new ArgumentNullException(nameof(plugin));

		// ADD THE PLUGIN
		lock (_pluginsLock)
		{
			_plugins ??= [];

			if (_plugins.Contains(plugin))
			{
				if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
					_logger?.Log(_options.PluginsErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}]: the same plugin instance already exists (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);

				throw new InvalidOperationException($"FUSION [N={CacheName}]: the same plugin instance already exists (TYPE={plugin.GetType().FullName})");
			}

			_plugins.Add(plugin);
		}

		// START THE PLUGIN
		try
		{
			plugin.Start(this);
		}
		catch (Exception exc)
		{
			lock (_pluginsLock)
			{
				_plugins.Remove(plugin);
			}

			if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
				_logger.Log(_options.PluginsErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}]: an error occurred while starting a plugin (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);

			throw new InvalidOperationException($"FUSION [N={CacheName}]: an error occurred while starting a plugin (TYPE={plugin.GetType().FullName})", exc);
		}

		if (_logger?.IsEnabled(_options.PluginsInfoLogLevel) ?? false)
			_logger?.Log(_options.PluginsInfoLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}]: a plugin has been added and started (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);
	}

	/// <inheritdoc/>
	public bool RemovePlugin(IFusionCachePlugin plugin)
	{
		if (plugin is null)
			throw new ArgumentNullException(nameof(plugin));

		lock (_pluginsLock)
		{
			_plugins ??= [];

			if (_plugins.Contains(plugin) == false)
			{
				if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
					_logger?.Log(_options.PluginsErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}]: the plugin cannot be removed because is not part of this FusionCache instance (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);

				// MAYBE WE SHOULD THROW (LIKE IN AddPlugin) INSTEAD OF JUST RETURNING (LIKE IN List<T>.Remove()) ?
				return false;
				//throw new InvalidOperationException($"FUSION [N={CacheName}]: the plugin cannot be removed because is not part of this FusionCache instance (TYPE={plugin.GetType().FullName})");
			}

			// STOP THE PLUGIN
			try
			{
				plugin.Stop(this);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
					_logger.Log(_options.PluginsErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}]: an error occurred while stopping a plugin (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);

				throw new InvalidOperationException($"FUSION [N={CacheName}]: an error occurred while stopping a plugin (TYPE={plugin.GetType().FullName})", exc);
			}
			finally
			{
				// REMOVE THE PLUGIN
				_plugins.Remove(plugin);
			}
		}

		if (_logger?.IsEnabled(_options.PluginsInfoLogLevel) ?? false)
			_logger?.Log(_options.PluginsInfoLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}]: a plugin has been stopped and removed (TYPE={PluginType})", CacheName, InstanceId, plugin.GetType().FullName);

		return true;
	}

	private void RemoveAllPlugins()
	{
		if (_plugins is null)
			return;

		foreach (var plugin in _plugins.ToArray())
		{
			RemovePlugin(plugin);
		}
	}

	// IDISPOSABLE
	private bool _disposedValue = false;
	/// <summary>
	/// Release all resources managed by FusionCache.
	/// </summary>
	/// <param name="disposing">Indicates if the disposing is happening.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				RemoveAllPlugins();
				RemoveBackplane();
				RemoveDistributedCache();

				_autoRecovery?.Dispose();
				_autoRecovery = null;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
				_memoryLocker.Dispose();
				_memoryLocker = null;

				_mca.Dispose();
				_mca = null;

				_events = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			}

			_disposedValue = true;
		}
	}

	/// <summary>
	/// Release all resources managed by FusionCache.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	internal bool RequiresDistributedOperations(FusionCacheEntryOptions options)
	{
		if (HasDistributedCache && options.SkipDistributedCache == false)
			return true;

		if (HasBackplane && options.SkipBackplaneNotifications == false)
			return true;

		return false;
	}

	internal bool MustAwaitDistributedOperations(FusionCacheEntryOptions options)
	{
		if (HasDistributedCache && options.AllowBackgroundDistributedCacheOperations == false)
			return true;

		if (HasDistributedCache == false && HasBackplane && options.AllowBackgroundBackplaneOperations == false)
			return true;

		return false;
	}

	internal bool MustAwaitBackplaneOperations(FusionCacheEntryOptions options)
	{
		if (HasBackplane && options.AllowBackgroundBackplaneOperations == false)
			return true;

		return false;
	}

	private void UpdateAdaptiveOptions<TValue>(FusionCacheFactoryExecutionContext<TValue> ctx, ref FusionCacheEntryOptions options, ref DistributedCacheAccessor? dca, ref MemoryCacheAccessor? mca)
	{
		// UPDATE ADAPTIVE OPTIONS
		var maybeNewOptions = ctx.GetOptions();

		if (ReferenceEquals(options, maybeNewOptions))
			return;

		options = maybeNewOptions;

		dca = GetCurrentDistributedAccessor(options);
		mca = GetCurrentMemoryAccessor(options);
	}

	internal async ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryAsync<TValue>(string operationId, string key, FusionCacheMemoryEntry<TValue> memoryEntry)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to update memory entry from distributed entry", CacheName, InstanceId, operationId, key);

		try
		{
			if (HasDistributedCache == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because there's no distributed cache", CacheName, InstanceId, operationId, key);

				return (false, false, false);
			}

			var dca = GetCurrentDistributedAccessor(null);

			if (dca is null)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because distributed cache is not enabled for the current operation", CacheName, InstanceId, operationId, key);

				return (false, false, false);
			}

			if (dca.IsCurrentlyUsable(operationId, key) == false)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): cannot update memory from distributed because distributed cache is not currently usable", CacheName, InstanceId, operationId, key);

				return (true, false, false);
			}

			try
			{
				var (distributedEntry, isValid) = await dca.TryGetEntryAsync<TValue>(operationId, key, TryUpdateOptions, false, Timeout.InfiniteTimeSpan, default).ConfigureAwait(false);

				if (distributedEntry is null || isValid == false)
				{
					//_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, null);
					//return;

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): distributed entry not found or stale, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, false, false);
				}

				if (/*distributedEntry.Timestamp is not null &&*/ distributedEntry.Timestamp == memoryEntry.Timestamp)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry same as distributed entry, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, true, false);
				}

				if (distributedEntry.Timestamp < memoryEntry.Timestamp)
				{
					//return;
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry more fresh than distributed entry, do not update memory entry", CacheName, InstanceId, operationId, key);

					return (false, false, false);
				}

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): updating memory entry from distributed entry", CacheName, InstanceId, operationId, key);


				_mca.UpdateEntryFromDistributedEntry(operationId, key, memoryEntry, distributedEntry);
				//memoryEntry.UpdateFromDistributedEntry(distributedEntry);

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry updated from distributed", CacheName, InstanceId, operationId, key);

				// EVENT
				_events.Memory.OnSet(operationId, key);

				return (false, false, true);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to update a memory entry from a distributed entry", CacheName, InstanceId, operationId, key);

				//MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, null);

				return (true, false, false);
			}
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling TryUpdateMemoryEntryFromDistributedEntryUntypedAsync() to try to update a memory entry from a distributed entry without knowing the TValue type", CacheName, InstanceId, operationId, key);

			return (true, false, false);
		}
	}

	internal TValue GetValueFromMemoryEntry<TValue>(string operationId, string key, IFusionCacheMemoryEntry entry, FusionCacheEntryOptions? options)
	{
		if (entry is null)
			throw new ArgumentNullException(nameof(entry));

		options ??= _options.DefaultEntryOptions;

		if (options.EnableAutoClone == false)
			return entry.GetValue<TValue>();

		if (_serializer is null)
			throw new InvalidOperationException($"A serializer is needed when using {nameof(FusionCacheEntryOptions.EnableAutoClone)}.");

		if (entry.Value is null)
			return entry.GetValue<TValue>();

		byte[] serializedValue;
		try
		{
			serializedValue = entry.GetSerializedValue(_serializer);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while serializing a value", _options.CacheName, _options.InstanceId, operationId, key);

			// EVENT
			_events.Distributed.OnSerializationError(operationId, key);

			if (_options.ReThrowOriginalExceptions)
			{
				throw;
			}
			else
			{
				throw new FusionCacheSerializationException("An error occurred while serializing a value", exc);
			}
		}

		try
		{
			return _serializer.Deserialize<TValue>(serializedValue)!;
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while deserializing a value", _options.CacheName, _options.InstanceId, operationId, key);

			// EVENT
			_events.Distributed.OnDeserializationError(operationId, key);

			if (_options.ReThrowOriginalExceptions)
			{
				throw;
			}
			else
			{
				throw new FusionCacheSerializationException("An error occurred while deserializing a value", exc);
			}
		}
	}
}
