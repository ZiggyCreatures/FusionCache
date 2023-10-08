using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;
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
	private readonly ILogger? _logger;
	private IFusionCacheReactor _reactor;
	private MemoryCacheAccessor _mca;
	private DistributedCacheAccessor? _dca;
	private BackplaneAccessor? _bpa;
	private readonly object _backplaneLock = new object();
	private FusionCacheEventsHub _events;
	private readonly List<IFusionCachePlugin> _plugins;

	// AUTO-RECOVERY
	private readonly ConcurrentDictionary<string, AutoRecoveryItem> _autoRecoveryQueue = new ConcurrentDictionary<string, AutoRecoveryItem>();
	private readonly SemaphoreSlim _autoRecoveryProcessingLock = new SemaphoreSlim(1, 1);
	private FusionCacheEntryOptions? _autoRecoveryRemoveDistributedCacheEntryOptions;
	private readonly FusionCacheEntryOptions _autoRecoverySentinelEntryOptions;
	private readonly int _autoRecoveryMaxItems;
	private readonly int _autoRecoveryMaxRetryCount;
	private readonly TimeSpan _autoRecoveryDelay;
	private static readonly TimeSpan _autoRecoveryMinDelay = TimeSpan.FromMilliseconds(10);
	private CancellationTokenSource? _autoRecoveryCts;
	private long _autoRecoveryBarrierTicks = 0;
	private readonly string _autoRecoverySentinelCacheKey = Guid.NewGuid().ToString("N");

	/// <summary>
	/// Creates a new <see cref="FusionCache"/> instance.
	/// </summary>
	/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
	/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use. If null, one will be automatically created and managed.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	/// <param name="reactor">The <see cref="IFusionCacheReactor"/> instance to use (advanced). If null, a standard one will be automatically created and managed.</param>
	public FusionCache(IOptions<FusionCacheOptions> optionsAccessor, IMemoryCache? memoryCache = null, ILogger<FusionCache>? logger = null, IFusionCacheReactor? reactor = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// GLOBALLY UNIQUE INSTANCE ID
		if (string.IsNullOrWhiteSpace(_options.InstanceId) == false)
		{
			InstanceId = _options.InstanceId!;
		}
		else
		{
			InstanceId = Guid.NewGuid().ToString("N");
		}

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

		// REACTOR
		_reactor = reactor ?? new FusionCacheReactorStandard();

		// EVENTS
		_events = new FusionCacheEventsHub(this, _options, _logger);

		// PLUGINS
		_plugins = new List<IFusionCachePlugin>();

		// MEMORY CACHE
		_mca = new MemoryCacheAccessor(memoryCache, _options, _logger, _events.Memory);

		// DISTRIBUTED CACHE
		_dca = null;

		// BACKPLANE
		_bpa = null;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: instance created", _options.CacheName, InstanceId);

		// AUTO-RECOVERY
		_autoRecoveryDelay = _options.AutoRecoveryDelay;
		// NOTE: THIS IS PRAGMATIC, SO TO AVOID CHECKING AN int? EVERY TIME, AND int.MaxValue IS HIGH ENOUGH THAT IT WON'T MATTER
		_autoRecoveryMaxItems = _options.AutoRecoveryMaxItems ?? int.MaxValue;
		_autoRecoveryMaxRetryCount = _options.AutoRecoveryMaxRetryCount ?? int.MaxValue;

		_autoRecoverySentinelEntryOptions = new FusionCacheEntryOptions
		{
			// MEMORY CACHE
			SkipMemoryCache = true,
			// DISTRIBUTED CACHE
			SkipDistributedCache = false,
			DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
			DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
			AllowBackgroundDistributedCacheOperations = false,
			ReThrowDistributedCacheExceptions = true,
			SkipDistributedCacheReadWhenStale = false,
			// BACKPLANE
			SkipBackplaneNotifications = false,
			AllowBackgroundBackplaneOperations = false,
			ReThrowBackplaneExceptions = true
		};

		// AUTO-RECOVERY
		if (_options.EnableAutoRecovery)
		{
			if (_autoRecoveryDelay <= TimeSpan.Zero)
			{
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery is enabled but cannot be started because the AutoRecoveryDelay has been set to zero", CacheName, InstanceId, FusionCacheInternalUtils.MaybeGenerateOperationId(_logger));
			}
			else
			{
				_autoRecoveryCts = new CancellationTokenSource();
				_ = BackgroundAutoRecoveryAsync();
			}
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
		get { return _options.DefaultEntryOptions; }
	}

	/// <inheritdoc/>
	public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
	{
		var res = _options.DefaultEntryOptions.Duplicate(duration);
		setupAction?.Invoke(res);
		return res;
	}

	private void ValidateCacheKey(string key)
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

	private bool TryPickFailSafeFallbackValue<TValue>(string operationId, string key, FusionCacheDistributedEntry<TValue>? distributedEntry, FusionCacheMemoryEntry? memoryEntry, MaybeValue<TValue?> failSafeDefaultValue, FusionCacheEntryOptions options, out MaybeValue<TValue?> value, out long? timestamp, out bool failSafeActivated)
	{
		// FAIL-SAFE NOT ENABLED
		if (options.IsFailSafeEnabled == false)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE not enabled", CacheName, operationId, key);

			value = default;
			timestamp = default;
			failSafeActivated = false;
			return false;
		}

		// FAIL-SAFE ENABLED
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): trying to activate FAIL-SAFE", CacheName, operationId, key);

		// TRY TO PICK A FALLBACK ENTRY
		IFusionCacheEntry? entry;
		//if (distributedEntry is not null && (memoryEntry is null || distributedEntry.Timestamp > memoryEntry.Timestamp))
		if (distributedEntry is not null)
			entry = distributedEntry;
		else
			entry = memoryEntry;

		if (entry is not null)
		{
			// ACTIVATE FAIL-SAFE
			value = entry.GetValue<TValue>();
			timestamp = entry.Timestamp;
			failSafeActivated = true;

			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from " + (entry is FusionCacheMemoryEntry ? "memory" : "distributed") + ")", CacheName, operationId, key);

			// EVENT
			_events.OnFailSafeActivate(operationId, key);

			return true;
		}

		// TRY WITH THE FAIL-SAFE DEFAULT VALUE
		if (failSafeDefaultValue.HasValue)
		{
			// ACTIVATE FAIL-SAFE
			value = failSafeDefaultValue.Value;
			timestamp = null;
			failSafeActivated = true;

			if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
				_logger.Log(_options.FailSafeActivationLogLevel, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from fail-safe default value)", CacheName, operationId, key);

			// EVENT
			_events.OnFailSafeActivate(operationId, key);

			return true;
		}

		// UNABLE TO ACTIVATE FAIL-SAFE
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): unable to activate FAIL-SAFE (no entries in memory or distributed)", CacheName, operationId, key);

		value = default;
		timestamp = default;
		failSafeActivated = false;
		return false;
	}

	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MaybeBackgroundCompleteTimedOutFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue?>? factoryTask, FusionCacheEntryOptions options, CancellationToken token)
	{
		if (factoryTask is null || options.AllowTimedOutFactoryBackgroundCompletion == false)
			return;

		CompleteBackgroundFactory<TValue>(operationId, key, ctx, factoryTask, options, null, token);
	}

	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CompleteBackgroundFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext<TValue> ctx, Task<TValue?> factoryTask, FusionCacheEntryOptions options, object? lockObj, CancellationToken token)
	{
		if (factoryTask.IsFaulted)
		{
			try
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, factoryTask.Exception.GetSingleInnerExceptionOrSelf(), "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): a background factory thrown an exception", CacheName, operationId, key);

				// EVENT
				_events.OnBackgroundFactoryError(operationId, key);
			}
			finally
			{
				ReleaseLock(operationId, key, lockObj);
			}

			return;
		}

		// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): trying to complete a background factory", CacheName, operationId, key);

		_ = factoryTask.ContinueWith(async antecedent =>
		{
			try
			{
				if (antecedent.Status == TaskStatus.Faulted)
				{
					if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
						_logger.Log(_options.FactoryErrorsLogLevel, antecedent.Exception.GetSingleInnerExceptionOrSelf(), "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): a background factory thrown an exception", CacheName, operationId, key);

					// EVENT
					_events.OnBackgroundFactoryError(operationId, key);
				}
				else if (antecedent.Status == TaskStatus.RanToCompletion)
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): a background factory successfully completed, keeping the result", CacheName, operationId, key);

					// UPDATE ADAPTIVE OPTIONS
					var maybeNewOptions = ctx.GetOptions();
					if (maybeNewOptions is not null && options != maybeNewOptions)
						options = maybeNewOptions;

					// ADAPTIVE CACHING UPDATE
					var lateEntry = FusionCacheMemoryEntry.CreateFromOptions(antecedent.Result, options, false, ctx.LastModified, ctx.ETag, null, typeof(TValue));

					var mca = GetCurrentMemoryAccessor(options);
					if (mca is not null)
					{
						mca.SetEntry<TValue>(operationId, key, lateEntry, options);
					}

					await DistributedSetEntryAsync<TValue>(operationId, key, lateEntry, options, token).ConfigureAwait(false);

					// EVENT
					_events.OnBackgroundFactorySuccess(operationId, key);
					_events.OnSet(operationId, key);
				}
			}
			finally
			{
				ReleaseLock(operationId, key, lockObj);
			}
		});
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReleaseLock(string operationId, string key, object? lockObj)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): releasing LOCK", CacheName, operationId, key);

		try
		{
			_reactor.ReleaseLock(CacheName, key, operationId, lockObj, _logger);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): LOCK released", CacheName, operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, exc, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): releasing the LOCK has thrown an exception", CacheName, operationId, key);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessFactoryError(string operationId, string key, Exception exc)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.FactorySyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.FactorySyntheticTimeoutsLogLevel, exc, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while calling the factory", CacheName, operationId, key);

			// EVENT
			_events.OnFactorySyntheticTimeout(operationId, key);

			return;
		}

		if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
			_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling the factory", CacheName, operationId, key);

		// EVENT
		_events.OnFactoryError(operationId, key);
	}

	internal bool MaybeExpireMemoryEntryInternal(string operationId, string key, bool allowFailSafe, long? timestampThreshold)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling MaybeExpireMemoryEntryInternal (allowFailSafe={AllowFailSafe}, timestampThreshold={TimestampThreshold})", CacheName, operationId, key, allowFailSafe, timestampThreshold);

		if (_mca is null)
			return false;

		return _mca.ExpireEntry(operationId, key, allowFailSafe, timestampThreshold);
	}

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer)
	{
		if (distributedCache is null)
			throw new ArgumentNullException(nameof(distributedCache));

		if (serializer is null)
			throw new ArgumentNullException(nameof(serializer));

		_dca = new DistributedCacheAccessor(distributedCache, serializer, _options, _logger, _events.Distributed);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}]: setup distributed cache (CACHE={DistributedCacheType} SERIALIZER={SerializerType})", CacheName, distributedCache.GetType().FullName, serializer.GetType().FullName);

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveDistributedCache()
	{
		_dca = null;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}]: distributed cache removed", CacheName);

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
			_bpa = new BackplaneAccessor(this, backplane, _options, _logger, _events.Backplane);
			_bpa.Subscribe();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}]: setup backplane (BACKPLANE={BackplaneType})", CacheName, backplane.GetType().FullName);
		}

		// CHECK: WARN THE USER IN CASE OF
		// - HAS A MEMORY CACHE (ALWAYS)
		// - HAS A BACKPLANE
		// - DOES *NOT* HAVE A DISTRIBUTED CACHE
		// - THE OPTION DefaultEntryOptions.SkipBackplaneNotifications IS FALSE
		if (HasBackplane && HasDistributedCache == false && DefaultEntryOptions.SkipBackplaneNotifications == false)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION [N={CacheName}]: it has been detected a situation where there *IS* a backplane, there is *NOT* a distributed cache and the DefaultEntryOptions.SkipBackplaneNotifications option is set to false. This will probably cause problems, since a notification will be sent automatically at every change in the cache but there is not a shared state (a distributed cache) that different nodes can use, basically resulting in a situation where the cache will keep invalidating itself at every change. It is suggested to either (1) add a distributed cache or (2) change the DefaultEntryOptions.SkipBackplaneNotifications to true.", CacheName, backplane.GetType().FullName);
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
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}]: backplane removed", CacheName);
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
		lock (_plugins)
		{
			if (_plugins.Contains(plugin))
			{
				if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
					_logger?.Log(_options.PluginsErrorsLogLevel, "FUSION [N={CacheName}]: the same plugin instance already exists (TYPE={PluginType})", CacheName, plugin.GetType().FullName);

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
			lock (_plugins)
			{
				_plugins.Remove(plugin);
			}

			if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
				_logger.Log(_options.PluginsErrorsLogLevel, exc, "FUSION [N={CacheName}]: an error occurred while starting a plugin (TYPE={PluginType})", CacheName, plugin.GetType().FullName);

			throw new InvalidOperationException($"FUSION [N={CacheName}]: an error occurred while starting a plugin (TYPE={plugin.GetType().FullName})", exc);
		}

		if (_logger?.IsEnabled(_options.PluginsInfoLogLevel) ?? false)
			_logger?.Log(_options.PluginsInfoLogLevel, "FUSION [N={CacheName}]: a plugin has been added and started (TYPE={PluginType})", CacheName, plugin.GetType().FullName);
	}

	/// <inheritdoc/>
	public bool RemovePlugin(IFusionCachePlugin plugin)
	{
		if (plugin is null)
			throw new ArgumentNullException(nameof(plugin));

		lock (_plugins)
		{
			if (_plugins.Contains(plugin) == false)
			{
				if (_logger?.IsEnabled(_options.PluginsErrorsLogLevel) ?? false)
					_logger?.Log(_options.PluginsErrorsLogLevel, "FUSION [N={CacheName}]: the plugin cannot be removed because is not part of this FusionCache instance (TYPE={PluginType})", CacheName, plugin.GetType().FullName);

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
					_logger.Log(_options.PluginsErrorsLogLevel, exc, "FUSION [N={CacheName}]: an error occurred while stopping a plugin (TYPE={PluginType})", CacheName, plugin.GetType().FullName);

				throw new InvalidOperationException($"FUSION [N={CacheName}]: an error occurred while stopping a plugin (TYPE={plugin.GetType().FullName})", exc);
			}
			finally
			{
				// REMOVE THE PLUGIN
				_plugins.Remove(plugin);
			}
		}

		if (_logger?.IsEnabled(_options.PluginsInfoLogLevel) ?? false)
			_logger?.Log(_options.PluginsInfoLogLevel, "FUSION [N={CacheName}]: a plugin has been stopped and removed (TYPE={PluginType})", CacheName, plugin.GetType().FullName);

		return true;
	}

	private void RemoveAllPlugins()
	{
		foreach (var plugin in _plugins.ToArray())
		{
			RemovePlugin(plugin);
		}
	}

	// IDISPOSABLE
	private bool disposedValue = false;
	/// <summary>
	/// Release all resources managed by FusionCache.
	/// </summary>
	/// <param name="disposing">Indicates if the disposing is happening.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
				_autoRecoveryQueue.Clear();
				_autoRecoveryCts?.Cancel();
				_autoRecoveryCts = null;

				RemoveAllPlugins();
				RemoveBackplane();
				RemoveDistributedCache();

				_reactor.Dispose();
				_reactor = null;

				_mca.Dispose();
				_mca = null;

				_events = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
			}
			disposedValue = true;
		}
	}

	/// <summary>
	/// Release all resources managed by FusionCache.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
	}

	private bool RequiresDistributedOperations(FusionCacheEntryOptions options)
	{
		if (HasDistributedCache && options.SkipDistributedCache == false)
			return true;

		if (HasBackplane && options.SkipBackplaneNotifications == false)
			return true;

		return false;
	}

	private bool MustAwaitDistributedOperations(FusionCacheEntryOptions options)
	{
		if (HasDistributedCache && options.AllowBackgroundDistributedCacheOperations == false)
			return true;

		if (HasDistributedCache == false && HasBackplane && options.AllowBackgroundBackplaneOperations == false)
			return true;

		return false;
	}

	private static readonly MethodInfo __methodInfoTryUpdateMemoryEntryFromDistributedEntryAsyncOpenGeneric = typeof(FusionCache).GetMethod(nameof(TryUpdateMemoryEntryFromDistributedEntryAsync), BindingFlags.NonPublic | BindingFlags.Instance);

	internal async ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryUntypedAsync(string operationId, string cacheKey, FusionCacheMemoryEntry memoryEntry)
	{
		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): Trying to update memory entry from distributed entry", CacheName, InstanceId, operationId, cacheKey);

		try
		{
			if (HasDistributedCache == false)
				return (false, false, false);

			var dca = GetCurrentDistributedAccessor(null);

			if (dca is null)
				return (false, false, false);

			if (dca.IsCurrentlyUsable(operationId, cacheKey) == false)
				return (true, false, false);

			var methodInfo = __methodInfoTryUpdateMemoryEntryFromDistributedEntryAsyncOpenGeneric.MakeGenericMethod(memoryEntry.ValueType);
			// SIGNATURE PARAMS: string operationId, string cacheKey, DistributedCacheAccessor dca, FusionCacheMemoryEntry memoryEntry
			return await ((ValueTask<(bool error, bool isSame, bool hasUpdated)>)methodInfo.Invoke(this, new object[] { operationId, cacheKey, dca, memoryEntry })).ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): an error occurred while calling TryUpdateMemoryEntryFromDistributedEntryUntypedAsync() to try to update a memory entry from a distributed entry without knowing the TValue type", _options.CacheName, operationId, cacheKey);

			return (true, false, false);
		}
	}

	private async ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryAsync<TValue>(string operationId, string cacheKey, DistributedCacheAccessor dca, FusionCacheMemoryEntry memoryEntry)
	{
		var options = new FusionCacheEntryOptions()
		{
			DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan,
			DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan,
			AllowBackgroundDistributedCacheOperations = false,
			ReThrowDistributedCacheExceptions = true,
			ReThrowSerializationExceptions = true,
		};

		try
		{
			(var distributedEntry, var isValid) = await dca.TryGetEntryAsync<TValue>(operationId, cacheKey, options, false, Timeout.InfiniteTimeSpan, default);

			if (distributedEntry is null || isValid == false)
			{
				//_cache.MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, null);
				//return;

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): distributed entry not found or stale, do not update memory entry", CacheName, InstanceId, operationId, cacheKey);

				return (false, false, false);
			}

			if (/*distributedEntry.Timestamp is not null &&*/ distributedEntry.Timestamp == memoryEntry.Timestamp)
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry same as distributed entry, do not update memory entry", CacheName, InstanceId, operationId, cacheKey);

				return (false, true, false);
			}

			if (distributedEntry.Timestamp < memoryEntry.Timestamp)
			{
				//return;
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): memory entry more fresh than distributed entry, do not update memory entry", CacheName, InstanceId, operationId, cacheKey);

				return (false, false, false);
			}

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): updating memory entry from distributed entry", CacheName, InstanceId, operationId, cacheKey);

			memoryEntry.UpdateFromDistributedEntry<TValue>(distributedEntry);

			_events.Memory.OnSet(operationId, cacheKey);

			return (false, false, true);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while trying to update a memory entry from a distributed entry", CacheName, InstanceId, operationId, cacheKey);

			//MaybeExpireMemoryEntryInternal(operationId, cacheKey, true, null);

			return (true, false, false);
		}
	}




	// AUTO-RECOVERY

	internal bool TryAddAutoRecoveryItem(string? operationId, string? cacheKey, FusionCacheAction action, long timestamp, FusionCacheEntryOptions options, BackplaneMessage? message)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (RequiresDistributedOperations(options) == false)
			return false;

		if (cacheKey is null)
			return false;

		if (action == FusionCacheAction.Unknown)
			return false;

		options = options.Duplicate();

		// DISTRIBUTED CACHE
		if (options.SkipDistributedCache == false)
		{
			options.AllowBackgroundDistributedCacheOperations = false;
			options.DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan;
			options.DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan;
			options.ReThrowDistributedCacheExceptions = true;
			options.ReThrowSerializationExceptions = true;
			options.SkipDistributedCacheReadWhenStale = false;
		}

		// BACKPLANE
		if (options.SkipBackplaneNotifications == false)
		{
			options.AllowBackgroundBackplaneOperations = false;
			options.ReThrowBackplaneExceptions = true;
		}

		var duration = (options.SkipDistributedCache || HasDistributedCache == false) ? options.Duration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration);
		var expirationTicks = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(duration, options, false).Ticks;

		// TODO: MAYBE USE THE ITEM'S Timestamp HERE

		if (_autoRecoveryQueue.Count >= _autoRecoveryMaxItems && _autoRecoveryQueue.ContainsKey(cacheKey) == false)
		{
			// IF:
			// - A LIMIT HAS BEEN SET
			// - THE LIMIT HAS BEEN REACHED OR SURPASSED
			// - THE ITEM TO BE ADDED IS NOT ALREADY THERE (OTHERWISE IT WILL BE AN OVERWRITE AND SIZE WILL NOT GROW)
			// THEN:
			// - FIND THE ITEM THAT WILL EXPIRE SOONER AND REMOVE IT
			// - OR, IF NEW ITEM WILL EXPIRE SOONER, DO NOT ADD IT
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the auto-recovery queue has reached the max size of {MaxSize}", CacheName, InstanceId, operationId, cacheKey, _autoRecoveryMaxItems);

			try
			{
				var earliestToExpire = _autoRecoveryQueue.Values.ToArray().Where(x => x.ExpirationTicks is not null).OrderBy(x => x.ExpirationTicks).FirstOrDefault();
				if (earliestToExpire is not null /*&& earliestToExpire.Message is not null*/)
				{
					if (earliestToExpire.ExpirationTicks < expirationTicks)
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an item with cache key {CacheKeyToRemove} has been removed from the auto-recovery queue to make space for the new one", CacheName, InstanceId, operationId, cacheKey, earliestToExpire.CacheKey);

						// REMOVE THE QUEUED ITEM
						TryRemoveAutoRecoveryItem(operationId, earliestToExpire);
					}
					else
					{
						if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
							_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): the item has not been added to the auto-recovery queue because it would have expired earlier than the earliest item already present in the queue (with cache key {CacheKeyEarliest})", CacheName, InstanceId, operationId, cacheKey, earliestToExpire.CacheKey);

						// IGNORE THE NEW ITEM
						return false;
					}
				}
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while deciding which item in the auto-recovery queue to remove to make space for a new one", CacheName, InstanceId, operationId, cacheKey);
			}
		}

		_autoRecoveryQueue[cacheKey] = new AutoRecoveryItem(cacheKey, action, timestamp, options, expirationTicks, _autoRecoveryMaxRetryCount, message);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): added (or overwrote) an item to the auto-recovery queue", CacheName, InstanceId, operationId, cacheKey);

		return true;
	}

	internal bool TryRemoveAutoRecoveryItemByCacheKey(string? operationId, string cacheKey)
	{
		if (cacheKey is null)
			return false;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the auto-recovery queue", CacheName, InstanceId, operationId, cacheKey);

		return true;
	}

	internal bool TryRemoveAutoRecoveryItem(string? operationId, AutoRecoveryItem item)
	{
		if (item is null)
			return false;

		if (item.CacheKey is null)
			return false;

		if (_autoRecoveryQueue.TryGetValue(item.CacheKey, out var pendingLocal) == false)
			return false;

		// NOTE: HERE WE SHOULD USE THE NEW OVERLOAD TryRemove(KeyValuePair<TKey,TValue>) BUT THAT IS NOT AVAILABLE UNTIL .NET 5
		// SO WE DO THE NEXT BEST THING WE CAN: TRY TO GET THE VALUE AND, IF IT IS THE SAME AS THE ONE WE HAVE, THEN REMOVE IT
		// OTHERWISE SKIP THE REMOVAL
		//
		// SEE: https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.tryremove?view=net-7.0#system-collections-concurrent-concurrentdictionary-2-tryremove(system-collections-generic-keyvaluepair((-0-1)))

		if (object.ReferenceEquals(item, pendingLocal) == false)
			return false;

		if (_autoRecoveryQueue.TryRemove(item.CacheKey, out _) == false)
			return false;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): removed an item from the auto-recovery queue", CacheName, InstanceId, operationId, item.CacheKey);

		return true;
	}

	internal bool TryCleanUpAutoRecoveryQueue(string operationId, IList<AutoRecoveryItem> items)
	{
		if (items.Count == 0)
			return false;

		var atLeastOneRemoved = false;

		// NOTE: WE USE THE REVERSE ITERATION TRICK TO AVOID PROBLEMS WITH REMOVING ITEMS WHILE ITERATING
		for (int i = items.Count - 1; i >= 0; i--)
		{
			var item = items[i];
			// IF THE ITEM IS SINCE EXPIRED -> REMOVE IT FROM THE QUEUE *AND* FROM THE LIST
			if (item.IsExpired())
			{
				TryRemoveAutoRecoveryItem(operationId, item);
				items.RemoveAt(i);
				atLeastOneRemoved = true;

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): auto-cleanup of auto-recovery item", CacheName, InstanceId, operationId, item.CacheKey);
			}
		}

		return atLeastOneRemoved;
	}

	internal bool CheckIncomingMessageForAutoRecoveryConflicts(string operationId, BackplaneMessage message)
	{
		if (message.CacheKey is null)
		{
			return true;
		}

		if (_autoRecoveryQueue.TryGetValue(message.CacheKey, out var pendingLocal) == false)
		{
			// NO PENDING LOCAL MESSAGE WITH THE SAME KEY
			return true;
		}

		// TODO: MAYBE USE THE ITEM'S Timestamp PROP DIRECTLY, IF WE'VE ADDED IT
		if (pendingLocal.Timestamp <= message.Timestamp)
		{
			// PENDING LOCAL MESSAGE IS -OLDER- THAN THE INCOMING ONE -> REMOVE THE LOCAL ONE
			TryRemoveAutoRecoveryItem(operationId, pendingLocal);
			return true;
		}

		// PENDING LOCAL MESSAGE IS -NEWER- THAN THE INCOMING ONE -> DO NOT PROCESS THE INCOMING ONE
		return false;
	}

	internal bool TryUpdateAutoRecoveryBarrier(string operationId)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (_autoRecoveryQueue.Count == 0)
			return false;

		var newBarrier = DateTimeOffset.UtcNow.Ticks + _autoRecoveryDelay.Ticks;
		var oldBarrier = Interlocked.Exchange(ref _autoRecoveryBarrierTicks, newBarrier);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery barrier set from {OldAutoRecoveryBarrier} to {NewAutoRecoveryBarrier}", CacheName, InstanceId, operationId, oldBarrier, newBarrier);

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger.Log(LogLevel.Information, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting at least {AutoRecoveryDelay} to start auto-recovery to let the other nodes reconnect, to better handle backpressure", CacheName, InstanceId, operationId, _autoRecoveryDelay);

		return true;
	}

	internal async ValueTask<bool> TryProcessAutoRecoveryQueueAsync(string operationId, CancellationToken token)
	{
		if (_options.EnableAutoRecovery == false)
			return false;

		if (_autoRecoveryQueue.Count == 0)
			return false;

		// ACQUIRE THE LOCK
		if (_autoRecoveryProcessingLock.Wait(0) == false)
		{
			// IF THE LOCK HAS NOT BEEN ACQUIRED IMMEDIATELY -> PROCESSING IS ALREADY ONGOING, SO WE JUST RETURN
			return false;
		}

		// SNAPSHOT THE ITEMS TO PROCESS
		var itemsToProcess = _autoRecoveryQueue.Values.ToList();

		// INITIAL CLEANUP
		_ = TryCleanUpAutoRecoveryQueue(operationId, itemsToProcess);

		// IF NO REMAINING ITEMS -> JUST RELEASE THE LOCK AND RETURN
		if (itemsToProcess.Count == 0)
		{
			_autoRecoveryProcessingLock.Release();
			return false;
		}

		var processedCount = 0;
		var hasStopped = false;
		AutoRecoveryItem? lastProcessedItem = null;

		try
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): starting auto-recovery of {Count} pending items", CacheName, InstanceId, operationId, itemsToProcess.Count);

			//// TODO: MAYBE SENTINEL NOT NEEDED HERE (THE BACKPLANE MAY BE NOT NEEDED...)

			//// PUBLISH (SENTINEL)
			//var sentinelSuccess = await _bpa.PublishSentinelAsync(operationId, _autoRecoverySentinelCacheKey, _autoRecoverySentinelEntryOptions, token).ConfigureAwait(false);
			//if (sentinelSuccess == false)
			//{
			//	hasStopped = true;
			//	return false;
			//}

			// AUTO-RECOVERY SPECIFIC ENTRY OPTIONS
			if (_autoRecoveryRemoveDistributedCacheEntryOptions is null)
			{
				//_autoRecoveryRemoveDistributedCacheEntryOptions = _cache.DefaultEntryOptions.Duplicate();
				_autoRecoveryRemoveDistributedCacheEntryOptions = new FusionCacheEntryOptions();

				// MEMORY CACHE
				_autoRecoveryRemoveDistributedCacheEntryOptions.SkipMemoryCache = true;
				// DISTRIBUTED CACHE
				_autoRecoveryRemoveDistributedCacheEntryOptions.SkipDistributedCache = false;

				_autoRecoveryRemoveDistributedCacheEntryOptions.DistributedCacheSoftTimeout = Timeout.InfiniteTimeSpan;
				_autoRecoveryRemoveDistributedCacheEntryOptions.DistributedCacheHardTimeout = Timeout.InfiniteTimeSpan;

				_autoRecoveryRemoveDistributedCacheEntryOptions.AllowBackgroundDistributedCacheOperations = false;
				_autoRecoveryRemoveDistributedCacheEntryOptions.ReThrowDistributedCacheExceptions = true;
				_autoRecoveryRemoveDistributedCacheEntryOptions.SkipDistributedCacheReadWhenStale = false;
				// BACKPLANE
				_autoRecoveryRemoveDistributedCacheEntryOptions.SkipBackplaneNotifications = true;
				_autoRecoveryRemoveDistributedCacheEntryOptions.AllowBackgroundBackplaneOperations = false;
				_autoRecoveryRemoveDistributedCacheEntryOptions.ReThrowBackplaneExceptions = true;
			}

			foreach (var item in itemsToProcess)
			{
				processedCount++;

				token.ThrowIfCancellationRequested();

				lastProcessedItem = item;

				var success = false;

				switch (item.Action)
				{
					case FusionCacheAction.EntrySet:
						success = await TryProcessAutoRecoveryItemSetAsync(operationId, item, token).ConfigureAwait(false);
						break;
					case FusionCacheAction.EntryRemove:
						success = await TryProcessAutoRecoveryItemRemoveAsync(operationId, item, token).ConfigureAwait(false);
						break;
					case FusionCacheAction.EntryExpire:
						success = await TryProcessAutoRecoveryItemExpireAsync(operationId, item, token).ConfigureAwait(false);
						break;
					default:
						success = true;
						break;
				}

				if (success)
				{
					TryRemoveAutoRecoveryItem(operationId, item);
				}
				else
				{
					hasStopped = true;
					return false;
				}
			}

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): completed auto-recovery of {Count} items", CacheName, InstanceId, operationId, processedCount);
		}
		catch (OperationCanceledException)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): auto-recovery canceled after having processed {Count} items", CacheName, InstanceId, operationId, processedCount);
		}
		catch (Exception exc)
		{
			hasStopped = true;

			if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
				_logger.Log(_options.BackplaneErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred during a auto-recovery of an item ({RetryCount} retries left)", CacheName, InstanceId, operationId, lastProcessedItem?.CacheKey, lastProcessedItem?.RetryCount);
		}
		finally
		{
			if (hasStopped)
			{
				if (_logger?.IsEnabled(_options.BackplaneErrorsLogLevel) ?? false)
					_logger.Log(_options.BackplaneErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): stopped auto-recovery because of an error after {Count} processed items", CacheName, InstanceId, operationId, lastProcessedItem?.CacheKey, processedCount);

				if (lastProcessedItem is not null)
				{
					// UPDATE RETRY COUNT
					lastProcessedItem.RecordRetry();

					if (lastProcessedItem.CanRetry() == false)
					{
						TryRemoveAutoRecoveryItem(operationId, lastProcessedItem);

						if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
							_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): a auto-recovery item retried too many times, so it has been removed from the queue", CacheName, InstanceId, operationId, lastProcessedItem?.CacheKey);
					}
				}
			}

			// RELEASE THE LOCK
			_autoRecoveryProcessingLock.Release();
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessAutoRecoveryItemSetAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			if (dca.IsCurrentlyUsable(operationId, item.CacheKey) == false)
			{
				return false;
			}

			// TRY TO GET THE MEMORY CACHE
			var mca = GetCurrentMemoryAccessor(item.Options);

			if (mca is not null)
			{
				// TRY TO GET THE MEMORY ENTRY
				var memoryEntry = mca.GetEntryOrNull(operationId, item.CacheKey);

				if (memoryEntry is not null)
				{
					try
					{
						(var error, var isSame, var hasUpdated) = await TryUpdateMemoryEntryFromDistributedEntryUntypedAsync(operationId, item.CacheKey, memoryEntry).ConfigureAwait(false);

						if (error)
						{
							// STOP PROCESSING THE QUEUE
							return false;
						}

						if (hasUpdated)
						{
							// IF THE MEMORY ENTRY HAS BEEN UPDATED FROM THE DISTRIBUTED ENTRY, IT MEANS THAT THE DISTRIBUTED ENTRY
							// IS NEWER THAN THE MEMORY ENTRY, BECAUSE IT HAS BEEN UPDATED SINCE WE SET IT LOCALLY AND NOW IT'S
							// NEWER -> STOP HERE, ALL IS GOOD
							return true;
						}

						if (isSame == false)
						{
							// IF THE MEMORY ENTRY IS ALSO NOT THE SAME AS THE DISTRIBUTED ENTRY, IT MEANS THAT THE DISTRIBUTED ENTRY
							// IS EITHER OLDER OR IT'S NOT THERE AT ALL -> WE SET IT TO THE CURRENT ONE

							var dcaSuccess = await dca.SetEntryUntypedAsync(operationId, item.CacheKey, memoryEntry, item.Options, true, token).ConfigureAwait(false);
							if (dcaSuccess == false)
							{
								// STOP PROCESSING THE QUEUE
								return false;
							}
						}
					}
					catch
					{
						return false;
					}
				}
			}
		}

		// BACKPLANE
		var bpa = GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishSetAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessAutoRecoveryItemRemoveAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			var dcaSuccess = false;
			try
			{
				if (dca.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					dcaSuccess = await dca.RemoveEntryAsync(operationId, item.CacheKey, item.Options, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				dcaSuccess = false;
			}

			if (dcaSuccess == false)
			{
				return false;
			}
		}

		// BACKPLANE
		var bpa = GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishRemoveAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async ValueTask<bool> TryProcessAutoRecoveryItemExpireAsync(string operationId, AutoRecoveryItem item, CancellationToken token)
	{
		// DISTRIBUTED CACHE
		var dca = GetCurrentDistributedAccessor(item.Options);
		if (dca is not null)
		{
			var dcaSuccess = false;
			try
			{
				if (dca.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					dcaSuccess = await dca.RemoveEntryAsync(operationId, item.CacheKey, item.Options, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				dcaSuccess = false;
			}

			if (dcaSuccess == false)
			{
				return false;
			}
		}

		// BACKPLANE
		var bpa = GetCurrentBackplaneAccessor(item.Options);
		if (bpa is not null)
		{
			var bpaSuccess = false;
			try
			{
				if (bpa.IsCurrentlyUsable(operationId, item.CacheKey))
				{
					bpaSuccess = await bpa.PublishExpireAsync(operationId, item.CacheKey, item.Timestamp, item.Options, true, true, token).ConfigureAwait(false);
				}
			}
			catch
			{
				bpaSuccess = false;
			}

			if (bpaSuccess == false)
			{
				return false;
			}
		}

		return true;
	}

	internal async Task BackgroundAutoRecoveryAsync()
	{
		if (_autoRecoveryCts is null)
			return;

		try
		{
			var ct = _autoRecoveryCts.Token;
			while (!ct.IsCancellationRequested)
			{
				var operationId = FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
				var delay = _autoRecoveryDelay;
				var barrierTicks = Interlocked.Read(ref _autoRecoveryBarrierTicks);
				if (DateTimeOffset.UtcNow.Ticks < barrierTicks)
				{
					// SET THE NEW DELAY TO REACH THE BARRIER
					var oldDelay = delay;
					var newTicks = barrierTicks - DateTimeOffset.UtcNow.Ticks + 1_000;
					delay = TimeSpan.FromTicks(newTicks);
					if (delay < _autoRecoveryMinDelay)
					{
						delay = _autoRecoveryMinDelay;
						newTicks = delay.Ticks;
					}

					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): instead of the standard auto-recovery delay of {NormalDelay} the new delay is {NewDelay} ({NewDelayMs} ms, {NewDelayTicks} ticks)", CacheName, InstanceId, operationId, oldDelay, delay, delay.TotalMilliseconds, newTicks);
				}

				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): waiting {AutoRecoveryCurrentDelay} before the next try of auto-recovery", CacheName, InstanceId, operationId, delay);

				await Task.Delay(delay, ct);

				// AFTER THE DELAY, UPDATE THE BARRIER
				barrierTicks = Interlocked.Read(ref _autoRecoveryBarrierTicks);

				// CHECK AGAIN THE BARRIER (MAY HAVE BEEN UPDATED WHILE WAITING): IF UPDATED -> SKIP TO THE NEXT LOOP CYCLE
				if (DateTimeOffset.UtcNow.Ticks < barrierTicks)
				{
					if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
						_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId}): a barrier has been set after having awaited to start processing the auto-recovery queue: skipping to the next loop cycle", CacheName, InstanceId, operationId);

					continue;
				}

				ct.ThrowIfCancellationRequested();

				if (_autoRecoveryQueue.Count > 0)
				{
					_ = await TryProcessAutoRecoveryQueueAsync(operationId, ct);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// EMPTY
		}
	}
}
