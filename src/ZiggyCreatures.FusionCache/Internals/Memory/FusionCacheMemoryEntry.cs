using System;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory;

/// <summary>
/// An entry in a <see cref="FusionCache"/> memory layer.
/// </summary>
internal sealed class FusionCacheMemoryEntry
	: IFusionCacheEntry
{
	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="value">The actual value.</param>
	/// <param name="metadata">The metadata for the entry.</param>
	public FusionCacheMemoryEntry(object? value, FusionCacheEntryMetadata? metadata)
	{
		Value = value;
		Metadata = metadata;
	}

	/// <inheritdoc/>
	public object? Value { get; set; }

	/// <inheritdoc/>
	public FusionCacheEntryMetadata? Metadata { get; }

	/// <inheritdoc/>
	public TValue GetValue<TValue>()
	{
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
		return (TValue)Value;
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
	}

	/// <inheritdoc/>
	public void SetValue<TValue>(TValue value)
	{
		Value = value;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		if (Metadata is null)
			return "[]";

		return Metadata.ToString();
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheMemoryEntry"/> instance from a value and some options.
	/// </summary>
	/// <param name="value">The value to be cached.</param>
	/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
	/// <param name="isFromFailSafe">Indicates if the value comes from a fail-safe activation.</param>
	/// <param name="lastModified">If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="etag">If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <returns>The newly created entry.</returns>
	public static FusionCacheMemoryEntry CreateFromOptions(object? value, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag)
	{
		if (options.IsFailSafeEnabled == false && options.EagerRefreshThreshold.HasValue == false)
			return new FusionCacheMemoryEntry(value, null);

		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration, options, true);

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheMemoryEntry(value, new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, etag, lastModified));
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheMemoryEntry"/> instance from another entry and some options.
	/// </summary>
	/// <param name="entry">The source entry.</param>
	/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
	/// <returns>The newly created entry.</returns>
	public static FusionCacheMemoryEntry CreateFromOtherEntry<TValue>(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (options.IsFailSafeEnabled == false && entry.Metadata is null && options.EagerRefreshThreshold.HasValue == false)
			return new FusionCacheMemoryEntry(entry.GetValue<TValue>(), null);

		var isFromFailSafe = entry.Metadata?.IsFromFailSafe ?? false;

		DateTimeOffset exp;

		if (entry.Metadata is not null)
		{
			exp = entry.Metadata.LogicalExpiration;
		}
		else
		{
			exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration, options, true);
		}

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheMemoryEntry(entry.GetValue<TValue>(), new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, entry.Metadata?.ETag, entry.Metadata?.LastModified));
	}
}
