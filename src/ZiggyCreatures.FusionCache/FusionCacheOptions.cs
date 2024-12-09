using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents all the options available for the entire <see cref="IFusionCache"/> instance.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
/// </summary>
public class FusionCacheOptions
	: IOptions<FusionCacheOptions>
{
	private string _cacheName;
	private FusionCacheEntryOptions _defaultEntryOptions;

	/// <summary>
	/// The default value for <see cref="IFusionCache.CacheName"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md"/>
	/// </summary>
	public const string DefaultCacheName = "FusionCache";

	/// <summary>
	/// The wire format version identifier for the distributed cache wire format, used in the cache key processing.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public const string DistributedCacheWireFormatVersion = "v2p3";

	/// <summary>
	/// The wire format version separator for the distributed cache wire format, used in the cache key processing.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public const string DistributedCacheWireFormatSeparator = ":";

	/// <summary>
	/// The wire format version identifier for the backplane wire format, used in the channel name.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public const string BackplaneWireFormatVersion = "v2p3";

	/// <summary>
	/// The wire format version separator for the backplane wire format, used in the channel name.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public const string BackplaneWireFormatSeparator = ":";

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheOptions"/> object.
	/// </summary>
	public FusionCacheOptions()
	{
		_cacheName = DefaultCacheName;

		_defaultEntryOptions = new FusionCacheEntryOptions();

		TagsMemoryCacheDurationOverride = TimeSpan.FromSeconds(30);

		SkipAutoCloneForImmutableObjects = true;

		// AUTO-RECOVERY
		EnableAutoRecovery = true;
		AutoRecoveryMaxItems = null;
		AutoRecoveryMaxRetryCount = null;
		AutoRecoveryDelay = TimeSpan.FromMilliseconds(5_000);

		// LOG LEVELS
		IncoherentOptionsNormalizationLogLevel = LogLevel.Warning;
		SerializationErrorsLogLevel = LogLevel.Error;
		DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Warning;
		DistributedCacheErrorsLogLevel = LogLevel.Warning;
		FactorySyntheticTimeoutsLogLevel = LogLevel.Warning;
		FactoryErrorsLogLevel = LogLevel.Warning;
		FailSafeActivationLogLevel = LogLevel.Warning;
		EventHandlingErrorsLogLevel = LogLevel.Warning;
		BackplaneSyntheticTimeoutsLogLevel = LogLevel.Warning;
		BackplaneErrorsLogLevel = LogLevel.Warning;
		PluginsInfoLogLevel = LogLevel.Information;
		PluginsErrorsLogLevel = LogLevel.Error;
	}

	/// <summary>
	/// The name of the cache: it can be used for identification, and in a multi-node scenario it is typically shared between nodes to create a logical association.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if you try to set this to a null/whitespace value, that value will be ignored.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md"/>
	/// </summary>
	public string CacheName
	{
		get { return _cacheName; }
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				//throw new ArgumentNullException(nameof(value), "It is not possible to set the CacheName to null");
				return;
			}

			_cacheName = value;
		}
	}

	/// <summary>
	/// The instance id of the cache: it will be used for low-level identification for the same logical cache between different nodes in a multi-node scenario: it is automatically set to a random value.
	/// </summary>
	public string? InstanceId { get; private set; }

	/// <summary>
	/// Set the InstanceId of the cache, but please don't use this.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> again, this should NOT be set, basically never ever, unless you really know what you are doing. For example by using the same value for two different cache instances they will be considered as the same cache, and this will lead to critical errors. So again, really: you should not use this.
	/// </summary>
	/// <param name="instanceId"></param>
	public void SetInstanceId(string instanceId)
	{
		InstanceId = instanceId;
	}

	/// <summary>
	/// The default <see cref="FusionCacheEntryOptions"/> to use when none will be specified, and as the starting point when duplicating one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown if an attempt is made to set this property to <see langword="null"/>.</exception>
	public FusionCacheEntryOptions DefaultEntryOptions
	{
		get { return _defaultEntryOptions; }
		set
		{
			if (value is null)
				throw new ArgumentNullException(nameof(value), "It is not possible to set the DefaultEntryOptions to null");

			_defaultEntryOptions = value;
		}
	}

	/// <summary>
	/// The default <see cref="FusionCacheEntryOptions"/> to use for the tag expiration data when none will be specified, and as the starting point when duplicating one.
	/// <br/>
	/// This is used by features like RemoveByTag() and Clear().
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// <br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	public FusionCacheEntryOptions? TagsDefaultEntryOptions { get; set; }

	/// <summary>
	/// The default Duration that will be automatically used for the memory level with <see cref="TagsDefaultEntryOptions"/>, when there is a distributed cache but no backplane.
	/// <br/>
	/// This is used by features like RemoveByTag() and Clear(), and is useful to reduce the time different memory caches in different nodes remain out-of-sync when not using a backplane.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if you manually specify a custom <see cref="TagsDefaultEntryOptions"/>, this option will not be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// <br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	public TimeSpan TagsMemoryCacheDurationOverride { get; set; }

	/// <summary>
	/// The duration of the circuit-breaker used when working with the distributed cache. Defaults to <see cref="TimeSpan.Zero"/>, which means the circuit-breaker will never be activated.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public TimeSpan DistributedCacheCircuitBreakerDuration { get; set; }

	/// <summary>
	/// Execute event handlers in a sync fashion, waiting for all of them to complete before moving on.
	/// <br/><br/>
	/// <strong>WARNING:</strong> by default this option is NOT enabled, and should remain this way in any normal circumstance unless you really know what you are doing.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Events.md"/>
	/// </summary>
	public bool EnableSyncEventHandlersExecution { get; set; }

	/// <summary>
	/// A prefix that will be added to each cache key for each call: it can be useful when working with multiple named caches.
	/// <br/><br/>
	/// <strong>EXAMPLE</strong>: if the CacheKeyPrefix specified is "MyCache:", a later call to cache.GetOrDefault("Product/123") will actually work on the cache key "MyCache:Product/123".
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md"/>
	/// </summary>
	public string? CacheKeyPrefix { get; set; }

	/// <summary>
	/// Specify the mode in which cache key will be changed for the distributed cache, for internal purposes (eg: to specify the wire format version).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public CacheKeyModifierMode DistributedCacheKeyModifierMode { get; set; }

	/// <summary>
	/// The duration of the circuit-breaker used when working with the backplane. Defaults to <see cref="TimeSpan.Zero"/>, which means the circuit-breaker will never be activated.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public TimeSpan BackplaneCircuitBreakerDuration { get; set; }

	/// <summary>
	/// The prefix to use in the backplane channel name: if not specified the <see cref="CacheName"/> will be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public string? BackplaneChannelPrefix { get; set; }

	/// <summary>
	/// Ignores incoming backplane notifications, which normally is <strong>DANGEROUS</strong>.
	/// <br/><br/>
	/// <strong>WARNING:</strong> it is advised not to ignore backplane notifications in any normal circumstance unless you really know what you are doing.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public bool IgnoreIncomingBackplaneNotifications { get; set; }

	/// <summary>
	/// When AutoClone is enabled, skips it anyway for immutable objects.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoClone.md"/>
	/// </summary>
	public bool SkipAutoCloneForImmutableObjects { get; set; }

	/// <summary>
	/// DEPRECATED: please use EnableAutoRecovery.
	/// <br/><br/>
	/// Enable auto-recovery for the backplane notifications to better handle transient errors without generating synchronization issues: notifications that failed to be sent out will be retried later on, when the backplane becomes responsive again.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Backplane auto-recovery is now simply auto-recovery: please use the EnableAutoRecovery property.", true)]
	public bool EnableBackplaneAutoRecovery
	{
		get { return EnableAutoRecovery; }
		set { EnableAutoRecovery = value; }
	}

	/// <summary>
	/// Enable auto-recovery to automatically handle transient errors to minimize synchronization issues.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	public bool EnableAutoRecovery { get; set; }

	/// <summary>
	/// DEPRECATED: please use AutoRecoveryMaxItems.
	/// <br/><br/>
	/// The maximum number of items in the auto-recovery queue: this can help reducing memory consumption. If set to <see langword="null"/> there will be no limit.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Backplane auto-recovery is now simply auto-recovery: please use the AutoRecoveryMaxItems property.", true)]
	public int? BackplaneAutoRecoveryMaxItems
	{
		get { return AutoRecoveryMaxItems; }
		set { AutoRecoveryMaxItems = value; }
	}

	/// <summary>
	/// The maximum number of items in the auto-recovery queue: this is usually not needed, but it may help reducing memory consumption in extreme scenarios.
	/// <br/>
	/// When set to null <see langword="null"/> there will be no limits.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	public int? AutoRecoveryMaxItems { get; set; }

	/// <summary>
	/// DEPRECATED: please use AutoRecoveryMaxRetryCount.
	/// <br/><br/>
	/// The maximum number of retries for a auto-recovery item: after this amount the item is discarded, to avoid keeping it retrying forever. If set to <see langword="null"/> there will be no limit.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Backplane auto-recovery is now simply auto-recovery: please use the AutoRecoveryMaxRetryCount property.", true)]
	public int? BackplaneAutoRecoveryMaxRetryCount
	{
		get { return AutoRecoveryMaxRetryCount; }
		set { AutoRecoveryMaxRetryCount = value; }
	}

	/// <summary>
	/// The maximum number of retries for a auto-recovery item: after this amount an item is discarded, to avoid keeping it for too long.
	/// Please note though that a cleanup is automatically performed, so in theory there's no need to set this.
	/// <br/>
	/// When set to <see langword="null"/> there will be no limits.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	public int? AutoRecoveryMaxRetryCount { get; set; }

	/// <summary>
	/// DEPRECATED: please use AutoRecoveryDelay.
	/// <br/><br/>
	/// The amount of time to wait, after a backplane reconnection, before trying to process the auto-recovery queue: this may be useful to allow all the other nodes to be ready.
	/// <br/>
	/// Use <see cref="TimeSpan.Zero"/> to avoid any delay (risky).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Backplane auto-recovery is now simply auto-recovery: please use the AutoRecoveryDelay property.", true)]
	public TimeSpan BackplaneAutoRecoveryReconnectDelay
	{
		get { return AutoRecoveryDelay; }
		set { AutoRecoveryDelay = value; }
	}

	/// <summary>
	/// DEPRECATED: please use AutoRecoveryDelay.
	/// <br/><br/>
	/// The amount of time to wait before actually processing the auto-recovery queue, to better handle backpressure.
	/// <br/>
	/// Use <see cref="TimeSpan.Zero"/> to avoid any delay (risky).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Backplane auto-recovery is now simply auto-recovery: please use the AutoRecoveryDelay property.", true)]
	public TimeSpan BackplaneAutoRecoveryDelay
	{
		get; set;
	}

	/// <summary>
	/// The amount of time to wait before actually processing the auto-recovery queue, to better handle backpressure.
	/// <br/>
	/// Use <see cref="TimeSpan.Zero"/> to avoid any delay (risky, like very very risky).
	/// <br/><br/>
	/// <strong>NOTE:</strong> when used with a distributed cache that supports a delayed reconnection logic (like StackExchange.Redis), set this to a higher value than the one used by the distributed cache, to avoid sending backplane notifications when not all nodes are reconnected yet, therefore avoiding that some nodes will not receive all the notifications.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	public TimeSpan AutoRecoveryDelay { get; set; }

	/// <summary>
	/// Enable expiring a cache entry, only on the distributed cache (if any), when an auto-recovery message is being published on the backplane, to ensure that the value in the distributed cache will not be stale.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("This is not needed anymore, everything is handled automatically now.", true)]
	public bool EnableDistributedExpireOnBackplaneAutoRecovery { get; set; }

	/// <summary>
	/// If enabled, and re-throwing of exceptions is also enabled (see <see cref="FusionCacheEntryOptions.ReThrowSerializationExceptions"/>, <see cref="FusionCacheEntryOptions.ReThrowDistributedCacheExceptions"/> or  <see cref="FusionCacheEntryOptions.ReThrowBackplaneExceptions"/>), it will re-throw the original exception as-is instead of wrapping it into one of the available specific exceptions (<see cref="FusionCacheSerializationException"/>, <see cref="FusionCacheDistributedCacheException"/> or <see cref="FusionCacheBackplaneException"/>).
	/// </summary>
	public bool ReThrowOriginalExceptions { get; set; }

	/// <summary>
	/// Specify that, even when calling async code, the sync version of the serialization methods should be preferred.
	/// </summary>
	public bool PreferSyncSerialization { get; set; }

	/// <summary>
	/// Include tags when logging a cache entry: since tags may contain sensitive data, be careful about enabling this.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public bool IncludeTagsInLogs { get; set; }

	/// <summary>
	/// Include tags when doing distributed tracing: since tags may contain sensitive data, be careful about enabling this.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/OpenTelemetry.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public bool IncludeTagsInTraces { get; set; }

	/// <summary>
	/// Include tags when doing distributed metrics: since tags may contain sensitive data, be careful about enabling this.
	/// <br/>
	/// <strong>NOTE:</strong> also, typically metrics are better NOT to have tags with high-cardinality, meaning with a lot of different values, so please be extra careful.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/OpenTelemetry.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public bool IncludeTagsInMetrics { get; set; }

	/// <summary>
	/// If set to <see langword="true"/>, disables the entire tagging system, meaning both RemoveByTag and Clear.
	/// <br/>
	/// <strong>NOTE:</strong> this may get to a little performance improvement, but if you'll try to call one of the above methods an <see cref="InvalidOperationException"></see> will be thrown.
	/// </summary>
	public bool DisableTagging { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when some options have incoherent values that have been fixed with a normalization, like for example when a FailSafeMaxDuration is lower than a Duration, so the Duration is used instead.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel IncoherentOptionsNormalizationLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when an error occurs during serialization or deserialization while working with the distributed cache.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel SerializationErrorsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a distributed cache operation.
	/// <br/><br/>
	/// <strong>NOTE:</strong> synthetic timeouts are only related to soft/hard timeouts, and are not related to intrinsic timeout exceptions or similar that may be thrown by your distributed cache.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel DistributedCacheSyntheticTimeoutsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when any error (except for a synthetic timeout) occurs during a distributed cache operation.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel DistributedCacheErrorsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a factory execution.
	/// <br/><br/>
	/// <strong>NOTE:</strong> synthetic timeouts are only related to soft/hard timeouts, and are not related to intrinsic timeout exceptions or similar that may be thrown by your database, services or else.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel FactorySyntheticTimeoutsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when any error (except for a synthetic timeout) occurs during a factory execution.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel FactoryErrorsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when a fail-safe activation occurs.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel FailSafeActivationLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when an error occurs during the handling of an event.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel EventHandlingErrorsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when a synthetic timeout occurs during a backplane cache operation.
	/// <br/><br/>
	/// <strong>NOTE:</strong> synthetic timeouts are only related to soft/hard timeouts, and are not related to intrinsic timeout exceptions or similar that may be thrown by your backplane.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel BackplaneSyntheticTimeoutsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when an error occurs during a backplane operation.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel BackplaneErrorsLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when logging info about plugins.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel PluginsInfoLogLevel { get; set; }

	/// <summary>
	/// Specify the <see cref="LogLevel"/> to use when an error occurs while working with a plugin.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Logging.md"/>
	/// </summary>
	public LogLevel PluginsErrorsLogLevel { get; set; }

	FusionCacheOptions IOptions<FusionCacheOptions>.Value
	{
		get { return this; }
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheOptions"/> object by duplicating all the options of the current one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	/// <returns>The newly created <see cref="FusionCacheOptions"/> object.</returns>
	public FusionCacheOptions Duplicate()
	{
		var res = new FusionCacheOptions
		{
			CacheName = CacheName,
			InstanceId = InstanceId,

			CacheKeyPrefix = CacheKeyPrefix,

			DefaultEntryOptions = DefaultEntryOptions.Duplicate(),
			TagsDefaultEntryOptions = TagsDefaultEntryOptions?.Duplicate(),
			TagsMemoryCacheDurationOverride = TagsMemoryCacheDurationOverride,

			EnableAutoRecovery = EnableAutoRecovery,
			AutoRecoveryDelay = AutoRecoveryDelay,
			AutoRecoveryMaxItems = AutoRecoveryMaxItems,
			AutoRecoveryMaxRetryCount = AutoRecoveryMaxRetryCount,

			BackplaneChannelPrefix = BackplaneChannelPrefix,
			IgnoreIncomingBackplaneNotifications = IgnoreIncomingBackplaneNotifications,
			BackplaneCircuitBreakerDuration = BackplaneCircuitBreakerDuration,

			DistributedCacheKeyModifierMode = DistributedCacheKeyModifierMode,
			DistributedCacheCircuitBreakerDuration = DistributedCacheCircuitBreakerDuration,

			EnableSyncEventHandlersExecution = EnableSyncEventHandlersExecution,

			ReThrowOriginalExceptions = ReThrowOriginalExceptions,

			PreferSyncSerialization = PreferSyncSerialization,

			IncludeTagsInLogs = IncludeTagsInLogs,
			IncludeTagsInTraces = IncludeTagsInTraces,
			IncludeTagsInMetrics = IncludeTagsInMetrics,

			DisableTagging = DisableTagging,

			SkipAutoCloneForImmutableObjects = SkipAutoCloneForImmutableObjects,

			// LOG LEVELS
			IncoherentOptionsNormalizationLogLevel = IncoherentOptionsNormalizationLogLevel,

			FailSafeActivationLogLevel = FailSafeActivationLogLevel,
			FactorySyntheticTimeoutsLogLevel = FactorySyntheticTimeoutsLogLevel,
			FactoryErrorsLogLevel = FactoryErrorsLogLevel,

			DistributedCacheSyntheticTimeoutsLogLevel = DistributedCacheSyntheticTimeoutsLogLevel,
			DistributedCacheErrorsLogLevel = DistributedCacheErrorsLogLevel,
			SerializationErrorsLogLevel = SerializationErrorsLogLevel,

			BackplaneSyntheticTimeoutsLogLevel = BackplaneSyntheticTimeoutsLogLevel,
			BackplaneErrorsLogLevel = BackplaneErrorsLogLevel,

			EventHandlingErrorsLogLevel = EventHandlingErrorsLogLevel,

			PluginsErrorsLogLevel = PluginsErrorsLogLevel,
			PluginsInfoLogLevel = PluginsInfoLogLevel,
		};

		return res;
	}
}
