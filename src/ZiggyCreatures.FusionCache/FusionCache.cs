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

/// <inheritdoc/>
[DebuggerDisplay("NAME: {_options.CacheName} - ID: {InstanceId} - DC: {HasDistributedCache} - BP: {HasBackplane}")]
public partial class FusionCache
	: IFusionCache
{
	private readonly FusionCacheOptions _options;
	private readonly ILogger? _logger;
	private readonly IFusionCacheReactor _reactor;
	private MemoryCacheAccessor _mca;
	private DistributedCacheAccessor? _dca;
	private BackplaneAccessor? _bpa;
	private FusionCacheEventsHub _events;
	private readonly List<IFusionCachePlugin> _plugins;
	private readonly object _lockBackplane = new object();

	/// <summary>
	/// Creates a new <see cref="FusionCache"/> instance.
	/// </summary>
	/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
	/// <param name="memoryCache">The <see cref="IMemoryCache"/> instance to use. If null, one will be automatically created and managed.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	/// <param name="reactor">The <see cref="IFusionCacheReactor"/> instance to use (advanced). If null, a standard one will be automatically created and managed.</param>
	public FusionCache(IOptions<FusionCacheOptions> optionsAccessor, IMemoryCache? memoryCache = null, ILogger<FusionCache>? logger = null, IFusionCacheReactor? reactor = null)
	{
		// GLOBALLY UNIQUE INSTANCE ID
		InstanceId = Guid.NewGuid().ToString("N");

		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

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
		_mca = new MemoryCacheAccessor(memoryCache, _logger, _events.Memory);

		// DISTRIBUTED CACHE
		_dca = null;

		// BACKPLANE
		_bpa = null;
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

	private string GenerateOperationId()
	{
		return FusionCacheInternalUtils.MaybeGenerateOperationId(_logger);
	}

	private DistributedCacheAccessor? GetCurrentDistributedAccessor(FusionCacheEntryOptions options)
	{
		return options.SkipDistributedCache ? null : _dca;
	}

	private IFusionCacheEntry? MaybeGetFallbackEntry<TValue>(string operationId, string key, FusionCacheDistributedEntry<TValue>? distributedEntry, FusionCacheMemoryEntry? memoryEntry, FusionCacheEntryOptions options, out bool failSafeActivated)
	{
		failSafeActivated = false;

		if (options.IsFailSafeEnabled)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): trying to activate FAIL-SAFE", operationId, key);
			if (distributedEntry is not null)
			{
				// FAIL SAFE (FROM DISTRIBUTED)
				if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
					_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from distributed)", operationId, key);
				failSafeActivated = true;

				// EVENT
				_events.OnFailSafeActivate(operationId, key);

				return distributedEntry;
			}
			else if (memoryEntry is not null)
			{
				// FAIL SAFE (FROM MEMORY)
				if (_logger?.IsEnabled(_options.FailSafeActivationLogLevel) ?? false)
					_logger.Log(_options.FailSafeActivationLogLevel, "FUSION (O={CacheOperationId} K={CacheKey}): FAIL-SAFE activated (from memory)", operationId, key);
				failSafeActivated = true;

				// EVENT
				_events.OnFailSafeActivate(operationId, key);

				return memoryEntry;
			}
			else
			{
				if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
					_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): unable to activate FAIL-SAFE (no entries in memory or distributed)", operationId, key);
				return null;
			}
		}
		else
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): FAIL-SAFE not enabled", operationId, key);
			return null;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MaybeBackgroundCompleteTimedOutFactory<TValue>(string operationId, string key, FusionCacheFactoryExecutionContext ctx, Task<TValue?>? factoryTask, FusionCacheEntryOptions options, DistributedCacheAccessor? dca, CancellationToken token)
	{
		if (options.AllowTimedOutFactoryBackgroundCompletion == false || factoryTask is null)
			return;

		if (factoryTask.IsFaulted)
		{
			if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
				_logger.Log(_options.FactoryErrorsLogLevel, factoryTask.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (O={CacheOperationId} K={CacheKey}): a timed-out factory thrown an exception", operationId, key);
			return;
		}

		// CONTINUE IN THE BACKGROUND TO TRY TO KEEP THE RESULT AS SOON AS IT WILL COMPLETE SUCCESSFULLY
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): trying to complete the timed-out factory in the background", operationId, key);

		_ = factoryTask.ContinueWith(antecedent =>
		{
			if (antecedent.Status == TaskStatus.Faulted)
			{
				if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
					_logger.Log(_options.FactoryErrorsLogLevel, antecedent.Exception.GetSingleInnerExceptionOrSelf(), "FUSION (O={CacheOperationId} K={CacheKey}): a timed-out factory thrown an exception", operationId, key);

				// EVENT
				_events.OnBackgroundFactoryError(operationId, key);
			}
			else if (antecedent.Status == TaskStatus.RanToCompletion)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): a timed-out factory successfully completed in the background: keeping the result", operationId, key);

				// UPDATE ADAPTIVE OPTIONS
				var maybeNewOptions = ctx.GetOptions();
				if (maybeNewOptions is not null && options != maybeNewOptions)
					options = maybeNewOptions;

				var lateEntry = FusionCacheMemoryEntry.CreateFromOptions(antecedent.Result, options, false);
				_ = dca?.SetEntryAsync<TValue>(operationId, key, lateEntry, options, token);
				_mca.SetEntry<TValue>(operationId, key, lateEntry, options);

				_events.OnSet(operationId, key);

				// EVENT
				_events.OnBackgroundFactorySuccess(operationId, key);
			}
		});
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReleaseLock(string operationId, string key, object? lockObj)
	{
		if (lockObj is null)
			return;

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): releasing LOCK", operationId, key);

		try
		{
			_reactor.ReleaseLock(key, operationId, lockObj, _logger);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.LogTrace("FUSION (O={CacheOperationId} K={CacheKey}): LOCK released", operationId, key);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.LogWarning(exc, "FUSION (O={CacheOperationId} K={CacheKey}): releasing the LOCK has thrown an exception", operationId, key);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ProcessFactoryError(string operationId, string key, Exception exc)
	{
		if (exc is SyntheticTimeoutException)
		{
			if (_logger?.IsEnabled(_options.FactorySyntheticTimeoutsLogLevel) ?? false)
				_logger.Log(_options.FactorySyntheticTimeoutsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): a synthetic timeout occurred while calling the factory", operationId, key);

			// EVENT
			_events.OnFactorySyntheticTimeout(operationId, key);

			return;
		}

		if (_logger?.IsEnabled(_options.FactoryErrorsLogLevel) ?? false)
			_logger.Log(_options.FactoryErrorsLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while calling the factory", operationId, key);

		// EVENT
		_events.OnFactoryError(operationId, key);
	}

	internal void EvictInternal(string key, bool allowFailSafe)
	{
		ValidateCacheKey(key);

		// TODO: BETTER CHECK THIS POTENTIAL NullReferenceException HERE
		if (_mca is null)
			return;

		var operationId = GenerateOperationId();

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION (O={CacheOperationId} K={CacheKey}): calling Evict", operationId, key);

		_mca.EvictEntry(operationId, key, allowFailSafe);
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
			_logger.LogDebug("FUSION: setup distributed cache (CACHE={DistributedCacheType} SERIALIZER={SerializerType})", distributedCache.GetType().FullName, serializer.GetType().FullName);

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveDistributedCache()
	{
		_dca = null;

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.LogDebug("FUSION: distributed cache removed");

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

		lock (_lockBackplane)
		{
			_bpa = new BackplaneAccessor(this, backplane, _options, _logger, _events.Backplane);
			_bpa.Subscribe();

			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.LogDebug("FUSION: setup backplane (BACKPLANE={BackplaneType})", backplane.GetType().FullName);
		}

		// CHECK: WARN THE USER IN CASE OF
		// - HAS A MEMORY CACHE (ALWAYS)
		// - HAS BACKPLANE
		// - DOES *NOT* HAVE A DISTRIBUTED CACHE
		// - THE OPTION DefaultEntryOptions.EnableBackplaneNotifications IS TRUE
		if (HasBackplane && HasDistributedCache == false && DefaultEntryOptions.EnableBackplaneNotifications)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.LogWarning("FUSION: it has been detected a situation where there *IS* a backplane, there is *NOT* a distributed cache and the DefaultEntryOptions.EnableBackplaneNotifications option is set to true. This will probably cause problems, since a notification will be sent automatically at every change in the cache but there is not a shared state (a distributed cache) that different nodes can use, basically resulting in a situation where the cache will keep invalidating itself at every change. It is suggested to either (1) add a distributed cache or (2) change the DefaultEntryOptions.EnableBackplaneNotifications to false.", backplane.GetType().FullName);
		}

		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveBackplane()
	{
		lock (_lockBackplane)
		{
			if (_bpa is not null)
			{
				_bpa.Unsubscribe();
				_bpa = null;

				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.LogDebug("FUSION: backplane removed");
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
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger?.LogError("FUSION: the same plugin instance already exists (TYPE={PluginType})", plugin.GetType().FullName);

				throw new InvalidOperationException($"FUSION: the same plugin instance already exists (TYPE={plugin.GetType().FullName})");
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

			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.LogError(exc, "FUSION: an error occurred while starting a plugin (TYPE={PluginType})", plugin.GetType().FullName);

			throw new InvalidOperationException($"FUSION: an error occurred while starting a plugin (TYPE={plugin.GetType().FullName})", exc);
		}

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger?.LogInformation("FUSION: a plugin has been added and started (TYPE={PluginType})", plugin.GetType().FullName);
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
				if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
					_logger?.LogWarning("FUSION: the plugin cannot be removed because is not part of this FusionCache instance (TYPE={PluginType})", plugin.GetType().FullName);

				// MAYBE WE SHOULD THROW (LIKE IN AddPlugin) INSTEAD OF JUST RETURNING (LIKE IN List<T>.Remove()) ?
				return false;
				//throw new InvalidOperationException($"FUSION: the plugin cannot be removed because is not part of this FusionCache instance (TYPE={plugin.GetType().FullName})");
			}

			// STOP THE PLUGIN
			try
			{
				plugin.Stop(this);
			}
			catch (Exception exc)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.LogError(exc, "FUSION: an error occurred while stopping a plugin (TYPE={PluginType})", plugin.GetType().FullName);

				throw new InvalidOperationException($"FUSION: an error occurred while stopping a plugin (TYPE={plugin.GetType().FullName})", exc);
			}
			finally
			{
				// REMOVE THE PLUGIN
				_plugins.Remove(plugin);
			}
		}

		if (_logger?.IsEnabled(LogLevel.Information) ?? false)
			_logger?.LogInformation("FUSION: a plugin has been stopped and removed (TYPE={PluginType})", plugin.GetType().FullName);

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
