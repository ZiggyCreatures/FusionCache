using System;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// Contains the default values used globally.
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
		public static long EntryOptionsSize { get; set; } = 1;

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
		public static bool EntryOptionsReThrowSerializationExceptions { get; set; } = false;

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
		/// The global default <see cref="FusionCacheEntryOptions.EnableBackplaneNotifications"/>.
		/// </summary>
		public static bool EntryOptionsEnableBackplaneNotifications { get; set; } = true;

		/// <summary>
		/// The global default <see cref="FusionCacheEntryOptions.AllowBackgroundBackplaneOperations"/>.
		/// </summary>
		public static bool EntryOptionsAllowBackgroundBackplaneOperations { get; set; } = true;
	}
}
