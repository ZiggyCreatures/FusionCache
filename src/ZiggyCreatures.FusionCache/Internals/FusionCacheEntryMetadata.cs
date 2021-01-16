using System;
using System.Runtime.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals
{

	/// <summary>
	/// Metadata for an entry in a <see cref="FusionCache"/> .
	/// </summary>
	[DataContract]
	public class FusionCacheEntryMetadata
	{

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="logicalExpiration">THe logical expiration of the cache entry: this is used in when the actual expiration in the cache is higher because of fail-safe.</param>
		/// <param name="isFromFailSafe">Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.</param>
		public FusionCacheEntryMetadata(DateTimeOffset logicalExpiration, bool isFromFailSafe)
		{
			LogicalExpiration = logicalExpiration;
			IsFromFailSafe = isFromFailSafe;
		}

		/// <summary>
		/// The intended expiration of the entry as requested from the caller
		/// <br/>
		/// When fail-safe is enabled the entry is cached with a higher duration (<see cref="FusionCacheEntryOptions.FailSafeMaxDuration"/>) so it may be used as a fallback value in case of problems: when that happens, the LogicalExpiration is used to check if the value is stale, instead of losing it by simply let it expire in the cache.
		/// </summary>
		[DataMember(Name = "e", EmitDefaultValue = false)]
		public DateTimeOffset LogicalExpiration { get; }

		/// <summary>
		/// Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.
		/// </summary>
		[DataMember(Name = "f", EmitDefaultValue = false)]
		public bool IsFromFailSafe { get; }

		/// <summary>
		/// Checks if the entry is logically expired.
		/// </summary>
		/// <returns>A <see cref="bool"/> indicating the logical expiration status.</returns>
		public bool IsLogicallyExpired()
		{
			return LogicalExpiration < DateTimeOffset.UtcNow;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[FFS={(IsFromFailSafe ? "Y" : "N")} LEXP={LogicalExpiration.ToLogString_Expiration()}]";
		}

	}

}