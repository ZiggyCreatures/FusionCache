using System;
using System.Runtime.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

/// <summary>
/// An entry in a <see cref="FusionCache"/> distributed layer.
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
	/// <param name="metadata">The metadata for the entry</param>
	public FusionCacheDistributedEntry(TValue value, FusionCacheEntryMetadata? metadata)
	{
		Value = value;
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

	/// <summary>
	/// The value inside the entry.
	/// </summary>
	[DataMember(Name = "v", EmitDefaultValue = false)]
	public TValue Value { get; set; }

	/// <summary>
	/// Metadata about the cache entry.
	/// </summary>
	[DataMember(Name = "m", EmitDefaultValue = false)]
	public FusionCacheEntryMetadata? Metadata { get; set; }

	/// <inheritdoc/>
	public TValue1 GetValue<TValue1>()
	{
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
		return (TValue1)(object)Value;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8603 // Possible null reference return.
	}

	/// <inheritdoc/>
	public void SetValue<TValue1>(TValue1 value)
	{
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
		Value = (TValue)(object)value;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8601 // Possible null reference assignment.
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		if (Metadata is null)
			return "[]";

		return Metadata.ToString();
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheDistributedEntry{TValue}"/> instance from a value and some options.
	/// </summary>
	/// <param name="value">The value to be cached.</param>
	/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
	/// <param name="isFromFailSafe">Indicates if the value comes from a fail-safe activation.</param>
	/// <param name="lastModified">If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="etag">If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <returns>The newly created entry.</returns>
	public static FusionCacheDistributedEntry<TValue> CreateFromOptions(TValue value, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag)
	{
		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.DistributedCacheDuration.GetValueOrDefault(options.Duration), options, false);

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheDistributedEntry<TValue>(value, new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, etag, lastModified));
	}
}
