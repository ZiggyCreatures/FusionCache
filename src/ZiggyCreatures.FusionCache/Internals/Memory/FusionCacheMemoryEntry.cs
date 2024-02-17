using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory;

/// <summary>
/// Represents a memory entry in <see cref="FusionCache"/>, which can be either a <see cref="FusionCacheMemoryEntry{TValue}"/> or a <see cref="FusionCacheDistributedEntry{TValue}"/>.
/// </summary>
internal sealed class FusionCacheMemoryEntry<TValue>
	: IFusionCacheMemoryEntry
{
	public FusionCacheMemoryEntry(object? value, FusionCacheEntryMetadata? metadata, long timestamp)
	{
		Value = value;
		Metadata = metadata;
		Timestamp = timestamp;
	}

	public object? Value { get; set; }

	public FusionCacheEntryMetadata? Metadata { get; private set; }

	public long Timestamp { get; private set; }

	public DateTimeOffset PhysicalExpiration { get; set; }

	public TValue1 GetValue<TValue1>()
	{
		return (TValue1)Value!;
	}

	public void SetValue<TValue1>(TValue1 value)
	{
		Value = value;
	}

	public override string ToString()
	{
		if (Metadata is null)
			return "[]";

		return Metadata.ToString();
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOptions(object? value, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag, long? timestamp)
	{
		if (options.IsFailSafeEnabled == false && options.EagerRefreshThreshold.HasValue == false)
		{
			return new FusionCacheMemoryEntry<TValue>(
				value,
				null,
				FusionCacheInternalUtils.GetCurrentTimestamp()
			);
		}

		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration, options, true);

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheMemoryEntry<TValue>(
			value,
			new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, etag, lastModified),
			timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp()
		);
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (options.IsFailSafeEnabled == false && entry.Metadata is null && options.EagerRefreshThreshold.HasValue == false)
		{
			return new FusionCacheMemoryEntry<TValue>(
				entry.GetValue<TValue>(),
				null,
				entry.Timestamp
			);
		}

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

		return new FusionCacheMemoryEntry<TValue>(
			entry.GetValue<TValue>(),
			new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, entry.Metadata?.ETag, entry.Metadata?.LastModified),
			entry.Timestamp
		);
	}

	public void UpdateFromDistributedEntry(FusionCacheDistributedEntry<TValue> distributedEntry)
	{
		Value = distributedEntry.Value;
		Timestamp = distributedEntry.Timestamp;
		Metadata = distributedEntry.Metadata;
	}

	public ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryAsync(string operationId, string cacheKey, FusionCache cache)
	{
		return cache.TryUpdateMemoryEntryFromDistributedEntryAsync<TValue>(operationId, cacheKey, this);
	}

	public ValueTask<bool> SetDistributedEntryAsync(string operationId, string key, DistributedCacheAccessor dca, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		return dca.SetEntryAsync<TValue>(operationId, key, this, options, isBackground, token);
	}
}
