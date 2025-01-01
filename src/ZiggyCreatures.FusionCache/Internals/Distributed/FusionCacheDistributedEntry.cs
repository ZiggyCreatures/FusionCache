using System;
using System.Runtime.Serialization;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

/// <summary>
/// Represents a distributed entry in <see cref="FusionCache"/>.
/// </summary>
/// <typeparam name="TValue">The type of the entry's value</typeparam>
[DataContract]
public sealed class FusionCacheDistributedEntry<TValue>
	: IFusionCacheEntry
{
	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="value">The actual value.</param>
	/// <param name="timestamp">The original timestamp of the entry, see <see cref="Timestamp"/>.</param>
	/// <param name="logicalExpirationTimestamp">The logical expiration of the entry</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="metadata">The metadata for the entry.</param>
	public FusionCacheDistributedEntry(TValue value, long timestamp, long logicalExpirationTimestamp, string[]? tags, FusionCacheEntryMetadata? metadata)
	{
		Value = value;
		Timestamp = timestamp;
		LogicalExpirationTimestamp = logicalExpirationTimestamp;
		Tags = tags;
		Metadata = metadata;
	}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	/// <summary>
	/// Creates a new instance.
	/// </summary>
	public FusionCacheDistributedEntry()
	{
#pragma warning disable CS8601 // Possible null reference assignment.
		Value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
	}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	/// <inheritdoc/>
	[DataMember(Name = "v", EmitDefaultValue = false)]
	public TValue Value { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "t", EmitDefaultValue = false)]
	public long Timestamp { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "l", EmitDefaultValue = false)]
	public long LogicalExpirationTimestamp { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "x", EmitDefaultValue = false)]
	public string[]? Tags { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "m", EmitDefaultValue = false)]
	public FusionCacheEntryMetadata? Metadata { get; set; }

	/// <inheritdoc/>
	public TValue1 GetValue<TValue1>()
	{
		return (TValue1)(object)Value!;
	}

	/// <inheritdoc/>
	public void SetValue<TValue1>(TValue1 value)
	{
		Value = (TValue)(object)value!;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return FusionCacheInternalUtils.ToLogString(this, false) ?? "";
	}

	internal static FusionCacheDistributedEntry<TValue> CreateFromOptions(TValue value, long timestamp, string[]? tags, FusionCacheEntryOptions options, bool isStale, DateTimeOffset? lastModified, string? etag)
	{
		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpirationTimestamp(isStale ? options.FailSafeThrottleDuration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration), options, false);

		FusionCacheEntryMetadata? metadata = null;
		if (FusionCacheInternalUtils.RequiresMetadata(options, isStale, lastModified, etag))
		{
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpirationTimestamp(isStale, options.EagerRefreshThreshold, exp);

			metadata = new FusionCacheEntryMetadata(isStale, eagerExp, etag, lastModified, options.Size);
		}

		return new FusionCacheDistributedEntry<TValue>(
			value,
			timestamp,
			exp
,
			tags,
			metadata);
	}

	internal static FusionCacheDistributedEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		FusionCacheEntryMetadata? metadata = null;
		if (FusionCacheInternalUtils.RequiresMetadata(options, entry.Metadata))
		{
			var isStale = entry.IsStale();
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpirationTimestamp(isStale, options.EagerRefreshThreshold, entry.LogicalExpirationTimestamp);

			metadata = new FusionCacheEntryMetadata(
				isStale,
				eagerExp,
				entry.Metadata?.ETag,
				entry.Metadata?.LastModified,
				entry.Metadata?.Size /*?? options.Size*/
			);
		}

		return new FusionCacheDistributedEntry<TValue>(
			entry.GetValue<TValue>(),
			entry.Timestamp,
			entry.LogicalExpirationTimestamp
,
			entry.Tags,
			metadata);
	}
}
