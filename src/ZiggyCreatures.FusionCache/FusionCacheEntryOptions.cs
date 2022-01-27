using System;
using System.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion
{
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

			DistributedCacheSoftTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheSoftTimeout;
			DistributedCacheHardTimeout = FusionCacheGlobalDefaults.EntryOptionsDistributedCacheHardTimeout;
			AllowBackgroundDistributedCacheOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundDistributedCacheOperations;

			IsFailSafeEnabled = FusionCacheGlobalDefaults.EntryOptionsIsFailSafeEnabled;
			FailSafeMaxDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeMaxDuration;
			FailSafeThrottleDuration = FusionCacheGlobalDefaults.EntryOptionsFailSafeThrottleDuration;

			EnableBackplaneNotifications = FusionCacheGlobalDefaults.EntryOptionsEnableBackplaneNotifications;
			AllowBackgroundBackplaneOperations = FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundBackplaneOperations;
		}

		/// <summary>
		/// The amount of time after which a cache entry is considered expired.
		/// </summary>
		public TimeSpan Duration { get; set; }

		/// <summary>
		/// The timeout to apply when trying to acquire a lock during a factory execution.
		/// </summary>
		public TimeSpan LockTimeout { get; set; }

		/// <summary>
		/// The maximum amount of extra duration to add to the normal <see cref="Duration"/> to allow for more variable expirations.
		/// </summary>
		public TimeSpan JitterMaxDuration { get; set; }

		/// <summary>
		/// The size of the cache entry (typically used as a value of <see cref="MemoryCacheEntryOptions.Size"/>).
		/// </summary>
		public long Size { get; set; }

		/// <summary>
		/// The <see cref="CacheItemPriority"/> of the entry in the memory cache.
		/// </summary>
		public CacheItemPriority Priority { get; set; }

		// DYNAMIC
		//public Action<FusionCacheEntryOptions, object?>? Modifier { get; set; }

		/// <summary>
		/// A function to apply when creating a <see cref="MemoryCacheEntryOptions"/> object from this <see cref="FusionCacheEntryOptions"/> object, to allow for extra customizations.
		/// </summary>
		[Obsolete("Please stop using this, it was an undocumented work in progress")]
		public Action<MemoryCacheEntryOptions, object?>? MemoryOptionsModifier { get; set; }

		/// <summary>
		/// A function to apply when creating a <see cref="DistributedCacheEntryOptions"/> object from this <see cref="FusionCacheEntryOptions"/> object, to allow for extra customizations.
		/// </summary>
		[Obsolete("Please stop using this, it was an undocumented work in progress")]
		public Action<DistributedCacheEntryOptions, object?>? DistributedOptionsModifier { get; set; }

		/// <summary>
		/// Enable the fail-safe mechanism, which will be activated if and when something goes wrong while calling a factory or getting data from a distributed cache.
		/// </summary>
		public bool IsFailSafeEnabled { get; set; }

		/// <summary>
		/// When fail-safe is enabled an entry will expire normally nonetheless, but it will also be kept around for this (usually long) duration, so it may be used as a fallback value in case of errors.
		/// <br/><br/>
		/// TL/DR: the maximum amount of time an expired cache entry can still be used in case of problems.
		/// </summary>
		public TimeSpan FailSafeMaxDuration { get; set; }

		/// <summary>
		/// If fail-safe is enabled, something goes wrong while getting data (from the distributed cache or while calling the factory) and there is an expired entry to be used as a fallback value, the fail-safe mechanism will actually be activated.
		/// In that case the fallback value will not only be returned to the caller but also put in the cache for this duration (usually small) to avoid excessive load on the distributed cache and/or the factory getting called continuously.
		/// <br/><br/>
		/// TL/DR: the amount of time an expired cache entry is temporarily considered non-expired before checking the source (calling the factory) again.
		/// </summary>
		public TimeSpan FailSafeThrottleDuration { get; set; }

		/// <summary>
		/// The maximum execution time allowed for the factory, applied only if fail-safe is enabled and there is a fallback value to return.
		/// </summary>
		public TimeSpan FactorySoftTimeout { get; set; }

		/// <summary>
		/// The maximum execution time allowed for the factory in any case, even if there is not a stale value to fallback to.
		/// </summary>
		public TimeSpan FactoryHardTimeout { get; set; }

		/// <summary>
		/// It enables a factory that has hit a synthetic timeout (both soft/hard) to complete in the background and update the cache with the new value.
		/// </summary>
		public bool AllowTimedOutFactoryBackgroundCompletion { get; set; }

		/// <summary>
		/// The maximum execution time allowed for each operation on the distributed cache, applied only if fail-safe is enabled and there is a fallback value to return.
		/// </summary>
		public TimeSpan DistributedCacheSoftTimeout { get; set; }

		/// <summary>
		/// The maximum execution time allowed for each operation on the distributed cache in any case, even if there is not a stale value to fallback to.
		/// </summary>
		public TimeSpan DistributedCacheHardTimeout { get; set; }

		/// <summary>
		/// Even if the distributed cache is a secondary layer, by default every operation on it (get/set/remove/etc) is blocking: that is to say the FusionCache method call would not return until the inner distributed cache operation is completed.
		/// <br/>
		/// This is to avoid rare edge cases like saving a value in the cache and immediately cheking the underlying distributed cache directly, not finding the value (because it is still being saved): very very rare, but still.
		/// <br/>
		/// Setting this flag to true will execute most of these operations in the background, resulting in a performance boost.
		/// <para>TL/DR: set this flag to true for a perf boost, but watch out for rare side effects.</para>
		/// </summary>
		public bool AllowBackgroundDistributedCacheOperations { get; set; }

		/// <summary>
		/// Sends notifications on the backplane to other nodes after some operations on cache entries, like a SET (via a Set/GetOrSet call) or a REMOVE (via a Remove call).
		/// </summary>
		public bool EnableBackplaneNotifications { get; set; }

		/// <summary>
		/// By default every operation on the backplane is non-blocking: that is to say the FusionCache method call would not wait for each backplane operation to be completed.
		/// <br/>
		/// Setting this flag to false will execute these operations in a blocking fashion, typically resulting in worse performance.
		/// <para>TL/DR: if you want to wait for backplane operations to complete, set this flag to false.</para>
		/// </summary>
		public bool AllowBackgroundBackplaneOperations { get; set; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[LKTO={LockTimeout.ToLogString_Timeout()} DUR={Duration.ToLogString()} JIT={JitterMaxDuration.ToLogString()} PR={Priority.ToLogString()} FS={(IsFailSafeEnabled ? "Y" : "N")} FSMAX={FailSafeMaxDuration.ToLogString()} FSTHR={FailSafeThrottleDuration.ToLogString()} FSTO={FactorySoftTimeout.ToLogString_Timeout()} FHTO={FactoryHardTimeout.ToLogString_Timeout()} TOFC={(AllowTimedOutFactoryBackgroundCompletion ? "Y" : "N")} DSTO={DistributedCacheSoftTimeout.ToLogString_Timeout()} DHTO={DistributedCacheHardTimeout.ToLogString_Timeout()} ABDO={(AllowBackgroundDistributedCacheOperations ? "Y" : "N")}]";
		}

		/// <summary>
		/// If <see cref="JitterMaxDuration"/> is greater than <see cref="TimeSpan.Zero"/>, this method returns a randomized duration (in ms) between 0 and <see cref="JitterMaxDuration"/> that will be added to the entry's specified <see cref="Duration"/> .
		/// This is done to avoid a variation of the so called <a href="https://en.wikipedia.org/wiki/Cache_stampede">cache stampede problem</a> that may happen when the entry for the same key expires on multiple nodes at the same time, because of high synchronization.
		/// </summary>
		/// <returns>An additional cache duration (in ms) to slightly vary the entry duration</returns>
		public double GetJitterDurationMs()
		{
			if (JitterMaxDuration <= TimeSpan.Zero)
				return 0d;

			return ConcurrentRandom.NextDouble() * JitterMaxDuration.TotalMilliseconds;
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
			if (maxDuration is object)
				FailSafeMaxDuration = maxDuration.Value;
			if (throttleDuration is object)
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
			if (softTimeout is object)
				FactorySoftTimeout = softTimeout.Value;
			if (hardTimeout is object)
				FactoryHardTimeout = hardTimeout.Value;
			if (keepTimedOutFactoryResult is object)
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
			if (softTimeout is object)
				DistributedCacheSoftTimeout = softTimeout.Value;
			if (hardTimeout is object)
				DistributedCacheHardTimeout = hardTimeout.Value;
			if (allowBackgroundDistributedCacheOperations is object)
				AllowBackgroundDistributedCacheOperations = allowBackgroundDistributedCacheOperations.Value;
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

			res.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(IsFailSafeEnabled ? FailSafeMaxDuration : Duration);

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

				DistributedCacheSoftTimeout = DistributedCacheSoftTimeout,
				DistributedCacheHardTimeout = DistributedCacheHardTimeout,
				AllowBackgroundDistributedCacheOperations = AllowBackgroundDistributedCacheOperations,

				EnableBackplaneNotifications = EnableBackplaneNotifications,
				AllowBackgroundBackplaneOperations = AllowBackgroundBackplaneOperations
			};
		}

		/// <summary>
		/// Creates a new <see cref="FusionCacheEntryOptions"/> object by duplicating all the options of the current one.
		/// </summary>
		/// <param name="duration">A custom <see cref="Duration"/> that, if specified, will overwrite the current one.</param>
		/// <param name="includeOptionsModifiers">If false, the <see cref="MemoryOptionsModifier"/> and <see cref="DistributedOptionsModifier"/> will not be duplicated.</param>
		/// <returns>The newly created <see cref="FusionCacheEntryOptions"/> object.</returns>
		[Obsolete("Please stop using this, it was an undocumented work in progress")]
		public FusionCacheEntryOptions Duplicate(TimeSpan? duration, bool includeOptionsModifiers)
		{
			return Duplicate(duration);
		}
	}
}
