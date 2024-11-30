using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// Metadata for an entry in a <see cref="FusionCache"/> .
/// </summary>
[DataContract]
public sealed class FusionCacheEntryMetadata
{
	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="logicalExpiration">The logical expiration of the cache entry: this is used in when the actual expiration in the cache is higher because of fail-safe.</param>
	/// <param name="isFromFailSafe">Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.</param>
	/// <param name="eagerExpiration">The eager expiration, based on the <see cref="FusionCacheEntryOptions.EagerRefreshThreshold"/>.</param>
	/// <param name="etag">If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="lastModified">If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="size">Ifprovided, it's the Size of the cache entry.</param>
	public FusionCacheEntryMetadata(DateTimeOffset logicalExpiration, bool isFromFailSafe, DateTimeOffset? eagerExpiration, string? etag, DateTimeOffset? lastModified, long? size)
	{
		LogicalExpiration = logicalExpiration;
		IsFromFailSafe = isFromFailSafe;
		EagerExpiration = eagerExpiration;
		ETag = etag;
		LastModified = lastModified;
		Size = size;
	}

	// USED BY SOME SERIALIZERS
	private FusionCacheEntryMetadata()
	{
		// EMPTY
	}

	/// <summary>
	/// The intended expiration of the entry as requested from the caller.
	/// <br/>
	/// When fail-safe is enabled the entry is cached with a higher duration (<see cref="FusionCacheEntryOptions.FailSafeMaxDuration"/>) so it may be used as a fallback value in case of problems: when that happens, the LogicalExpiration is used to check if the value is stale, instead of losing it by simply let it expire in the cache.
	/// </summary>
	[DataMember(Name = "l", EmitDefaultValue = false)]
	public DateTimeOffset LogicalExpiration { get; set; }

	/// <summary>
	/// Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.
	/// </summary>
	[DataMember(Name = "f", EmitDefaultValue = false)]
	public bool IsFromFailSafe { get; set; }

	/// <summary>
	/// The eager expiration, based on the <see cref="FusionCacheEntryOptions.EagerRefreshThreshold"/>.
	/// </summary>
	[DataMember(Name = "e", EmitDefaultValue = false)]
	public DateTimeOffset? EagerExpiration { get; set; }

	/// <summary>
	/// If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	[DataMember(Name = "t", EmitDefaultValue = false)]
	public string? ETag { get; set; }

	/// <summary>
	/// If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	[DataMember(Name = "m", EmitDefaultValue = false)]
	public DateTimeOffset? LastModified { get; set; }

	/// <summary>
	/// If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	[DataMember(Name = "s", EmitDefaultValue = false)]
	public long? Size { get; set; }

	/// <summary>
	/// Checks if the entry is logically expired.
	/// </summary>
	/// <returns>A <see cref="bool"/> indicating the logical expiration status.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool IsLogicallyExpired()
	{
		return LogicalExpiration < DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Checks if an eager refresh should happen.
	/// </summary>
	/// <returns>A <see cref="bool"/> indicating an eager refresh should happen.</returns>
	public bool ShouldEagerlyRefresh()
	{
		if (EagerExpiration.HasValue == false)
			return false;

		if (EagerExpiration.Value >= DateTimeOffset.UtcNow)
			return false;

		if (IsLogicallyExpired())
			return false;

		return true;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[FFS={IsFromFailSafe.ToStringYN()}, LEXP={LogicalExpiration.ToLogString_Expiration()}, EEXP={EagerExpiration.ToLogString_Expiration()}, LM={LastModified.ToLogString()}, ET={(string.IsNullOrEmpty(ETag) ? "/" : ETag)}, S={(Size.HasValue ? Size.Value.ToString() : "/")}]";
	}
}
