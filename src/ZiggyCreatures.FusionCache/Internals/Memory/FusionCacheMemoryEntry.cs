using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory;

/// <summary>
/// Represents a memory entry in <see cref="FusionCache"/>, which can be either a <see cref="FusionCacheMemoryEntry{TValue}"/> or a <see cref="FusionCacheDistributedEntry{TValue}"/>.
/// </summary>
internal sealed class FusionCacheMemoryEntry<TValue>
	: IFusionCacheMemoryEntry
{
	private FusionCacheMemoryEntry(object? value, byte[]? serializedValue, long timestamp, long logicalExpirationTimestamp, string[]? tags, FusionCacheEntryMetadata? metadata, TimeSpan? memoryCacheDuration)
	{
		SetValue(value, serializedValue);
		Timestamp = timestamp;
		LogicalExpirationTimestamp = logicalExpirationTimestamp;
		Tags = tags;
		Metadata = metadata;
		MemoryCacheDuration = memoryCacheDuration;
	}

	private byte[]? _serializedValue;

	private object? _value;
	public object? Value
	{
		get
		{
			return _value;
		}
	}

	public long Timestamp { get; private set; }

	public long LogicalExpirationTimestamp { get; set; }

	public string[]? Tags { get; set; }

	public FusionCacheEntryMetadata? Metadata { get; private set; }

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

	public TimeSpan? MemoryCacheDuration { get; private set; }

	public TValue1 GetValue<TValue1>()
	{
		return (TValue1)Value!;
	}

	public void SetValue<TValue1>(TValue1 value)
	{
		SetValue(value, null);
	}

	public void SetValue<TValue1>(TValue1 value, byte[]? serializedValue)
	{
		_value = value;
		_serializedValue = serializedValue;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return FusionCacheInternalUtils.ToLogString(this, false) ?? "";
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOptions(object? value, byte[]? serializedValue, long? timestamp, string[]? tags, FusionCacheEntryOptions options, bool isStale, long? lastModifiedTimestamp, string? etag)
	{
		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpirationTimestamp(isStale ? options.FailSafeThrottleDuration : options.MemoryCacheDuration.GetValueOrDefault(options.Duration), options, isStale == false);

		FusionCacheEntryMetadata? metadata = null;
		if (FusionCacheInternalUtils.RequiresMetadata(options, isStale, lastModifiedTimestamp, etag))
		{
			var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpirationTimestamp(isStale, options.EagerRefreshThreshold, exp);

			metadata = new FusionCacheEntryMetadata(isStale, eagerExp, etag, lastModifiedTimestamp, options.Size, (byte)options.Priority);
		}

		return new FusionCacheMemoryEntry<TValue>(
			value,
			serializedValue,
			timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp(),
			exp,
			tags,
			metadata,
			options.MemoryCacheDuration
		);
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
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
				entry.Metadata?.LastModifiedTimestamp,
				entry.Metadata?.Size,
				entry.Metadata?.Priority
			);
		}

		var logicalExpirationTimestamp = entry.LogicalExpirationTimestamp;
		if (options.MemoryCacheDuration is not null)
		{
			var correctTimestamp = FusionCacheInternalUtils.GetCurrentTimestamp() + options.MemoryCacheDuration.Value.Ticks;

			if (entry.LogicalExpirationTimestamp > correctTimestamp)
			{
				//Console.WriteLine($"!!! FUCK !!! (DIFFERENCE: {entry.LogicalExpirationTimestamp - correctTimestamp} ticks - {TimeSpan.FromTicks(entry.LogicalExpirationTimestamp - correctTimestamp)} sec)");
				logicalExpirationTimestamp = correctTimestamp;
			}
		}

		return new FusionCacheMemoryEntry<TValue>(
			entry.GetValue<TValue>(),
			null,
			entry.Timestamp,
			logicalExpirationTimestamp,
			entry.Tags,
			metadata,
			options.MemoryCacheDuration
		);
	}

	public void UpdateFromDistributedEntry(FusionCacheDistributedEntry<TValue> distributedEntry)
	{
		var logicalExpirationTimestamp = distributedEntry.LogicalExpirationTimestamp;
		if (MemoryCacheDuration is not null)
		{
			var correctTimestamp = FusionCacheInternalUtils.GetCurrentTimestamp() + MemoryCacheDuration.Value.Ticks;

			if (distributedEntry.LogicalExpirationTimestamp > correctTimestamp)
			{
				logicalExpirationTimestamp = correctTimestamp;
			}
		}

		SetValue(distributedEntry.Value);
		Tags = distributedEntry.Tags;
		Timestamp = distributedEntry.Timestamp;
		LogicalExpirationTimestamp = logicalExpirationTimestamp;
		Metadata = distributedEntry.Metadata;
	}

	public (bool error, bool isSame, bool hasUpdated) TryUpdateMemoryEntryFromDistributedEntry(string operationId, string cacheKey, FusionCache cache)
	{
		return cache.TryUpdateMemoryEntryFromDistributedEntry<TValue>(operationId, cacheKey, this);
	}

	public ValueTask<(bool error, bool isSame, bool hasUpdated)> TryUpdateMemoryEntryFromDistributedEntryAsync(string operationId, string cacheKey, FusionCache cache)
	{
		return cache.TryUpdateMemoryEntryFromDistributedEntryAsync<TValue>(operationId, cacheKey, this);
	}

	public ValueTask<bool> SetDistributedEntryAsync(string operationId, string key, IDistributedCacheAccessor dca, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		return dca.SetEntryAsync<TValue>(operationId, key, this, options, isBackground, token);
	}
}
