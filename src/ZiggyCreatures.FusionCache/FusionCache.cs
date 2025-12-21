using System.Diagnostics;
using System.Runtime.CompilerServices;
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
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The standard implementation of <see cref="IFusionCache"/>.
/// </summary>
[DebuggerDisplay("NAME: {CacheName} - ID: {InstanceId} - DC: {HasDistributedCache} - BP: {HasBackplane}")]
public sealed partial class FusionCache
	: IFusionCache
{
	private readonly FusionCacheOptions _options;
	private readonly string? _cacheKeyPrefix;
	private readonly ILogger<FusionCache>? _logger;
	internal readonly FusionCacheEntryOptions _defaultEntryOptions;
	internal readonly FusionCacheEntryOptionsProvider? _defaultEntryOptionsProvider;
	internal FusionCacheEntryOptionsProviderContext _defaultEntryOptionsProviderContext;
	internal readonly FusionCacheEntryOptions _tryUpdateEntryOptions;

	// MEMORY LOCKER
	private IFusionCacheMemoryLocker _memoryLocker;

	// DISTRIBUTED LOCKER
	private IFusionCacheDistributedLocker? _distributedLocker;

	// MEMORY CACHE
	private MemoryCacheAccessor _mca;
	private readonly bool _mcaCanClear;

	// DISTRIBUTED CACHE
	private DistributedCacheAccessor? _dca;
	private IFusionCacheSerializer? _serializer;

	// BACKPLANE
	private BackplaneAccessor? _bpa;
	private readonly object _backplaneLock = new();

	// AUTO-RECOVERY
	private AutoRecoveryService? _autoRecovery;
	private readonly object _autoRecoveryLock = new();

	// EVENTS
	private FusionCacheEventsHub _events;

	// PLUGINS
	private List<IFusionCachePlugin>? _plugins;
	private readonly object _pluginsLock = new();

	// TAGGING
	private readonly FusionCacheEntryOptions _tagsDefaultEntryOptions;
	private readonly FusionCacheEntryOptions _cascadeRemoveByTagEntryOptions;

	internal readonly string TagInternalCacheKeyPrefix;

	internal readonly string ClearRemoveTagCacheKey;
	internal readonly string ClearRemoveTagInternalCacheKey;
	internal long ClearRemoveTimestamp;

	internal readonly string ClearExpireTagCacheKey;
	internal readonly string ClearExpireTagInternalCacheKey;
	internal long ClearExpireTimestamp;

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
		_options = optionsAccessor.Value;

		if (_options is null)
			throw new NullReferenceException($"No options have been provided via {nameof(optionsAccessor.Value)}.");

		// DUPLICATE OPTIONS (TO AVOID EXTERNAL MODIFICATIONS)
		_options = _options.Duplicate();

		_defaultEntryOptions = _options.DefaultEntryOptions;
		_defaultEntryOptionsProvider = _options.DefaultEntryOptionsProvider;
		if (_defaultEntryOptionsProvider is not null)
		{
			_defaultEntryOptionsProviderContext = new FusionCacheEntryOptionsProviderContext(this);
		}
		else
		{
			// NOTE: SINCE THERE IS NO PROVIDER, WE CAN SAFELY SET THIS TO NULL (IT WILL NOT BE USED)
			_defaultEntryOptionsProviderContext = null!;
		}

		// TRY UPDATE OPTIONS
		_tryUpdateEntryOptions = new FusionCacheEntryOptions
		{
			DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
			DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
			AllowBackgroundDistributedCacheOperations = false,
			ReThrowDistributedCacheExceptions = false,
			ReThrowSerializationExceptions = false,
			ReThrowBackplaneExceptions = false,
			SkipMemoryCacheRead = false,
			SkipMemoryCacheWrite = false,
			SkipDistributedCacheRead = false,
			SkipDistributedCacheWrite = false,
			SkipBackplaneNotifications = false,
		};

		// TAGGING
		_tagsDefaultEntryOptions = _options.TagsDefaultEntryOptions;
		_cascadeRemoveByTagEntryOptions = new FusionCacheEntryOptions
		{
			Duration = TimeSpan.FromHours(24),
			IsFailSafeEnabled = true,
			FailSafeThrottleDuration = TimeSpan.FromSeconds(30),
			FailSafeMaxDuration = TimeSpan.FromHours(24),
			AllowBackgroundDistributedCacheOperations = false,
			AllowBackgroundBackplaneOperations = false,
			ReThrowDistributedCacheExceptions = false,
			ReThrowSerializationExceptions = false,
			ReThrowBackplaneExceptions = false,
			SkipMemoryCacheRead = false,
			SkipMemoryCacheWrite = false,
			SkipDistributedCacheRead = false,
			SkipDistributedCacheWrite = false,
			SkipBackplaneNotifications = false,
			Priority = CacheItemPriority.NeverRemove,
			Size = 1
		};

		// GLOBALLY UNIQUE INSTANCE ID
		if (string.IsNullOrWhiteSpace(_options.InstanceId))
		{
			_options.SetInstanceIdInternal(FusionCacheInternalUtils.GenerateOperationId());
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
		_mcaCanClear = _mca.CanClear;

		// DISTRIBUTED CACHE
		_dca = null;

		// BACKPLANE
		_bpa = null;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: instance created", CacheName, InstanceId);

		TagInternalCacheKeyPrefix = GetTagInternalCacheKey("");

		ClearRemoveTimestamp = -1;
		ClearRemoveTagCacheKey = GetTagCacheKey(_options.InternalStrings.ClearRemoveTag);
		ClearRemoveTagInternalCacheKey = GetTagInternalCacheKey(_options.InternalStrings.ClearRemoveTag);

		ClearExpireTimestamp = -1;
		ClearExpireTagCacheKey = GetTagCacheKey(_options.InternalStrings.ClearExpireTag);
		ClearExpireTagInternalCacheKey = GetTagInternalCacheKey(_options.InternalStrings.ClearExpireTag);

		// MICRO OPTIMIZATION: WARM UP OBSERVABILITY STUFF
		_ = Activities.Source;
		_ = Metrics.Meter;

		if (_options.CheckBestPracticesOnStartup)
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(1000);
				CheckBestPractices();
			});
		}
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
		get { return _defaultEntryOptions; }
	}

	/// <inheritdoc/>
	public FusionCacheEntryOptionsProvider? DefaultEntryOptionsProvider
	{
		get { return _defaultEntryOptionsProvider; }
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

	internal FusionCacheEntryOptionsProviderContext DefaultEntryOptionsProviderContext
	{
		get { return _defaultEntryOptionsProviderContext; }
	}

	/// <inheritdoc/>
	public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
	{
		var res = _defaultEntryOptions.Duplicate(duration);
		setupAction?.Invoke(res);
		return res;
	}

	private void CheckDisposed()
	{
		if (_disposedValue)
		{
			throw new ObjectDisposedException("The FusionCache instance has been disposed and cannot be used anymore.", (Exception?)null);
		}
	}

	private static void ValidateCacheKey(string key)
	{
		if (key is null)
			throw new ArgumentNullException(nameof(key));
	}

	private static void ValidateTag(string tag)
	{
		if (tag is null)
			throw new ArgumentNullException(nameof(tag));

		// TODO: SHOULD WE KEEP THIS CHECK, AND SOMEHOW BYPASS IT INTERNALLY?
		//if (tag == ClearTag)
		//	throw new ArgumentOutOfRangeException(nameof(tag), $"The tag '{ClearTag}' is reserved and cannot be used.");
	}

	private void ValidateTags(string[]? tags)
	{
		if (tags is null || tags.Length == 0)
			return;

		CheckTaggingEnabled();

		foreach (var tag in tags)
		{
			ValidateTag(tag);
		}
	}

	private void MaybePreProcessCacheKey(ref string key)
	{
		if (_cacheKeyPrefix is not null)
			key = _cacheKeyPrefix + key;
	}

	private void MaybePreProcessCacheKey(ref string key, out string originalKey)
	{
		originalKey = key;
		if (_cacheKeyPrefix is not null)
			key = _cacheKeyPrefix + key;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string MaybeGenerateOperationId()
	{
		return FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
	}

	// MEMORY ACCESSOR

	internal MemoryCacheAccessor MemoryCacheAccessor
	{
		get { return _mca; }
	}

	// DISTRIBUTED ACCESSOR

	internal DistributedCacheAccessor? DistributedCacheAccessor
	{
		get { return _dca; }
	}

	// BACKPLANE ACCESSOR

	internal BackplaneAccessor? BackplaneAccessor
	{
		get { return _bpa; }
	}

	// FAIL-SAFE

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
			entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(distributedEntry.GetValue<TValue>(), null, distributedEntry.Timestamp, distributedEntry.Tags, options, true, distributedEntry.Metadata?.LastModifiedTimestamp, distributedEntry.Metadata?.ETag);
		}
		else if (memoryEntry is not null && memoryEntry.Metadata is not null)
		{
			// TRY WITH MEMORY CACHE ENTRY
			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from memory)", CacheName, InstanceId, operationId, key);

			var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpirationTimestamp(options.FailSafeThrottleDuration, options, false);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger?.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): SHIFTING A MEMORY ENTRY FROM {OldExp} TO {NewExp} ({Diff} DIFF)", CacheName, InstanceId, operationId, key, new DateTimeOffset(memoryEntry.LogicalExpirationTimestamp, TimeSpan.Zero), new DateTimeOffset(exp, TimeSpan.Zero), new DateTimeOffset(exp, TimeSpan.Zero) - new DateTimeOffset(memoryEntry.LogicalExpirationTimestamp, TimeSpan.Zero));

			memoryEntry.Metadata.IsStale = true;
			memoryEntry.LogicalExpirationTimestamp = exp;
			memoryEntry.Metadata.EagerExpirationTimestamp = null;
			entry = memoryEntry;
		}
		else if (failSafeDefaultValue.HasValue)
		{
			// TRY WITH FAIL-SAFE DEFAULT VALUE
			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from fail-safe default value)", CacheName, InstanceId, operationId, key);

			entry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(failSafeDefaultValue.Value, null, null, null, options, true, null, null);
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

	// BACKGROUND FACTORY COMPLETION

	private void MaybeBackgroundCompleteFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue>? factoryTask, FusionCacheEntryOptions options, ref object? memoryLockObj, ref object? distributedLockObj, Activity? activity)
	{
		if (factoryTask is null)
		{
			// ACTIVITY
			activity?.Dispose();

			return;
		}

		if (factoryTask.IsFaulted || factoryTask.IsCanceled || ctx.HasFailed)
		{
			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, factoryTask.Exception?.Message ?? ctx.ErrorMessage ?? "An error occurred while running the factory");
			if (factoryTask.Exception is not null)
				activity?.AddException(factoryTask.Exception);
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
		var tmpMemoryLockObj = memoryLockObj;
		memoryLockObj = null;
		var tmpDistributedLockObj = distributedLockObj;
		distributedLockObj = null;
		BackgroundCompleteFactory<TValue>(operationId, key, ctx, factoryTask, options, tmpMemoryLockObj, tmpDistributedLockObj, activity);
	}

	private void BackgroundCompleteFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue> factoryTask, FusionCacheEntryOptions options, object? memoryLockObj, object? distributedLockObj, Activity? activity)
	{
		_ = Task.Run(async () =>
		{
			try
			{
				// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): trying to complete a background factory", CacheName, InstanceId, operationId, key);

				var result = await factoryTask.ConfigureAwait(false);

				if (ctx.HasFailed)
				{
					// FAIL

					var errorMessage = ctx.ErrorMessage ?? "An error occurred while executing the background factory";

					if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
						_logger.Log(_options.FactoryErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while executing the background factory: {ErrorMessage}", CacheName, InstanceId, operationId, key, errorMessage);

					// ACTIVITY
					activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
					activity?.Dispose();

					// EVENT
					_events.OnBackgroundFactoryError(operationId, key);
				}
				else
				{
					// SUCCESS

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
					var lateEntry = FusionCacheMemoryEntry<TValue>.CreateFromOptions(result, GetSerializedValueFromValue(operationId, key, result, options), null, ctx.Tags, options, false, ctx.LastModified?.UtcTicks, ctx.ETag);

					if (_mca.ShouldWrite(options))
					{
						_mca.SetEntry<TValue>(operationId, key, lateEntry, options);
					}

					// MEMORY LOCK
					if (memoryLockObj is not null)
					{
						ReleaseMemoryLock(operationId, key, memoryLockObj);
						memoryLockObj = null;
					}

					if (RequiresDistributedOperations(options))
					{
						await DistributedSetEntryAsync<TValue>(operationId, key, lateEntry, options, distributedLockObj, CancellationToken.None).ConfigureAwait(false);
						distributedLockObj = null;
					}

					// EVENT
					_events.OnBackgroundFactorySuccess(operationId, key);
					_events.OnSet(operationId, key);
				}
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a background factory thrown an exception", CacheName, InstanceId, operationId, key);

				// ACTIVITY
				activity?.SetStatus(ActivityStatusCode.Error, exc.Message ?? ctx.ErrorMessage ?? "An error occurred while running the factory");
				activity?.AddException(exc);
				activity?.Dispose();

				// EVENT
				_events.OnBackgroundFactoryError(operationId, key);
			}
			finally
			{
				if (memoryLockObj is not null)
					ReleaseMemoryLock(operationId, key, memoryLockObj);

				if (distributedLockObj is not null)
					await ReleaseDistributedLockAsync(operationId, key, distributedLockObj, CancellationToken.None).ConfigureAwait(false);
			}
		});
	}

	// MEMORY LOCKER

	private async ValueTask<object?> AcquireMemoryLockAsync(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] waiting to acquire the MEMORY LOCK", CacheName, InstanceId, operationId, key);

		var lockObj = await _memoryLocker.AcquireLockAsync(CacheName, InstanceId, operationId, key, timeout, _logger, token);

		if (lockObj is not null)
		{
			// LOCK ACQUIRED
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] MEMORY LOCK acquired", CacheName, InstanceId, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] MEMORY LOCK timeout", CacheName, InstanceId, operationId, key);
		}

		return lockObj;
	}

	private object? AcquireMemoryLock(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] waiting to acquire the MEMORY LOCK", CacheName, InstanceId, operationId, key);

		var lockObj = _memoryLocker.AcquireLock(CacheName, InstanceId, operationId, key, timeout, _logger, token);

		if (lockObj is not null)
		{
			// LOCK ACQUIRED
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] MEMORY LOCK acquired", CacheName, InstanceId, operationId, key);
		}
		else
		{
			// LOCK TIMEOUT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] MEMORY LOCK timeout", CacheName, InstanceId, operationId, key);
		}

		return lockObj;
	}

	private void ReleaseMemoryLock(string operationId, string key, object? lockObj)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] releasing MEMORY LOCK", CacheName, InstanceId, operationId, key);

		try
		{
			_memoryLocker.ReleaseLock(CacheName, InstanceId, operationId, key, lockObj, _logger);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] MEMORY LOCK released", CacheName, InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [ML] releasing the MEMORY LOCK has thrown an exception", CacheName, InstanceId, operationId, key);
		}
	}

	// DISTRIBUTED LOCKER

	private async ValueTask<object?> AcquireDistributedLockAsync(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_distributedLocker is null)
			throw new InvalidOperationException("No distributed locker has been configured for this FusionCache instance.");

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] waiting to acquire the DISTRIBUTED LOCK", CacheName, InstanceId, operationId, key);

		try
		{
			var lockObj = await _distributedLocker.AcquireLockAsync(CacheName, InstanceId, operationId, "XXXABC", key, timeout, _logger, token);

			if (lockObj is not null)
			{
				// LOCK ACQUIRED
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK acquired", CacheName, InstanceId, operationId, key);
			}
			else
			{
				// LOCK TIMEOUT
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK timeout", CacheName, InstanceId, operationId, key);
			}

			return lockObj;
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] acquiring the DISTRIBUTED LOCK has thrown an exception", CacheName, InstanceId, operationId, key);

			return null;

			// TODO: WHAT DO!?
			//throw;
		}
	}

	private object? AcquireDistributedLock(string operationId, string key, TimeSpan timeout, CancellationToken token)
	{
		if (_distributedLocker is null)
			throw new InvalidOperationException("No distributed locker has been configured for this FusionCache instance.");

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] waiting to acquire the DISTRIBUTEDLOCK", CacheName, InstanceId, operationId, key);

		try
		{
			var lockObj = _distributedLocker.AcquireLock(CacheName, InstanceId, operationId, key, "XXXABC" + key, timeout, _logger, token);

			if (lockObj is not null)
			{
				// LOCK ACQUIRED
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTEDLOCK acquired", CacheName, InstanceId, operationId, key);
			}
			else
			{
				// LOCK TIMEOUT
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTEDLOCK timeout", CacheName, InstanceId, operationId, key);
			}

			return lockObj;
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] acquiring the DISTRIBUTED LOCK has thrown an exception", CacheName, InstanceId, operationId, key);

			return null;

			// TODO: WHAT DO!?
			//throw;
		}
	}

	private async ValueTask ReleaseDistributedLockAsync(string operationId, string key, object? lockObj, CancellationToken token)
	{
		if (lockObj is null)
			return;

		if (_distributedLocker is null)
			throw new InvalidOperationException("No distributed locker has been configured for this FusionCache instance.");

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing DISTRIBUTED LOCK", CacheName, InstanceId, operationId, key);

		try
		{
			await _distributedLocker.ReleaseLockAsync(CacheName, InstanceId, operationId, key, "XXXABC" + key, lockObj, _logger, token);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK released", CacheName, InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing the DISTRIBUTED LOCK has thrown an exception", CacheName, InstanceId, operationId, key);
		}
	}

	private void ReleaseDistributedLock(string operationId, string key, object? lockObj, CancellationToken token)
	{
		if (lockObj is null)
			return;

		if (_distributedLocker is null)
			throw new InvalidOperationException("No distributed locker has been configured for this FusionCache instance.");

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing DISTRIBUTED LOCK", CacheName, InstanceId, operationId, key);

		try
		{
			_distributedLocker.ReleaseLock(CacheName, InstanceId, operationId, key, "XXXABC" + key, lockObj, _logger, token);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] DISTRIBUTED LOCK released", CacheName, InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DL] releasing the DISTRIBUTED LOCK has thrown an exception", CacheName, InstanceId, operationId, key);
		}
	}

	// FACTORY STUFF

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

	internal void RemoveMemoryEntryInternal(string operationId, string key)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling RemoveMemoryEntryInternal", CacheName, InstanceId, operationId, key);

		_mca.RemoveEntry(operationId, key);
	}

	internal void ExpireMemoryEntryInternal(string operationId, string key, long? timestampThreshold)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): calling ExpireMemoryEntryInternal (timestampThreshold={TimestampThreshold})", CacheName, InstanceId, operationId, key, timestampThreshold);

		_mca.ExpireEntry(operationId, key, timestampThreshold);
	}

	// TAGGING

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CheckTaggingEnabled()
	{
		if (_options.DisableTagging)
			throw new InvalidOperationException("This operation requires Tagging, which has been disabled via FusionCacheOptions.DisableTagging.");
	}

	private string GetTagCacheKey(string tag)
	{
		return $"{_options.InternalStrings.TagCacheKeyPrefix}{tag}";
	}

	private string GetTagInternalCacheKey(string tag)
	{
		var res = GetTagCacheKey(tag);
		MaybePreProcessCacheKey(ref res);
		return res;
	}

	internal bool CanExecuteRawClear()
	{
		// CHECK: NO DISTRIBUTED CACHE
		if (HasDistributedCache)
			return false;

		// CHECK: NO BACKPLANE
		if (HasBackplane)
			return false;

		// CHECK: THE INNER MEMORY CACHE SUPPORTS CLEARING
		if (_mcaCanClear == false)
			return false;

		// NOTE: WE MAY THINK ABOUT ALSO CHECKING FOR THE USAGE OF A
		// CacheKeyPrefix, WHICH WOULD *PROBABLY* INDICATE (NOT 100%
		// SURE THOUGH) THAT THE INNER MEMORY CACHE IS BEING SHARED
		// WITH OTHER INSTANCES.
		// THIS IS NOT A STRONG ENOUGH SIGNAL THOUGH, SO IT CANNOT BE USED.
		// 
		// ALSO, WE ALREADY CHECKED, VIA _mca.CanClear(), THAT THE INTERNAL
		// MEMORY CACHE CAN BE CLEARED, WHICH IN TURN ALSO CHECKED THAT THE
		// INNER MEMORY CACHE SHOULD BE DISPOSED, WHICH IN TURN MEANS THAT
		// WE CREATED THE INSTANCE, WHICH IN TURN MEANS THAT NOBODY ELSE CAN
		// BE USING IT.
		// 
		// ALL OF THIS MEANS THAT WE ARE SURE THAT BY CALLING .Clear() WE ARE
		// NOT CLEARING SOMEBODY ELSE'S DATA.

		return true;
	}

	internal bool TryExecuteRawClear(string operationId)
	{
		if (CanExecuteRawClear() == false)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): it was not possible to execute a raw clear", _options.CacheName, _options.InstanceId, operationId);

			return false;
		}

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): executing a raw clear", _options.CacheName, _options.InstanceId, operationId);

		return _mca.TryClear();
	}

	// SERIALIZATION

	/// <inheritdoc/>
	public IFusionCache SetupSerializer(IFusionCacheSerializer serializer)
	{
		CheckDisposed();

		if (serializer is null)
			throw new ArgumentNullException(nameof(serializer));

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup serializer (SERIALIZER={SerializerType})", CacheName, InstanceId, serializer.GetType().FullName);

		_serializer = serializer;

		return this;
	}

	// DISTRIBUTED CACHE

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache)
	{
		CheckDisposed();

		if (distributedCache is null)
			throw new ArgumentNullException(nameof(distributedCache));

		if (_serializer is null)
			throw new InvalidOperationException("The serializer must be set before setting up the distributed cache");

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup distributed cache (CACHE={DistributedCacheType})", CacheName, InstanceId, distributedCache.GetType().FullName);

		_dca = new DistributedCacheAccessor(distributedCache, _serializer, _options, _logger, _events.Distributed);

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
		CheckDisposed();

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
	public IDistributedCache? DistributedCache
	{
		get { return _dca?.DistributedCache; }
	}

	// BACKPLANE

	/// <inheritdoc/>
	public IFusionCache SetupBackplane(IFusionCacheBackplane backplane)
	{
		CheckDisposed();

		if (backplane is null)
			throw new ArgumentNullException(nameof(backplane));

		if (_bpa is not null)
		{
			RemoveBackplane();
		}

		var shouldSubscribe = false;
		lock (_backplaneLock)
		{
			shouldSubscribe = true;
			_bpa = new BackplaneAccessor(this, backplane, _options, _logger);
		}

		if (shouldSubscribe)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: setup backplane (BACKPLANE={BackplaneType})", CacheName, InstanceId, backplane.GetType().FullName);

			if (_options.WaitForInitialBackplaneSubscribe)
			{
				_bpa.Subscribe();
			}
			else
			{
				_ = Task.Run(async () =>
				{
					await _bpa.SubscribeAsync().ConfigureAwait(false);
				});
			}
		}

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveBackplane()
	{
		CheckDisposed();

		if (_bpa is not null)
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
		}

		return this;
	}

	/// <inheritdoc/>
	public bool HasBackplane
	{
		get { return _bpa is not null; }
	}

	/// <inheritdoc/>
	public IFusionCacheBackplane? Backplane
	{
		get { return _bpa?.Backplane; }
	}

	// DISTRIBUTED LOCKER

	/// <inheritdoc/>
	public IFusionCache SetupDistributedLocker(IFusionCacheDistributedLocker distributedLocker)
	{
		CheckDisposed();

		if (distributedLocker is null)
			throw new ArgumentNullException(nameof(distributedLocker));

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: [DL] setup distributed locker (LOCKER={DistributedLockerType})", CacheName, InstanceId, distributedLocker.GetType().FullName);

		_distributedLocker = distributedLocker;

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveDistributedLocker()
	{
		CheckDisposed();

		if (_distributedLocker is not null)
		{
			_distributedLocker = null;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: [DL] distributed locker removed", CacheName, InstanceId);
		}

		return this;
	}

	/// <inheritdoc/>
	public bool HasDistributedLocker
	{
		get { return _distributedLocker is not null; }
	}

	// EVENTS

	/// <inheritdoc/>
	public FusionCacheEventsHub Events { get { return _events; } }

	// PLUGINS

	/// <inheritdoc/>
	public void AddPlugin(IFusionCachePlugin plugin)
	{
		CheckDisposed();

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
		CheckDisposed();

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

	// DISTRIBUTED OPERATIONS

	internal bool RequiresDistributedOperations(FusionCacheEntryOptions options)
	{
		if (HasDistributedCache && options.SkipDistributedCacheRead == false && options.SkipDistributedCacheWrite == false)
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

	// ADAPTIVE CACHING

	private static void UpdateAdaptiveOptions<TValue>(FusionCacheFactoryExecutionContext<TValue> ctx, ref FusionCacheEntryOptions options)
	{
		// UPDATE ADAPTIVE OPTIONS
		var maybeNewOptions = ctx.GetOptions();

		if (ReferenceEquals(options, maybeNewOptions))
			return;

		options = maybeNewOptions;
	}

	// INTERNAL UPDATES

	internal TValue GetValueFromMemoryEntry<TValue>(string operationId, string key, IFusionCacheMemoryEntry entry, FusionCacheEntryOptions? options)
	{
		options ??= _defaultEntryOptions;

		if (options.EnableAutoClone == false)
			return entry.GetValue<TValue>();

		if (_serializer is null)
			throw new InvalidOperationException($"A serializer is needed when using {nameof(FusionCacheEntryOptions.EnableAutoClone)}.");

		if (entry.Value is null)
			return entry.GetValue<TValue>();

		if (_options.SkipAutoCloneForImmutableObjects && ImmutableTypeCache<TValue>.IsImmutable)
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

	internal byte[]? GetSerializedValueFromValue<TValue>(string operationId, string key, TValue value, FusionCacheEntryOptions? options)
	{
		options ??= _defaultEntryOptions;

		if (options.EnableAutoClone == false)
			return null;

		if (_serializer is null)
			throw new InvalidOperationException($"A serializer is needed when using {nameof(FusionCacheEntryOptions.EnableAutoClone)}.");

		if (value is null)
			return null;

		if (_options.SkipAutoCloneForImmutableObjects && ImmutableTypeCache<TValue>.IsImmutable)
			return null;

		try
		{
			return _serializer.Serialize(value);
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
	}

	// BEST PRACTICES
	private void CheckBestPractices()
	{
		// CHECK:
		// - IS NOT DEFAULT CACHE
		// - NO CACHE KEY PREFIX
		// - AND (
		//   MEMORY CACHE NOT OWNED
		//   OR HAS L2
		// )
		if (
			CacheName != FusionCacheOptions.DefaultCacheName
			&& string.IsNullOrWhiteSpace(_options.CacheKeyPrefix)
			&& (
				_mca.IsOwned == false
				|| HasDistributedCache
			)
		)
		{
			if (_logger?.IsEnabled(_options.MissingCacheKeyPrefixWarningLogLevel) ?? false)
				_logger.Log(_options.MissingCacheKeyPrefixWarningLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}]: a named cache is being used, and no CacheKeyPrefix has been specified. It's usually better to specify a prefix to automatically avoid cache key collisions. If collisions are already avoided when manually creating the cache keys, you can change the MissingCacheKeyPrefixWarningLogLevel option.", CacheName, InstanceId);
		}

		// CHECK:
		// - HAS BACKPLANE
		// - AND NO L2
		// - AND DefaultEntryOptions.SkipBackplaneNotifications IS FALSE
		if (
			HasBackplane
			&& HasDistributedCache == false
			&& DefaultEntryOptions.SkipBackplaneNotifications == false
		)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}]: it has been detected a situation where there *IS* a backplane (BACKPLANE={BackplaneType}), there is *NOT* a distributed cache and the DefaultEntryOptions.SkipBackplaneNotifications option is set to false. This will probably cause problems, since a notification will be sent automatically at every change in the cache (Set, Remove, Expire and also GetOrSet when the factory is called) but there is not a distributed cache that different nodes can use, basically resulting in a situation where the cache will keep invalidating itself at every change. It is suggested to either (1) add a distributed cache or (2) change the DefaultEntryOptions.SkipBackplaneNotifications to true.", CacheName, InstanceId, Backplane!.GetType().FullName);
		}

		// CHECK:
		// - HAS L2
		// - AND NO BACKPLANE
		// - AND (
		//   NO DefaultEntryOptions.MemoryCacheDuration
		//   OR NO TagsDefaultEntryOptions.MemoryCacheDuration
		// )
		if (
			HasDistributedCache
			&& HasBackplane == false
			&& (
				_defaultEntryOptions.MemoryCacheDuration is null
				|| _tagsDefaultEntryOptions.MemoryCacheDuration is null
			)
		)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName} I={CacheInstanceId}]: you are using an L2 (distributed cache) without a backplane, which will potentially leave other nodes' L1s (memory caches) out-of-sync after an update (see: cache coherence). To solve this, you can use a backplane. If that is not possible, you can mitigate the situation by setting both DefaultEntryOptions.MemoryCacheDuration and TagsDefaultEntryOptions.MemoryCacheDuration to a low value: this will refresh data in the L1 from the L2 more frequently, reducing the incoherence window.", CacheName, InstanceId);
		}
	}

	// IDISPOSABLE

	private bool _disposedValue = false;

	/// <summary>
	/// Release all resources managed by FusionCache.
	/// </summary>
	public void Dispose()
	{
		if (_disposedValue)
		{
			return;
		}

		RemoveAllPlugins();
		RemoveBackplane();
		RemoveDistributedCache();
		RemoveDistributedLocker();

		_autoRecovery?.Dispose();
		_autoRecovery = null;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
		_memoryLocker.Dispose();
		_memoryLocker = null;

		_mca.Dispose();
		_mca = null;

		_events = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

		_disposedValue = true;
	}
}
