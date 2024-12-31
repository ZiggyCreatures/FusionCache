using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory;

/// <summary>
/// Represents a memory entry in <see cref="FusionCache"/>, which can be either a <see cref="FusionCacheMemoryEntry{TValue}"/> or a <see cref="FusionCacheDistributedEntry{TValue}"/>.
/// </summary>
internal sealed class FusionCacheMemoryEntry<TValue>
	: IFusionCacheMemoryEntry
{
	public FusionCacheMemoryEntry(object? value, string[]? tags, FusionCacheEntryMetadata? metadata, long timestamp, long logicalExpirationTimestamp)
	{
		Value = value;
		Tags = tags;
		Metadata = metadata;
		Timestamp = timestamp;
		LogicalExpirationTimestamp = logicalExpirationTimestamp;
	}

	private byte[]? _serializedValue;

	private object? _value;
	public object? Value
	{
		get
		{
			return _value;
		}
		set
		{
			_value = value;
			lock (this)
			{
				_serializedValue = null;
			}
		}
	}

	public string[]? Tags { get; set; }

	public FusionCacheEntryMetadata? Metadata { get; private set; }

	public long Timestamp { get; private set; }

	public long LogicalExpirationTimestamp { get; set; }

	// TODO: STILL USEFUL ?
	public DateTimeOffset PhysicalExpiration { get; set; }

	public byte[] GetSerializedValue(IFusionCacheSerializer serializer)
	{
		byte[]? serializedValue = _serializedValue;
		if (serializedValue is not null)
			return serializedValue;

		lock (this)
		{
			if (_serializedValue is not null)
				return _serializedValue;

			_serializedValue = serializer.Serialize(GetValue<TValue>());
			return _serializedValue;
		}
	}

	public TValue1 GetValue<TValue1>()
	{
		return (TValue1)Value!;
	}

	public void SetValue<TValue1>(TValue1 value)
	{
		Value = value;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return FusionCacheInternalUtils.ToLogString(this, false) ?? "";
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOptions(object? value, string[]? tags, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag, long? timestamp)
	{
		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpirationTimestamp(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration, options, true);

		FusionCacheEntryMetadata? metadata = null;
		if (FusionCacheInternalUtils.RequiresMetadata(options, isFromFailSafe, lastModified, etag))
		{
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, new DateTimeOffset(exp, TimeSpan.Zero));

			metadata = new FusionCacheEntryMetadata(isFromFailSafe, eagerExp, etag, lastModified, options.Size);
		}

		return new FusionCacheMemoryEntry<TValue>(
			value,
			tags,
			metadata,
			timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp(),
			exp
		);
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		FusionCacheEntryMetadata? metadata = null;
		if (FusionCacheInternalUtils.RequiresMetadata(options, entry.Metadata))
		{
			var isStale = entry.IsStale();
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isStale, options.EagerRefreshThreshold, new DateTimeOffset(entry.LogicalExpirationTimestamp, TimeSpan.Zero));

			metadata = new FusionCacheEntryMetadata(
				isStale,
				eagerExp,
				entry.Metadata?.ETag,
				entry.Metadata?.LastModified,
				entry.Metadata?.Size /*?? options.Size*/
			);
		}

		return new FusionCacheMemoryEntry<TValue>(
			entry.GetValue<TValue>(),
			entry.Tags,
			metadata,
			entry.Timestamp,
			entry.LogicalExpirationTimestamp
		);
	}

	public void UpdateFromDistributedEntry(FusionCacheDistributedEntry<TValue> distributedEntry)
	{
		Value = distributedEntry.Value;
		Tags = distributedEntry.Tags;
		Timestamp = distributedEntry.Timestamp;
		LogicalExpirationTimestamp = distributedEntry.LogicalExpirationTimestamp;
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
