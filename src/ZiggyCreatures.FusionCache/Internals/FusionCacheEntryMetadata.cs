using System;
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
	/// <param name="isStale">Indicates if the cache entry comes from a fail-safe activation, so if the value was used as a fallback because errors occurred.</param>
	/// <param name="eagerExpirationTimestamp">The eager expiration, based on the <see cref="FusionCacheEntryOptions.EagerRefreshThreshold"/>.</param>
	/// <param name="etag">If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="lastModifiedTimestamp">If provided, it's the last modified date of the entry, expressed as a timestamp (UtcTicks): this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="size">The Size of the cache entry.</param>
	public FusionCacheEntryMetadata(bool isStale, long? eagerExpirationTimestamp, string? etag, long? lastModifiedTimestamp, long? size)
	{
		IsStale = isStale;
		EagerExpirationTimestamp = eagerExpirationTimestamp;
		ETag = etag;
		LastModifiedTimestamp = lastModifiedTimestamp;
		Size = size;
	}

	// USED BY SOME SERIALIZERS
	private FusionCacheEntryMetadata()
	{
		// EMPTY
	}

	/// <summary>
	/// Indicates if the cache entry i stale, typically because of a fail-safe activation during a refresh (factory execution).
	/// </summary>
	[DataMember(Name = "s", EmitDefaultValue = false)]
	public bool IsStale { get; set; }

	/// <summary>
	/// The eager expiration, based on the <see cref="FusionCacheEntryOptions.EagerRefreshThreshold"/>.
	/// </summary>
	[DataMember(Name = "e", EmitDefaultValue = false)]
	public long? EagerExpirationTimestamp { get; set; }

	/// <summary>
	/// If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	[DataMember(Name = "t", EmitDefaultValue = false)]
	public string? ETag { get; set; }

	/// <summary>
	/// If provided, it's the Last-Modified date of the entry, expressed as a timestamp (UtcTicks): this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	[DataMember(Name = "m", EmitDefaultValue = false)]
	public long? LastModifiedTimestamp { get; set; }

	/// <summary>
	/// The size of the entry.
	/// </summary>
	[DataMember(Name = "z", EmitDefaultValue = false)]
	public long? Size { get; set; }

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[S={IsStale.ToStringYN()}, EEXP={(EagerExpirationTimestamp is null ? "/" : new DateTimeOffset(EagerExpirationTimestamp.Value, TimeSpan.Zero).ToLogString())}, LM={(LastModifiedTimestamp is null ? "/" : new DateTimeOffset(LastModifiedTimestamp.Value, TimeSpan.Zero).ToLogString())}, ET={(string.IsNullOrWhiteSpace(ETag) ? "/" : ETag)}, S={(Size.HasValue ? Size.Value.ToString() : "/")}]";
	}
}
