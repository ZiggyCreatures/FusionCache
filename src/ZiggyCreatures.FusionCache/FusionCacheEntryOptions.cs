using System;
using System.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents all the options available for a single <see cref="IFusionCache"/> entry.
/// </summary>
public class FusionCacheEntryOptions
{
	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheEntryOptions"/> object.
	/// </summary>
	/// <param name="duration">The value for the <see cref="Duration"/> property. If null, <see cref="FusionCacheGlobalDefaults.EntryOptionsDuration"/> will be used.</param>
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

		DistributedCacheDuration = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheDuration;
		DistributedCacheSoftTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheSoftTimeout;
		DistributedCacheHardTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheHardTimeout;
		AllowBackgroundDistributedCacheOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundDistributedCacheOperations;
		ReThrowDistributedCacheExceptions = FusionCacheGlobalDefaults.EntryOptionsReThrowDistributedCacheExceptions;
		ReThrowSerializationExceptions = FusionCacheGlobalDefaults.EntryOptionsReThrowSerializationExceptions;

		IsFailSafeEnabled = FusionCacheGlobalDefaults.EntryOptionsIsFailSafeEnabled;
		FailSafeMaxDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeMaxDuration;
		FailSafeThrottleDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeThrottleDuration;

		EnableBackplaneNotifications = FusionCacheGlobalDefaults.EntryOptionsEnableBackplaneNotifications;
		AllowBackgroundBackplaneOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundBackplaneOperations;

		SkipDistributedCache = FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCache;
		SkipDistributedCacheReadWhenStale = FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheReadWhenStale;
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
	/// - if <see cref="IsFailSafeEnabled" /> is set to <see langword="true"/>, the underlying duration in the cache corresponds to <see cref="FailSafeMaxDuration"/> and the <see cref="Duration"/> property is used internally as a way to indicate when the data should be considered stale (expired), without making it actually expire inside the cache levels (memory and/or distributed)
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	public TimeSpan Duration { get; set; }

	/// <summary>
	/// The timeout to apply when trying to acquire a lock during a factory execution.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md"/>
	/// </summary>
	public TimeSpan LockTimeout { get; set; }

	/// <summary>
	/// The maximum amount of extra duration to add to the normal <see cref="Duration"/> to allow for more variable expirations.
	/// </summary>
	public TimeSpan JitterMaxDuration { get; set; }

	/// <summary>
	/// The size of the cache entry, used as a value of <see cref="MemoryCacheEntryOptions.Size"/> in the underlying memory cache.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/StepByStep.md"/>
	/// </summary>
	public long Size { get; set; }

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
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="true"/>, an entry will apparently expire normally after the specified <see cref="Duration"/>: behind the scenes though it will also be kept around for this (usually long) amount of time, so it may be used as a fallback value in case of problems.
	/// <br/>
	/// - if <see cref="IsFailSafeEnabled"/> is set to <see langword="false"/>, this is ignored.
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
	/// The maximum execution time allowed for the factory in any case, even if there is not a stale value to fallback to.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan FactoryHardTimeout { get; set; }

	/// <summary>
	/// It enables a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value.
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
	/// The maximum execution time allowed for each operation on the distributed cache, applied only if fail-safe is enabled and there is a fallback value to return.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan DistributedCacheSoftTimeout { get; set; }

	/// <summary>
	/// The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fallback to.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md"/>
	/// </summary>
	public TimeSpan DistributedCacheHardTimeout { get; set; }

	/// <summary>
	/// Even if the distributed cache is a secondary layer, by default every operation on it (get/set/remove/etc) is blocking: that is to say the FusionCache method call would not return until the inner distributed cache operation is completed.
	/// <br/>
	/// This is to avoid rare edge cases like saving a value in the cache and immediately cheking the underlying distributed cache directly, not finding the value (because it is still being saved): very very rare, but still.
	/// <br/>
	/// Setting this flag to <see langword="true"/> will execute most of these operations in the background, resulting in a performance boost.
	/// <br/><br/>
	/// <strong>TL/DR:</strong> set this flag to <see langword="true"/> for a perf boost, but watch out for rare side effects.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
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
	///	Set this to <see langword="true"/> to allow the bubble up of serialization exceptions (default is <see langword="false"/>).
	///	Please note that, even if set to <see langword="true"/>, in some cases you would also need <see cref="AllowBackgroundDistributedCacheOperations"/> set to <see langword="false"/> and no timeout (neither soft nor hard) specified.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool ReThrowSerializationExceptions { get; set; }

	/// <summary>
	/// Enable publishing of backplane notifications after some operations, like a SET (via a Set/GetOrSet call) or a REMOVE (via a Remove call).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public bool EnableBackplaneNotifications { get; set; }

	/// <summary>
	/// By default every operation on the backplane is non-blocking: that is to say the FusionCache method call would not wait for each backplane operation to be completed.
	/// <br/>
	/// Setting this flag to <see langword="false"/> will execute these operations in a blocking fashion, typically resulting in worse performance.
	/// <br/><br/>
	/// <strong>TL/DR:</strong> if you want to wait for backplane operations to complete, set this flag to <see langword="false"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public bool AllowBackgroundBackplaneOperations { get; set; }

	/// <summary>
	/// Skip the usage of the distributed cache, if any.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipDistributedCache { get; set; }

	/// <summary>
	/// When a 2nd layer (distributed cache) is used and a cache entry in the 1st layer (memory cache) is found but is stale, a read is done on the distributed cache: the reason is that in a multi-node environment another node may have updated the cache entry, so we may found a newer version of it.
	/// <br/><br/>
	/// There are situations though, like in a mobile app with a SQLite 2nd layer, where the 2nd layer is not really "distributed" but just "out of process" (to ease cold starts): in situations like this noone can have updated the 2nd layer, so we can skip that extra read for a perf boost (of course the write part will still be done).
	/// <br/><br/>
	/// <strong>TL/DR:</strong> if your 2nd level is not "distributed" but only "out of process", setting this to <see langword="true"/> can give you a nice performance boost.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public bool SkipDistributedCacheReadWhenStale { get; set; }

	internal bool IsSafeForAdaptiveCaching { get; set; }

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[LKTO={LockTimeout.ToLogString_Timeout()} DUR={Duration.ToLogString()} SKD={SkipDistributedCache.ToStringYN()} DDUR={DistributedCacheDuration.ToLogString()} JIT={JitterMaxDuration.ToLogString()} PR={Priority.ToLogString()} FS={IsFailSafeEnabled.ToStringYN()} FSMAX={FailSafeMaxDuration.ToLogString()} FSTHR={FailSafeThrottleDuration.ToLogString()} FSTO={FactorySoftTimeout.ToLogString_Timeout()} FHTO={FactoryHardTimeout.ToLogString_Timeout()} TOFC={AllowTimedOutFactoryBackgroundCompletion.ToStringYN()} DSTO={DistributedCacheSoftTimeout.ToLogString_Timeout()} DHTO={DistributedCacheHardTimeout.ToLogString_Timeout()} ABDO={AllowBackgroundDistributedCacheOperations.ToStringYN()} BN={EnableBackplaneNotifications.ToStringYN()} BBO={AllowBackgroundBackplaneOperations.ToStringYN()}]";
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

	internal FusionCacheEntryOptions SetIsSafeForAdaptiveCaching()
	{
		IsSafeForAdaptiveCaching = true;
		return this;
	}

	/// <summary>
	/// Set the duration to the specified <see cref="TimeSpan"/> value.
	/// </summary>
	/// <param name="duration">The duration to set.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDuration(TimeSpan duration)
	{
		Duration = duration;
		return this;
	}

	/// <summary>
	/// Set the distributed cache duration to the specified <see cref="TimeSpan"/> value.
	/// </summary>
	/// <param name="duration">The duration to set.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDistributedCacheDuration(TimeSpan? duration)
	{
		DistributedCacheDuration = duration;
		return this;
	}

	/// <summary>
	/// Set the duration to the specified number of milliseconds.
	/// </summary>
	/// <param name="durationMs">The duration to set, in milliseconds.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationMs(int durationMs)
	{
		return SetDuration(TimeSpan.FromMilliseconds(durationMs));
	}

	/// <summary>
	/// Set the duration to the specified number of seconds.
	/// </summary>
	/// <param name="durationSec">The duration to set, in seconds.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationSec(int durationSec)
	{
		return SetDuration(TimeSpan.FromSeconds(durationSec));
	}

	/// <summary>
	/// Set the duration to the specified number of minutes.
	/// </summary>
	/// <param name="durationMin">The duration to set, in minutes.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetDurationMin(int durationMin)
	{
		return SetDuration(TimeSpan.FromMinutes(durationMin));
	}

	/// <summary>
	/// Set the 
	/// </summary>
	/// <param name="size">The (unitless) size value to set.</param>
	/// <returns></returns>
	public FusionCacheEntryOptions SetSize(long size)
	{
		Size = size;
		return this;
	}

	/// <summary>
	/// Set the <see cref="Priority"/>.
	/// </summary>
	/// <param name="priority">The value for the <see cref="Priority"/> property.</param>
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
	/// <param name="maxDuration">The value for the <see cref="FailSafeMaxDuration"/> property.</param>
	/// <param name="throttleDuration">The value for the <see cref="FailSafeThrottleDuration"/> property.</param>
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
	/// Set various options related to the factory timeouts handling.
	/// </summary>
	/// <param name="softTimeout">The value for the <see cref="FactorySoftTimeout"/> property.</param>
	/// <param name="hardTimeout">The value for the <see cref="FactoryHardTimeout"/> property.</param>
	/// <param name="keepTimedOutFactoryResult">The value for the <see cref="AllowTimedOutFactoryBackgroundCompletion"/> property.</param>
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
	/// <param name="softTimeout">The value for the <see cref="DistributedCacheSoftTimeout"/> property.</param>
	/// <param name="hardTimeout">The value for the <see cref="DistributedCacheHardTimeout"/> property.</param>
	/// <param name="allowBackgroundDistributedCacheOperations">The value for the <see cref="AllowBackgroundDistributedCacheOperations"/> property.</param>
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
	/// </summary>
	/// <param name="enableBackplaneNotifications">Set the <see cref="EnableBackplaneNotifications"/> property.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetBackplane(bool enableBackplaneNotifications)
	{
		EnableBackplaneNotifications = enableBackplaneNotifications;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCache"/> property.
	/// </summary>
	/// <param name="skip">The value for the <see cref="SkipDistributedCache"/> property.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCache(bool skip)
	{
		SkipDistributedCache = skip;
		return this;
	}

	/// <summary>
	/// Set the <see cref="SkipDistributedCacheReadWhenStale"/> property.
	/// </summary>
	/// <param name="skip">Set the <see cref="SkipDistributedCacheReadWhenStale"/> property.</param>
	/// <returns>The <see cref="FusionCacheEntryOptions"/> so that additional calls can be chained.</returns>
	public FusionCacheEntryOptions SetSkipDistributedCacheReadWhenStale(bool skip)
	{
		SkipDistributedCacheReadWhenStale = skip;
		return this;
	}

	/// <summary>
	/// Creates a new <see cref="MemoryCacheEntryOptions"/> instance based on this <see cref="FusionCacheEntryOptions"/> instance.
	/// </summary>
	/// <returns>The newly created <see cref="MemoryCacheEntryOptions"/> instance.</returns>
	public MemoryCacheEntryOptions ToMemoryCacheEntryOptions(FusionCacheMemoryEventsHub events)
	{
		var res = new MemoryCacheEntryOptions
		{
			Size = Size,
			Priority = Priority
		};

		if (JitterMaxDuration <= TimeSpan.Zero)
		{
			res.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(IsFailSafeEnabled ? FailSafeMaxDuration : Duration);
		}
		else
		{
			res.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(IsFailSafeEnabled ? FailSafeMaxDuration : Duration).AddMilliseconds(GetJitterDurationMs());
		}

		if (events.HasEvictionSubscribers())
		{
			res.RegisterPostEvictionCallback(
				(key, _, reason, state) => ((FusionCacheMemoryEventsHub)state)?.OnEviction(string.Empty, key.ToString(), reason),
				events
			);
		}

		return res;
	}

	/// <summary>
	/// Creates a new <see cref="DistributedCacheEntryOptions"/> instance based on this <see cref="FusionCacheEntryOptions"/> instance.
	/// </summary>
	/// <returns>The newly created <see cref="DistributedCacheEntryOptions"/> instance.</returns>
	public DistributedCacheEntryOptions ToDistributedCacheEntryOptions()
	{
		var res = new DistributedCacheEntryOptions();

		res.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(IsFailSafeEnabled ? FailSafeMaxDuration : DistributedCacheDuration.GetValueOrDefault(Duration));

		return res;
	}

	internal TimeSpan GetAppropriateFactoryTimeout(bool hasFallbackValue)
	{
		// SHORT CIRCUIT WHEN NO TIMEOUTS AT ALL
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
		// SHORT CIRCUIT WHEN NO TIMEOUTS AT ALL
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

			IsFailSafeEnabled = IsFailSafeEnabled,
			FailSafeMaxDuration = FailSafeMaxDuration,
			FailSafeThrottleDuration = FailSafeThrottleDuration,

			FactorySoftTimeout = FactorySoftTimeout,
			FactoryHardTimeout = FactoryHardTimeout,
			AllowTimedOutFactoryBackgroundCompletion = AllowTimedOutFactoryBackgroundCompletion,

			DistributedCacheDuration = DistributedCacheDuration,
			DistributedCacheSoftTimeout = DistributedCacheSoftTimeout,
			DistributedCacheHardTimeout = DistributedCacheHardTimeout,

			ReThrowDistributedCacheExceptions = ReThrowDistributedCacheExceptions,
			ReThrowSerializationExceptions = ReThrowSerializationExceptions,

			AllowBackgroundDistributedCacheOperations = AllowBackgroundDistributedCacheOperations,
			AllowBackgroundBackplaneOperations = AllowBackgroundBackplaneOperations,

			EnableBackplaneNotifications = EnableBackplaneNotifications,

			SkipDistributedCache = SkipDistributedCache,
			SkipDistributedCacheReadWhenStale = SkipDistributedCacheReadWhenStale
		};
	}

	internal FusionCacheEntryOptions EnsureIsSafeForAdaptiveCaching()
	{
		if (IsSafeForAdaptiveCaching)
			return this;

		return Duplicate().SetIsSafeForAdaptiveCaching();
	}
}
