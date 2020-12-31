using Microsoft.Extensions.Caching.Memory;
using System;
using ZiggyCreatures.FusionCaching.Internals;

namespace ZiggyCreatures.FusionCaching
{

	/// <summary>
	/// An entry in a <see cref="FusionCache"/> .
	/// </summary>
	/// <typeparam name="TValue">The type of the entry's value</typeparam>
	public class FusionCacheEntry<TValue>
	{

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="value">The actual value.</param>
		/// <param name="logicalExpiration">THe logical expiration of the cache entry: this is used in when the actual expiration in the cache is higher because of fail-safe.</param>
		/// <param name="priority">The <see cref="CacheItemPriority"/> of the entry (mainly used in the memory cache).</param>
		/// <param name="isFromFailSafe">Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.</param>
		public FusionCacheEntry(TValue value, DateTimeOffset logicalExpiration, CacheItemPriority priority, bool isFromFailSafe)
		{
			Value = value;
			LogicalExpiration = logicalExpiration;
			Priority = priority;
			IsFromFailSafe = isFromFailSafe;
		}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		/// <summary>
		/// Creates a new instance.
		/// </summary>
		protected FusionCacheEntry()
		{
#pragma warning disable CS8601 // Possible null reference assignment.
			Value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
		}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary>
		/// The value to be cached.
		/// </summary>
		public TValue Value { get; set; }

		/// <summary>
		/// The intended expiration of the entry as requested from the caller
		/// <br/>
		/// When fail-safe is enabled the entry is cached with a higher duration (<see cref="FusionCacheEntryOptions.FailSafeMaxDuration"/>) so it may be used as a fallback value in case of problems: when that happens, the LogicalExpiration is used to check if the value is stale, instead of losing it by simply let it expire in the cache.
		/// </summary>
		public DateTimeOffset LogicalExpiration { get; }

		/// <summary>
		/// The <see cref="CacheItemPriority"/> of the entry (mainly used in the memory cache).
		/// </summary>
		public CacheItemPriority Priority { get; }

		/// <summary>
		/// Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.
		/// </summary>
		public bool IsFromFailSafe { get; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[FFS={(IsFromFailSafe ? "Y" : "N")} LEXP={LogicalExpiration.ToLogString_Expiration()} PR={Priority.ToLogString()}]";
		}

		/// <summary>
		/// Checks if the entry is logically expired.
		/// </summary>
		/// <returns>A <see cref="bool"/> indicating the logical expiration status.</returns>
		public bool IsLogicallyExpired()
		{
			return LogicalExpiration < DateTimeOffset.UtcNow;
		}

		/// <summary>
		/// Creates a new <see cref="FusionCacheEntry{TValue}"/> instance from a value and some options.
		/// </summary>
		/// <param name="value">The value to be cached.</param>
		/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
		/// <param name="isFromFailSafe">Indicates if the value comes from a fail-safe activation.</param>
		/// <returns>The newly created entry.</returns>
		public static FusionCacheEntry<TValue> CreateFromOptions(TValue value, FusionCacheEntryOptions options, bool isFromFailSafe)
		{
			DateTimeOffset exp;

			if (isFromFailSafe)
			{
				exp = DateTimeOffset.UtcNow.AddMilliseconds(options.GetJitterDurationMs()) + options.FailSafeThrottleDuration;
			}
			else
			{
				exp = DateTimeOffset.UtcNow.AddMilliseconds(options.GetJitterDurationMs()) + options.Duration;
			}

			return new FusionCacheEntry<TValue>(
				value,
				exp,
				options.Priority,
				isFromFailSafe
			);
		}

	}

}