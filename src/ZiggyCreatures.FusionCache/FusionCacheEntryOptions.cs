using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents all the options available for a single <see cref="IFusionCache"/> entry.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
/// </summary>
public sealed class FusionCacheEntryOptions
{
	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheEntryOptions"/> object.
	/// </summary>
	/// <param name="duration">The value for the <see cref="Duration"/> option. If null, <see cref="FusionCacheGlobalDefaults.EntryOptionsDuration"/> will be used.</param>
	public FusionCacheEntryOptions(TimeSpan? duration = null)
	{
		Duration = duration ?? FusionCacheGlobalDefaults.EntryOptionsDuration;
		LockTimeout = FusionCacheGlobalDefaults.EntryOptionsLockTimeout;
		JitterMaxDuration = FusionCacheGlobalDefaults.EntryOptionsJitterMaxDuration;
		Size = FusionCacheGlobalDefaults.EntryOptionsSize;
		Priority = FusionCacheGlobalDefaults.EntryOptionsPriority;

		FactorySoftTimeout = FusionCacheGlobalDefaults.EntryOptionsFactorySoftTimeout;
		FactoryHardTimeout = FusionCacheGlobalDefaults.EntryOptionsFactoryHardTimeout;
		AllowTimedOutFactoryBackgroundCompletion = FusionCacheGlobalDefaults.EntryOptionsAllowTimedOutFactoryBackgroundCompletion;

		IsFailSafeEnabled = FusionCacheGlobalDefaults.EntryOptionsIsFailSafeEnabled;
		FailSafeMaxDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeMaxDuration;
		FailSafeThrottleDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeThrottleDuration;

		DistributedCacheDuration = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheDuration;
		DistributedCacheFailSafeMaxDuration = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheFailSafeMaxDuration;
		DistributedCacheSoftTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheSoftTimeout;
		DistributedCacheHardTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheHardTimeout;
		AllowBackgroundDistributedCacheOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundDistributedCacheOperations;
		ReThrowDistributedCacheExceptions = FusionCacheGlobalDefaults.EntryOptionsReThrowDistributedCacheExceptions;
		ReThrowSerializationExceptions = FusionCacheGlobalDefaults.EntryOptionsReThrowSerializationExceptions;

		SkipBackplaneNotifications = FusionCacheGlobalDefaults.EntryOptionsSkipBackplaneNotifications;
		AllowBackgroundBackplaneOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundBackplaneOperations;
		ReThrowBackplaneExceptions = FusionCacheGlobalDefaults.EntryOptionsReThrowBackplaneExceptions;

		SkipDistributedCacheRead = FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheRead;
		SkipDistributedCacheWrite = FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheWrite;
		SkipDistributedCacheReadWhenStale = FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheReadWhenStale;

		SkipMemoryCacheRead = FusionCacheGlobalDefaults.EntryOptionsSkipMemoryCacheRead;
		SkipMemoryCacheWrite = FusionCacheGlobalDefaults.EntryOptionsSkipMemoryCacheWrite;
	}

	/// <summary>
	/// The amount of time after which a cache entry is <strong>considered expired</strong>.
	/// <br/><br/>
	/// Please note the wording "considered expired" here: what it means is that, although from the OUTSIDE what is observed is always the same (a piece of data logically expires after the specified <see cref="Duration"/>), on the INSIDE things change depending on the fact that fail-safe is enabled or not.
	/// <br/>
	/// More specifically:
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="false"/> the <see cref="Duration"/> corresponds to the actual underlying duration in the cache, nothing more, nothing less
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled" /> is set to <see langword="true"/>, the underlying duration in the cache corresponds to <see cref="FailSafeMaxDuration"/> and the <see cref="Duration"/> option is used internally as a way to indicate when the data should be considered stale (expired), without making it actually expire inside the cache levels (memory and/or distributed)
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public TimeSpan Duration { get; set; }

	private float? _eagerRefreshThreshold = null;

	/// <summary>
	/// The threshold to apply when deciding whether to refresh the cache entry eagerly (that is, before the actual expiration).
	/// <br/>
	/// This value is intended as a percentage of the <see cref="Duration"/> option, expressed as a value between 0.0 and 1.0 (eg: 0.5 = 50%, 0.75 = 75%, etc).
	/// <br/><br/>
	/// For example by setting it to 0.8 (80%) with a <see cref="Duration"/> of 10 minutes, if there's a cache access for the entry after 8 minutes (80% of 10 minutes) an eager refresh will automatically start in the background, while immediately returning the (still valid) cached value to the caller.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public float? EagerRefreshThreshold
	{
		get { return _eagerRefreshThreshold; }
		set
		{
			if (value.HasValue)
			{
				if (value.Value <= 0.0f)
					value = null;
				else if (value.Value >= 1.0f)
					value = null;
			}
			_eagerRefreshThreshold = value;
		}
	}

	/// <summary>
	/// The timeout to apply when trying to acquire a memory lock during a factory execution.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	public TimeSpan LockTimeout { get; set; }

	/// <summary>
	/// The maximum amount of extra duration to add to the normal <see cref="Duration"/> in a randomized way, to allow for more variable expirations.
	/// <br/>
	/// This may be useful in a horizontal scalable scenario(eg: multi-node scenario).
	/// </summary>
	public TimeSpan JitterMaxDuration { get; set; }

	/// <summary>
	/// The size of the cache entry, used as a value for the <see cref="MemoryCacheEntryOptions.Size"/> option in the underlying memory cache.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/StepByStep.md"/>
	/// </summary>
	public long? Size { get; set; }

	/// <summary>
	/// The <see cref="CacheItemPriority"/> of the entry in the underlying memory cache.
	/// </summary>
	public CacheItemPriority Priority { get; set; }

	/// <summary>
	/// Enable the fail-safe mechanism, which will be activated if and when something goes wrong while calling a factory or getting data from a distributed cache.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public bool IsFailSafeEnabled { get; set; }

	/// <summary>
	/// When fail-safe is enabled this is the maximum amount of time a cache entry can be used in case of problems, even if expired.
	/// <br/><br/>
	/// Specifically:
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="true"/>, an entry will apparently expire normally after the specified <see cref="Duration"/>: behind the scenes though it will also be kept around for this (usually long) amount of time, so it may be used as a fallback value in case of problems
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="false"/>, this is ignored
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public TimeSpan FailSafeMaxDuration { get; set; }

	/// <summary>
	/// If fail-safe is enabled, something goes wrong while getting data (from the distributed cache or while calling the factory) and there is an expired entry to be used as a fallback value, the fail-safe mechanism will actually be activated.
	/// In that case the fallback value will not only be returned to the caller but also put in the cache for this duration (usually small) to avoid excessive load on the distributed cache and/or the factory getting called continuously.
	/// <br/><br/>
	/// <strong>TL/DR:</strong> the amount of time an expired cache entry is temporarily considered non-expired before checking the source (calling the factory) again.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public TimeSpan FailSafeThrottleDuration { get; set; }

	/// <summary>
	/// The maximum execution time allowed for the factory, applied only if fail-safe is enabled and there is a fallback value to return.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan FactorySoftTimeout { get; set; }

	/// <summary>
	/// The maximum execution time allowed for the factory in any case, even if there is not a stale value to fall back to.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan FactoryHardTimeout { get; set; }

	/// <summary>
	/// Enable a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public bool AllowTimedOutFactoryBackgroundCompletion { get; set; }

	/// <summary>
	/// The duration specific for the distributed cache, if any. If not set, <see cref="Duration"/> will be used.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public TimeSpan? DistributedCacheDuration { get; set; }

	/// <summary>
	/// When fail-safe is enabled this is the maximum amount of time a cache entry can be used in case of problems, even if expired, in the distributed cache.
	/// <br/><br/>
	/// Specifically:
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="true"/>, an entry will apparently expire normally after the specified Duration: behind the scenes though it will also be kept around for this (usually long) amount of time, so it may be used as a fallback value in case of problems;
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="false"/>, this is ignored;
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public TimeSpan? DistributedCacheFailSafeMaxDuration { get; set; }

	/// <summary>
	/// The maximum execution time allowed for each operation on the distributed cache, applied only if fail-safe is enabled and there is a fallback value to return.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan DistributedCacheSoftTimeout { get; set; }

	/// <summary>
	/// The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fall back to.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan DistributedCacheHardTimeout { get; set; }

	/// <summary>
	/// Even if the distributed cache is a secondary level, by default every operation on it (get/set/remove/etc) is blocking: that is to say the FusionCache method call would not return until the inner distributed cache operation is completed.
	/// <br/>
	/// This is to avoid rare edge cases like saving a value in the cache and immediately checking the underlying distributed cache directly, not finding the value (because it is still being saved): very very rare, but still.
	/// <br/>
	/// Setting this flag to <see langword="true"/> will execute most of these operations in the background, resulting in a performance boost.
	/// <br/><br/>
	/// <strong>TL/DR:</strong> set this flag to <see langword="true"/> for a perf boost, but watch out for rare side effects.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/BackgroundDistributedOperations.md"/>
	/// </summary>
	public bool AllowBackgroundDistributedCacheOperations { get; set; }

	/// <summary>
	///	Set this to <see langword="true"/> to allow the bubble up of distributed cache exceptions (default is <see langword="false"/>).
	///	Please note that, even if set to <see langword="true"/>, in some cases you would also need <see cref="AllowBackgroundDistributedCacheOperations"/> set to <see langword="false"/> and no timeout (neither soft nor hard) specified.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool ReThrowDistributedCacheExceptions { get; set; }

	/// <summary>
	///	Set this to <see langword="true"/> to allow the bubble up of serialization exceptions (default is <see langword="true"/>).
	///	Please note that, even if set to <see langword="true"/>, in some cases you would also need <see cref="AllowBackgroundDistributedCacheOperations"/> set to <see langword="false"/> and no timeout (neither soft nor hard) specified.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool ReThrowSerializationExceptions { get; set; }

	/// <summary>
	/// Enable publishing of backplane notifications after some operations, like a SET (via a Set/GetOrSet call) or a REMOVE (via a Remove call).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// <br/>
	/// <strong>OBSOLETE NOW:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/issues/101"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the SkipBackplaneNotifications option and invert the value: EnableBackplaneNotifications = true is the same as SkipBackplaneNotifications = false", true)]
	public bool EnableBackplaneNotifications
	{
		get { return !SkipBackplaneNotifications; }
		set { SkipBackplaneNotifications = !value; }
	}

	/// <summary>
	/// Skip the usage of the backplane, if any.
	/// <br/>
	/// Normally, if you have a backplane setup, any change operation (like a SET via a Set/GetOrSet call or a REMOVE via a Remove call) will send backplane notifications: this option can skip it.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public bool SkipBackplaneNotifications { get; set; }

	/// <summary>
	/// By default, every operation on the backplane is non-blocking: that is to say the FusionCache method call would not wait for each backplane operation to be completed.
	/// <br/>
	/// Setting this flag to <see langword="false"/> will execute these operations in a blocking fashion, typically resulting in worse performance.
	/// <br/><br/>
	/// <strong>TL/DR:</strong> if you want to wait for backplane operations to complete, set this flag to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/BackgroundDistributedOperations.md"/>
	/// </summary>
	public bool AllowBackgroundBackplaneOperations { get; set; }

	/// <summary>
	///	Set this to <see langword="true"/> to allow the bubble up of backplane exceptions (default is <see langword="false"/>).
	///	Please note that, even if set to <see langword="true"/>, in some cases you would also need <see cref="AllowBackgroundBackplaneOperations"/> set to <see langword="false"/> and no timeout (neither soft nor hard) specified.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public bool ReThrowBackplaneExceptions { get; set; }

	/// <summary>
	/// Skip the usage of the distributed cache, if any.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the specific SkipDistributedCacheRead and SkipDistributedCacheWrite options. To set them both at the same time, you can use SetSkipDistributedCache(skip).")]
	public bool SkipDistributedCache
	{
		get
		{
			return SkipDistributedCacheRead && SkipDistributedCacheWrite;
		}
		set
		{
			SkipDistributedCacheRead = SkipDistributedCacheWrite = value;
		}
	}

	/// <summary>
	/// Skip reading from the distributed cache, if any.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipDistributedCacheRead { get; set; }

	/// <summary>
	/// Skip writing to the distributed cache, if any.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipDistributedCacheWrite { get; set; }

	/// <summary>
	/// When a 2nd level (distributed cache) is used and a cache entry in the 1st level (memory cache) is found but is stale, a read is done on the distributed cache: the reason is that in a multi-node environment another node may have updated the cache entry, so we may found a newer version of it.
	/// <br/><br/>
	/// There are situations though, like in a mobile app with a SQLite 2nd level, where the 2nd level is not really "distributed" but just "out of process" (to ease cold starts): in situations like this no one can have updated the 2nd level, so we can skip that extra read for a perf boost (of course the write part will still be done).
	/// <br/><br/>
	/// <strong>TL/DR:</strong> if your 2nd level is not "distributed" but only "out of process", setting this to <see langword="true"/> can give you a nice performance boost.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipDistributedCacheReadWhenStale { get; set; }

	/// <summary>
	/// Skip the usage of the memory cache.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the specific SkipMemoryCacheRead and SkipMemoryCacheWrite options. To set them both at the same time, you can use SetSkipMemoryCache(skip).")]
	public bool SkipMemoryCache
	{
		get
		{
			return SkipMemoryCacheRead && SkipMemoryCacheWrite;
		}
		set
		{
			SkipMemoryCacheRead = SkipMemoryCacheWrite = value;
		}
	}

	/// <summary>
	/// Skip reading from the memory cache.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipMemoryCacheRead { get; set; }

	/// <summary>
	/// Skip writing to the memory cache.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipMemoryCacheWrite { get; set; }

	/// <summary>
	/// Enable automatic cloning of the value being returned from the cache, by using the provided <see cref="IFusionCacheSerializer"/>.
	/// <br></br>
	/// <strong>NOTE:</strong> if no <see cref="IFusionCacheSerializer"/> has been setup or if the value being returned cannot be (de)serialized, an exception will be thrown.
	/// </summary>
	public bool EnableAutoClone { get; set; }

	internal bool IsSafeForAdaptiveCaching { get; set; }

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[DUR={Duration.ToLogString()} LKTO={LockTimeout.ToLogString_Timeout()} SKMR={SkipMemoryCacheRead.ToStringYN()} SKMW={SkipMemoryCacheWrite.ToStringYN()} SKDR={SkipDistributedCacheRead.ToStringYN()} SKDW={SkipDistributedCacheWrite.ToStringYN()} SKDRWS={SkipDistributedCacheReadWhenStale.ToStringYN()} DDUR={DistributedCacheDuration.ToLogString()} JIT={JitterMaxDuration.ToLogString()} PR={Priority.ToLogString()} SZ={Size.ToLogString()} FS={IsFailSafeEnabled.ToStringYN()} FSMAX={FailSafeMaxDuration.ToLogString()} DFSMAX={DistributedCacheFailSafeMaxDuration.ToLogString()} FSTHR={FailSafeThrottleDuration.ToLogString()} FSTO={FactorySoftTimeout.ToLogString_Timeout()} FHTO={FactoryHardTimeout.ToLogString_Timeout()} TOFC={AllowTimedOutFactoryBackgroundCompletion.ToStringYN()} DSTO={DistributedCacheSoftTimeout.ToLogString_Timeout()} DHTO={DistributedCacheHardTimeout.ToLogString_Timeout()} ABDO={AllowBackgroundDistributedCacheOperations.ToStringYN()} SBN={SkipBackplaneNotifications.ToStringYN()} ABBO={AllowBackgroundBackplaneOperations.ToStringYN()} AC={EnableAutoClone.ToStringYN()}]";
	}

	/// <summary>
	/// If <see cref="JitterMaxDuration"/> is greater than <see cref="TimeSpan.Zero"/>, this method returns a randomized duration (in ms) between 0 and <see cref="JitterMaxDuration"/> that will be added to the entry's specified <see cref="Duration"/>.
	/// <br/>
	/// This is done to avoid a variation of the so called <see href="https://en.wikipedia.org/wiki/Cache_stampede"><strong>Cache Stampede problem</strong></see> that may happen when the entry for the same key expires on multiple nodes at the same time, because of high synchronization.
	/// </summary>
	/// <returns>An additional cache duration (in ms) to slightly vary the entry duration</returns>
	public double GetJitterDurationMs()
	{
		if (JitterMaxDuration <= TimeSpan.Zero)
			return 0d;

		return ConcurrentRandom.NextDouble() * JitterMaxDuration.TotalMilliseconds;
	}

	/// <summary>
	/// Set the jitter max duration.
	/// </summary>
	/// <param name="jitterMaxDuration">The jitter max duration.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetJittering(TimeSpan jitterMaxDuration)
	{
		JitterMaxDuration = jitterMaxDuration;
		return this;
	}

	internal FusionCacheEntryOptions SetIsSafeForAdaptiveCaching()
	{
		IsSafeForAdaptiveCaching = true;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to the specified <see cref="TimeSpan"/> value.
	/// </summary>
	/// <param name="duration">The duration to set.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDuration(TimeSpan duration)
	{
		Duration = duration;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to be zero: this will effectively remove the entry from the cache if fail-safe is disabled, or it will set the entry as logically expired if fail-safe is enabled (so it can be used later as a fallback).
	/// </summary>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationZero()
	{
		Duration = TimeSpan.Zero;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to be infinite, so it will never expire.
	/// <strong>NOTE:</strong> the expiration will not be literally "infinite", but it will be set to <see cref="DateTimeOffset.MaxValue"/> which in turn is Dec 31st 9999 which, I mean, c'mon. If that time will come and you'll have some problems feel free to try and contact me :-)
	/// </summary>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationInfinite()
	{
		Duration = TimeSpan.MaxValue;
		return this;
	}

	/// <summary>
	/// Set the <see cref="DistributedCacheDuration"/> to the specified <see cref="TimeSpan"/> value.
	/// </summary>
	/// <param name="duration">The duration to set.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheDuration(TimeSpan? duration)
	{
		DistributedCacheDuration = duration;
		return this;
	}

	/// <summary>
	/// Set the <see cref="DistributedCacheDuration"/> to be zero: this will effectively remove the entry from the cache if fail-safe is disabled, or it will set the entry as logically expired if fail-safe is enabled (so it can be used later as a fallback).
	/// </summary>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheDurationZero()
	{
		DistributedCacheDuration = TimeSpan.Zero;
		return this;
	}

	/// <summary>
	/// Set the <see cref="DistributedCacheDuration"/> to be infinite, so it will never expire.
	/// <strong>NOTE:</strong> the expiration will not be literally "infinite", but it will be set to <see cref="DateTimeOffset.MaxValue"/> which in turn is Dec 31st 9999 which, I mean, c'mon. If that time will come and you'll have some problems feel free to try and contact me :-)
	/// </summary>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheDurationInfinite()
	{
		DistributedCacheDuration = TimeSpan.MaxValue;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to the specified number of milliseconds.
	/// </summary>
	/// <param name="durationMs">The duration to set, in milliseconds.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationMs(int durationMs)
	{
		return SetDuration(TimeSpan.FromMilliseconds(durationMs));
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to the specified number of seconds.
	/// </summary>
	/// <param name="durationSec">The duration to set, in seconds.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationSec(int durationSec)
	{
		return SetDuration(TimeSpan.FromSeconds(durationSec));
	}

	/// <summary>
	/// Set the <see cref="Duration"/> to the specified number of minutes.
	/// </summary>
	/// <param name="durationMin">The duration to set, in minutes.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationMin(int durationMin)
	{
		return SetDuration(TimeSpan.FromMinutes(durationMin));
	}

	/// <summary>
	/// Set the <see cref="EagerRefreshThreshold"/>.
	/// </summary>
	/// <param name="threshold">The amount to set: values &lt;= 0.0 or &gt;= 1.0 will be normalized to <see langword="null"/>, meaning "no eager refresh".</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetEagerRefresh(float? threshold)
	{
		EagerRefreshThreshold = threshold;
		return this;
	}

	/// <summary>
	/// Set the size of the entry.
	/// </summary>
	/// <param name="size">The (unitless) size value to set.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSize(long? size)
	{
		Size = size;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Priority"/>.
	/// </summary>
	/// <param name="priority">The value for the <see cref="Priority"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetPriority(CacheItemPriority priority)
	{
		Priority = priority;
		return this;
	}

	/// <summary>
	/// Set various options related to the fail-safe mechanism.
	/// </summary>
	/// <param name="isEnabled">Enable or disable the fail-safe mechanism.</param>
	/// <param name="maxDuration">The value for the <see cref="FailSafeMaxDuration"/> option.</param>
	/// <param name="throttleDuration">The value for the <see cref="FailSafeThrottleDuration"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetFailSafe(bool isEnabled, TimeSpan? maxDuration = null, TimeSpan? throttleDuration = null)
	{
		IsFailSafeEnabled = isEnabled;
		if (maxDuration is not null)
			FailSafeMaxDuration = maxDuration.Value;
		if (throttleDuration is not null)
			FailSafeThrottleDuration = throttleDuration.Value;
		return this;
	}

	/// <summary>
	/// Set various options related to the fail-safe mechanism, related to the distributed cache.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this will not enable or disable the fail-safe mechanism, but only set some overrides.
	/// </summary>
	/// <param name="distributedCacheMaxDuration"></param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheFailSafeOptions(TimeSpan? distributedCacheMaxDuration)
	{
		DistributedCacheFailSafeMaxDuration = distributedCacheMaxDuration;
		return this;
	}

	/// <summary>
	/// Set various options related to the factory timeouts handling.
	/// </summary>
	/// <param name="softTimeout">The value for the <see cref="FactorySoftTimeout"/> option.</param>
	/// <param name="hardTimeout">The value for the <see cref="FactoryHardTimeout"/> option.</param>
	/// <param name="keepTimedOutFactoryResult">The value for the <see cref="AllowTimedOutFactoryBackgroundCompletion"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetFactoryTimeouts(TimeSpan? softTimeout = null, TimeSpan? hardTimeout = null, bool? keepTimedOutFactoryResult = null)
	{
		if (softTimeout is not null)
			FactorySoftTimeout = softTimeout.Value;
		if (hardTimeout is not null)
			FactoryHardTimeout = hardTimeout.Value;
		if (keepTimedOutFactoryResult is not null)
			AllowTimedOutFactoryBackgroundCompletion = keepTimedOutFactoryResult.Value;
		return this;
	}

	/// <summary>
	/// Set various options related to the factory timeouts handling.
	/// </summary>
	/// <param name="softTimeout">The value for the <see cref="DistributedCacheSoftTimeout"/> option.</param>
	/// <param name="hardTimeout">The value for the <see cref="DistributedCacheHardTimeout"/> option.</param>
	/// <param name="allowBackgroundDistributedCacheOperations">The value for the <see cref="AllowBackgroundDistributedCacheOperations"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheTimeouts(TimeSpan? softTimeout = null, TimeSpan? hardTimeout = null, bool? allowBackgroundDistributedCacheOperations = null)
	{
		if (softTimeout is not null)
			DistributedCacheSoftTimeout = softTimeout.Value;
		if (hardTimeout is not null)
			DistributedCacheHardTimeout = hardTimeout.Value;
		if (allowBackgroundDistributedCacheOperations is not null)
			AllowBackgroundDistributedCacheOperations = allowBackgroundDistributedCacheOperations.Value;
		return this;
	}

	/// <summary>
	/// Enable or disable backplane notifications.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// <br/>
	/// <strong>OBSOLETE NOW:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/issues/101"/>
	/// </summary>
	/// <param name="enableBackplaneNotifications">Set the <see cref="EnableBackplaneNotifications"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the SetSkipBackplaneNotifications method and invert the value: EnableBackplaneNotifications = true is the same as SkipBackplaneNotifications = false", true)]
	public FusionCacheEntryOptions SetBackplane(bool enableBackplaneNotifications)
	{
		return SetSkipBackplaneNotifications(!enableBackplaneNotifications);
	}

	/// <summary>
	/// Set the <see cref="SkipBackplaneNotifications"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipBackplaneNotifications"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipBackplaneNotifications(bool skip)
	{
		SkipBackplaneNotifications = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCacheRead"/> and <see cref="SkipDistributedCacheWrite"/> options.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipDistributedCacheRead"/> and <see cref="SkipDistributedCacheWrite"/> options.</param>
	/// <param name="skipBackplaneNotifications">The value for the <see cref="SkipBackplaneNotifications"/> option: if set to <see langword="null"/>, no changes will be made.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCache(bool skip, bool? skipBackplaneNotifications)
	{
		SkipDistributedCacheRead = skip;
		SkipDistributedCacheWrite = skip;
		if (skipBackplaneNotifications.HasValue)
			SkipBackplaneNotifications = skipBackplaneNotifications.Value;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCacheRead"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipDistributedCacheRead"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCacheRead(bool skip)
	{
		SkipDistributedCacheRead = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCacheWrite"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipDistributedCacheWrite"/> option.</param>
	/// <param name="skipBackplaneNotifications">The value for the <see cref="SkipBackplaneNotifications"/> option: if set to <see langword="null"/>, no changes will be made.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCacheWrite(bool skip, bool? skipBackplaneNotifications)
	{
		SkipDistributedCacheWrite = skip;
		if (skipBackplaneNotifications.HasValue)
			SkipBackplaneNotifications = skipBackplaneNotifications.Value;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCacheReadWhenStale"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">Set the <see cref="SkipDistributedCacheReadWhenStale"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCacheReadWhenStale(bool skip)
	{
		SkipDistributedCacheReadWhenStale = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipMemoryCacheRead"/> and <see cref="SkipMemoryCacheWrite"/> options.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipMemoryCacheRead"/> and <see cref="SkipMemoryCacheWrite"/> options.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipMemoryCache(bool skip = true)
	{
		SkipMemoryCacheRead = skip;
		SkipMemoryCacheWrite = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipMemoryCacheRead"/> option.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipMemoryCacheRead"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipMemoryCacheRead(bool skip = true)
	{
		SkipMemoryCacheRead = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipMemoryCacheWrite"/> option.
	/// <br/><br/>
	/// <strong>NOTE:</strong> this option must be used very carefully and is generally not recommended, as it will not protect you from some problems like Cache Stampede. Also, it can lead to a lot of extra work for the 2nd level (distributed cache) and a lot of extra network traffic.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipMemoryCacheWrite"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipMemoryCacheWrite(bool skip = true)
	{
		SkipMemoryCacheWrite = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="EnableAutoClone"/> option.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoClone.md"/>
	/// </summary>
	/// <param name="enable">The value for the <see cref="EnableAutoClone"/> option.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetAutoClone(bool enable = true)
	{
		EnableAutoClone = enable;
		return this;
	}

	internal DateTimeOffset GetAbsoluteExpiration(out bool incoherentFailSafeMaxDuration)
	{
		// PHYSICAL DURATION
		TimeSpan physicalDuration;

		if (IsFailSafeEnabled == false)
		{
			physicalDuration = Duration;
			incoherentFailSafeMaxDuration = false;
		}
		else
		{
			if (FailSafeMaxDuration < Duration)
			{
				incoherentFailSafeMaxDuration = true;
				physicalDuration = Duration;
			}
			else
			{
				incoherentFailSafeMaxDuration = false;
				physicalDuration = FailSafeMaxDuration;
			}
		}

		// ABSOLUTE EXPIRATION
		return FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(physicalDuration, this, true);
	}

	internal (MemoryCacheEntryOptions? memoryEntryOptions, DateTimeOffset? absoluteExpiration) ToMemoryCacheEntryOptionsOrAbsoluteExpiration(FusionCacheMemoryEventsHub events, FusionCacheOptions options, ILogger? logger, string operationId, string key, long? size)
	{
		size ??= Size;

		var absoluteExpiration = GetAbsoluteExpiration(out var incoherentFailSafeMaxDuration);

		// INCOHERENT DURATION
		if (incoherentFailSafeMaxDuration)
		{
			if (logger?.IsEnabled(options.IncoherentOptionsNormalizationLogLevel) ?? false)
				logger.Log(options.IncoherentOptionsNormalizationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): FailSafeMaxDuration {FailSafeMaxDuration} was lower than the Duration {Duration} on {Options}. Duration has been used instead.", options.CacheName, options.InstanceId, operationId, key, FailSafeMaxDuration.ToLogString(), Duration.ToLogString(), this.ToLogString());
		}

		if (size is null && Priority == CacheItemPriority.Normal && events.HasEvictionSubscribers() == false)
		{
			return (null, absoluteExpiration);
		}

		var res = new MemoryCacheEntryOptions
		{
			Size = size,
			Priority = Priority,
			AbsoluteExpiration = absoluteExpiration
		};

		// EVENTS
		if (events.HasEvictionSubscribers())
		{
			res.RegisterPostEvictionCallback(
				(key, entry, reason, state) =>
				{
					((FusionCacheMemoryEventsHub?)state)?.OnEviction(string.Empty, key.ToString() ?? "", reason, ((IFusionCacheMemoryEntry?)entry)?.Value);
				},
				events
			);
		}

		return (res, null);
	}

	internal DistributedCacheEntryOptions ToDistributedCacheEntryOptions(FusionCacheOptions options, ILogger? logger, string operationId, string key)
	{
		var res = new DistributedCacheEntryOptions();

		// PHYSICAL DURATION
		TimeSpan physicalDuration;
		TimeSpan durationToUse;

		durationToUse = DistributedCacheDuration ?? Duration;

		if (IsFailSafeEnabled == false)
		{
			// FAIL-SAFE DISABLED
			physicalDuration = durationToUse;
		}
		else
		{
			// FAIL-SAFE ENABLED
			var failSafeMaxDurationToUse = DistributedCacheFailSafeMaxDuration ?? FailSafeMaxDuration;
			if (failSafeMaxDurationToUse < durationToUse)
			{
				// INCOHERENT DURATION
				physicalDuration = durationToUse;

				if (logger?.IsEnabled(options.IncoherentOptionsNormalizationLogLevel) ?? false)
					logger.Log(options.IncoherentOptionsNormalizationLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): DistributedCacheFailSafeMaxDuration/FailSafeMaxDuration {FailSafeMaxDuration} was lower than the DistributedCacheDuration/Duration {Duration} on {Options} {MemoryOptions}. Duration has been used instead.", options.CacheName, options.InstanceId, operationId, key, failSafeMaxDurationToUse.ToLogString(), durationToUse.ToLogString(), this.ToLogString(), res.ToLogString());
			}
			else
			{
				physicalDuration = failSafeMaxDurationToUse;
			}
		}

		res.AbsoluteExpiration = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(physicalDuration, this, false);

		return res;
	}

	internal TimeSpan GetAppropriateMemoryLockTimeout(bool hasFallbackValue)
	{
		var res = LockTimeout;
		if (res == Timeout.InfiniteTimeSpan && hasFallbackValue && IsFailSafeEnabled && FactorySoftTimeout != Timeout.InfiniteTimeSpan)
		{
			// IF THERE IS NO SPECIFIC MEMORY LOCK TIMEOUT
			// + THERE IS A FALLBACK ENTRY
			// + FAIL-SAFE IS ENABLED
			// + THERE IS A FACTORY SOFT TIMEOUT
			// --> USE IT AS A MEMORY LOCK TIMEOUT
			res = FactorySoftTimeout;
		}

		return res;
	}

	internal TimeSpan GetAppropriateFactoryTimeout(bool hasFallbackValue)
	{
		// EARLY RETURN: WHEN NO TIMEOUTS AT ALL
		if (FactorySoftTimeout == Timeout.InfiniteTimeSpan && FactoryHardTimeout == Timeout.InfiniteTimeSpan)
			return Timeout.InfiniteTimeSpan;

		var res = Timeout.InfiniteTimeSpan;

		// 1ST: CHECK SOFT TIMEOUT, IF APPLICABLE
		if (FactorySoftTimeout > Timeout.InfiniteTimeSpan && IsFailSafeEnabled && hasFallbackValue)
		{
			res = FactorySoftTimeout;
		}

		// 2ND: CHECK HARD TIMEOUT, IF LOWER
		if (FactoryHardTimeout > Timeout.InfiniteTimeSpan && (res <= Timeout.InfiniteTimeSpan || res > FactoryHardTimeout))
			res = FactoryHardTimeout;

		return res;
	}

	internal TimeSpan GetAppropriateDistributedCacheTimeout(bool hasFallbackValue)
	{
		// EARLY RETURN: WHEN NO TIMEOUTS AT ALL
		if (DistributedCacheSoftTimeout == Timeout.InfiniteTimeSpan && DistributedCacheHardTimeout == Timeout.InfiniteTimeSpan)
			return Timeout.InfiniteTimeSpan;

		var res = Timeout.InfiniteTimeSpan;

		// 1ST: CHECK SOFT TIMEOUT, IF APPLICABLE
		if (DistributedCacheSoftTimeout > Timeout.InfiniteTimeSpan && IsFailSafeEnabled && hasFallbackValue)
		{
			res = DistributedCacheSoftTimeout;
		}

		// 2ND: CHECK HARD TIMEOUT, IF LOWER
		if (DistributedCacheHardTimeout > Timeout.InfiniteTimeSpan && (res <= Timeout.InfiniteTimeSpan || res > DistributedCacheHardTimeout))
			res = DistributedCacheHardTimeout;

		return res;
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheEntryOptions"/> object by duplicating all the options of the current one.
	/// </summary>
	/// <param name="duration">A custom <see cref="Duration"/> that, if specified, will overwrite the current one.</param>
	/// <returns>The newly created <see cref="FusionCacheEntryOptions"/> object.</returns>
	public FusionCacheEntryOptions Duplicate(TimeSpan? duration = null)
	{
		return new FusionCacheEntryOptions()
		{
			IsSafeForAdaptiveCaching = IsSafeForAdaptiveCaching,

			Duration = duration ?? Duration,
			LockTimeout = LockTimeout,
			Size = Size,
			Priority = Priority,
			JitterMaxDuration = JitterMaxDuration,

			// NOTE: PERF MICRO-OPT
			_eagerRefreshThreshold = _eagerRefreshThreshold,

			IsFailSafeEnabled = IsFailSafeEnabled,
			FailSafeMaxDuration = FailSafeMaxDuration,
			FailSafeThrottleDuration = FailSafeThrottleDuration,

			FactorySoftTimeout = FactorySoftTimeout,
			FactoryHardTimeout = FactoryHardTimeout,
			AllowTimedOutFactoryBackgroundCompletion = AllowTimedOutFactoryBackgroundCompletion,

			DistributedCacheDuration = DistributedCacheDuration,
			DistributedCacheFailSafeMaxDuration = DistributedCacheFailSafeMaxDuration,
			DistributedCacheSoftTimeout = DistributedCacheSoftTimeout,
			DistributedCacheHardTimeout = DistributedCacheHardTimeout,

			ReThrowDistributedCacheExceptions = ReThrowDistributedCacheExceptions,
			ReThrowSerializationExceptions = ReThrowSerializationExceptions,
			ReThrowBackplaneExceptions = ReThrowBackplaneExceptions,

			AllowBackgroundDistributedCacheOperations = AllowBackgroundDistributedCacheOperations,
			AllowBackgroundBackplaneOperations = AllowBackgroundBackplaneOperations,

			SkipBackplaneNotifications = SkipBackplaneNotifications,

			SkipDistributedCacheRead = SkipDistributedCacheRead,
			SkipDistributedCacheWrite = SkipDistributedCacheWrite,
			SkipDistributedCacheReadWhenStale = SkipDistributedCacheReadWhenStale,

			SkipMemoryCacheRead = SkipMemoryCacheRead,
			SkipMemoryCacheWrite = SkipMemoryCacheWrite,

			EnableAutoClone = EnableAutoClone
		};
	}

	internal FusionCacheEntryOptions EnsureIsSafeForAdaptiveCaching()
	{
		if (IsSafeForAdaptiveCaching)
			return this;

		return Duplicate().SetIsSafeForAdaptiveCaching();
	}
}
