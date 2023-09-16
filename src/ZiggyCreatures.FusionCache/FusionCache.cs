using System;
using System.Collections.Generic;
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
[DebuggerDisplay("NAME: {_options.CacheName} - ID: {InstanceId} - DC: {HasDistributedCache} - BP: {HasBackplane}")]
public partial class FusionCache
	: IFusionCache
{
	private readonly FusionCacheOptions _options;
	private readonly string? _cacheKeyPrefix;
	private readonly ILogger? _logger;
	private readonly IFusionCacheReactor _reactor;
	private MemoryCacheAccessor _mca;
	private DistributedCacheAccessor? _dca;
	private BackplaneAccessor? _bpa;
	private readonly object _backplaneLock = new object();
	private FusionCacheEventsHub _events;
	private readonly List<IFusionCachePlugin> _plugins;

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

	private MemoryCacheAccessor? GetCurrentMemoryAccessor(FusionCacheEntryOptions options)
	{
		return options.SkipMemoryCache ? null : _mca;
	}

	internal DistributedCacheAccessor? GetCurrentDistributedAccessor(FusionCacheEntryOptions options)
	{
		return options.SkipDistributedCache ? null : _dca;
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
					var lateEntry = FusionCacheMemoryEntry.CreateFromOptions(antecedent.Result, options, false, ctx.LastModified, ctx.ETag, null);

					var mca = GetCurrentMemoryAccessor(options);
					if (mca is not null)
					{
						mca.SetEntry<TValue>(operationId, key, lateEntry, options);
					}

					await ExecuteDistributedSetAsync<TValue>(operationId, key, lateEntry, options, token).ConfigureAwait(false);

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

	internal bool MaybeExpireMemoryEntryInternal(string operationId, string key, bool allowFailSafe, long timestampThreshold)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): calling MaybeExpireMemoryEntryInternal (allowFailSafe={AllowFailSafe})", CacheName, operationId, key, allowFailSafe);

		if (_mca is null)
			return false;

		var (memoryEntry, memoryEntryIsValid) = _mca.TryGetEntry(operationId, key);

		// IF NO ENTRY -> DO NOTHING
		if (memoryEntry is null)
			return false;

		if (memoryEntry.Timestamp >= timestampThreshold)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName}] (O={CacheOperationId} K={CacheKey}): timestamp of cached entry {TimestampCached} was greater than the specified threshold {TimestampThreshold}", CacheName, operationId, key, memoryEntry.Timestamp, timestampThreshold);

			return false;
		}

		return _mca.ExpireEntry(operationId, key, allowFailSafe);
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
				RemoveAllPlugins();
				RemoveBackplane();
				RemoveDistributedCache();
				_reactor.Dispose();
				_mca.Dispose();
			}
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_mca = null;
			_events = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
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
}
