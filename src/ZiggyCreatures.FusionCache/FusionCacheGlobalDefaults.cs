using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Contains the default values used globally.
/// <br/><br/>
/// <strong>NOTE:</strong> since these values are used *globally*, they should be changed only as a last resort, and if you *really* know what you are doing.
/// </summary>
public static class FusionCacheGlobalDefaults
{
	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.Duration"/>.
	/// </summary>
	public static TimeSpan EntryOptionsDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.LockTimeout"/>.
	/// </summary>
	public static TimeSpan EntryOptionsLockTimeout { get; set; } = Timeout.InfiniteTimeSpan;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.JitterMaxDuration"/>.
	/// </summary>
	public static TimeSpan EntryOptionsJitterMaxDuration { get; set; } = TimeSpan.Zero;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.Size"/>.
	/// </summary>
	public static long? EntryOptionsSize { get; set; } = null;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.Priority"/>.
	/// </summary>
	public static CacheItemPriority EntryOptionsPriority { get; set; } = CacheItemPriority.Normal;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.FactorySoftTimeout"/>.
	/// </summary>
	public static TimeSpan EntryOptionsFactorySoftTimeout { get; set; } = Timeout.InfiniteTimeSpan;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.FactoryHardTimeout"/>.
	/// </summary>
	public static TimeSpan EntryOptionsFactoryHardTimeout { get; set; } = Timeout.InfiniteTimeSpan;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.AllowTimedOutFactoryBackgroundCompletion"/>.
	/// </summary>
	public static bool EntryOptionsAllowTimedOutFactoryBackgroundCompletion { get; set; } = true;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.DistributedCacheDuration"/>.
	/// </summary>
	public static TimeSpan? EntryOptionsDistributedCacheDuration { get; set; } = null;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.DistributedCacheFailSafeMaxDuration"/>.
	/// </summary>
	public static TimeSpan? EntryOptionsDistributedCacheFailSafeMaxDuration { get; set; } = null;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.DistributedCacheSoftTimeout"/>.
	/// </summary>
	public static TimeSpan EntryOptionsDistributedCacheSoftTimeout { get; set; } = Timeout.InfiniteTimeSpan;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.DistributedCacheHardTimeout"/>.
	/// </summary>
	public static TimeSpan EntryOptionsDistributedCacheHardTimeout { get; set; } = Timeout.InfiniteTimeSpan;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.AllowBackgroundDistributedCacheOperations"/>.
	/// </summary>
	public static bool EntryOptionsAllowBackgroundDistributedCacheOperations { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.ReThrowDistributedCacheExceptions"/>.
	/// </summary>
	public static bool EntryOptionsReThrowDistributedCacheExceptions { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.ReThrowSerializationExceptions"/>.
	/// </summary>
	public static bool EntryOptionsReThrowSerializationExceptions { get; set; } = true;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.IsFailSafeEnabled"/>.
	/// </summary>
	public static bool EntryOptionsIsFailSafeEnabled { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.FailSafeMaxDuration"/>.
	/// </summary>
	public static TimeSpan EntryOptionsFailSafeMaxDuration { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.FailSafeThrottleDuration"/>.
	/// </summary>
	public static TimeSpan EntryOptionsFailSafeThrottleDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipBackplaneNotifications"/>.
	/// </summary>
	public static bool EntryOptionsSkipBackplaneNotifications { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.AllowBackgroundBackplaneOperations"/>.
	/// </summary>
	public static bool EntryOptionsAllowBackgroundBackplaneOperations { get; set; } = true;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.ReThrowBackplaneExceptions"/>.
	/// </summary>
	public static bool EntryOptionsReThrowBackplaneExceptions { get; set; } = false;

	/// <summary>
	/// The global default SkipDistributedCache, not used anymore.
	/// <br/><br/>
	/// NOTE: this is not used anymore, please use <see cref="EntryOptionsSkipDistributedCacheRead"/> and <see cref="EntryOptionsSkipDistributedCacheWrite"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the specific EntryOptionsSkipDistributedCacheRead and EntryOptionsSkipDistributedCacheWrite.", true)]
	public static bool EntryOptionsSkipDistributedCache { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipDistributedCacheRead"/>.
	/// </summary>
	public static bool EntryOptionsSkipDistributedCacheRead { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipDistributedCacheWrite"/>.
	/// </summary>
	public static bool EntryOptionsSkipDistributedCacheWrite { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipDistributedCacheReadWhenStale"/>.
	/// </summary>
	public static bool EntryOptionsSkipDistributedCacheReadWhenStale { get; set; } = false;

	/// <summary>
	/// The global default SkipMemoryCache, not used anymore.
	/// <br/><br/>
	/// NOTE: this is not used anymore, please use <see cref="EntryOptionsSkipMemoryCacheRead"/> and <see cref="EntryOptionsSkipMemoryCacheWrite"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the specific SkipMemoryCacheRead and SkipMemoryCacheWrite.", true)]
	public static bool EntryOptionsSkipMemoryCache { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipMemoryCacheRead"/>.
	/// </summary>
	public static bool EntryOptionsSkipMemoryCacheRead { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.SkipMemoryCacheWrite"/>.
	/// </summary>
	public static bool EntryOptionsSkipMemoryCacheWrite { get; set; } = false;

	/// <summary>
	/// The global default <see cref="FusionCacheEntryOptions.EnableAutoClone"/>.
	/// </summary>
	public static bool EntryOptionsEnableAutoClone { get; set; } = false;
}
