using System;
using System.Runtime.Serialization;

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
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="metadata">The metadata for the entry.</param>
	/// <param name="timestamp">The original timestamp of the entry, see <see cref="Timestamp"/>.</param>
	public FusionCacheDistributedEntry(TValue value, string[]? tags, FusionCacheEntryMetadata? metadata, long timestamp)
	{
		Value = value;
		Tags = tags;
		Metadata = metadata;
		Timestamp = timestamp;
	}

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="value">The actual value.</param>
	/// <param name="metadata">The metadata for the entry.</param>
	/// <param name="timestamp">The original timestamp of the entry, see <see cref="Timestamp"/>.</param>
	public FusionCacheDistributedEntry(TValue value, FusionCacheEntryMetadata? metadata, long timestamp)
		: this(value, FusionCacheInternalUtils.NoTags, metadata, timestamp)
	{
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
	[DataMember(Name = "m", EmitDefaultValue = false)]
	public FusionCacheEntryMetadata? Metadata { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "t", EmitDefaultValue = false)]
	public long Timestamp { get; set; }

	/// <inheritdoc/>
	[DataMember(Name = "x", EmitDefaultValue = false)]
	public string[]? Tags { get; set; }

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

	internal static FusionCacheDistributedEntry<TValue> CreateFromOptions(TValue value, string[]? tags, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag, long timestamp)
	{
		// TODO: CHECK THIS AGAIN
		//if (FusionCacheInternalUtils.RequiresMetadata(options, isFromFailSafe, lastModified, etag) == false)
		//{
		//	return new FusionCacheDistributedEntry<TValue>(
		//		value,
		//		tags,
		//		null,
		//		timestamp
		//	);
		//}

		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration), options, false);

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheDistributedEntry<TValue>(
			value,
			tags,
			new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, etag, lastModified, options.Size),
			timestamp
		);
	}

	internal static FusionCacheDistributedEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		// TODO: CHECK THIS AGAIN
		//if (FusionCacheInternalUtils.RequiresMetadata(options, entry.Metadata) == false)
		//{
		//	return new FusionCacheDistributedEntry<TValue>(
		//		entry.GetValue<TValue>(),
		//		entry.Tags,
		//		null,
		//		entry.Timestamp
		//	);
		//}

		//if (options.IsFailSafeEnabled == false && entry.Metadata is null && options.EagerRefreshThreshold.HasValue == false)
		//	return new FusionCacheDistributedEntry<TValue>(entry.GetValue<TValue>(), null);

		var isStale = entry.IsStale();

		DateTimeOffset exp;

		if (entry.Metadata is not null)
		{
			exp = entry.Metadata.LogicalExpiration;
		}
		else
		{
			exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isStale ? options.FailSafeThrottleDuration : options.Duration, options, true);
		}

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isStale, options.EagerRefreshThreshold, exp);

		return new FusionCacheDistributedEntry<TValue>(
			entry.GetValue<TValue>(),
			entry.Tags,
			new FusionCacheEntryMetadata(exp, isStale, eagerExp, entry.Metadata?.ETag, entry.Metadata?.LastModified, entry.Metadata?.Size ?? options.Size),
			entry.Timestamp
		);
	}
}
