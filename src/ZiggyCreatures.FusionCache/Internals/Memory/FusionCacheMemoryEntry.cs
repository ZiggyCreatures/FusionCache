using System;
using System.Runtime.CompilerServices;
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
	public FusionCacheMemoryEntry(object? value, string[]? tags, FusionCacheEntryMetadata? metadata, long timestamp)
	{
		Value = value;
		Tags = tags;
		Metadata = metadata;
		Timestamp = timestamp;
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

	public override string ToString()
	{
		// TODO: SHOULD WE ALSO INCLUDE TAGS?

		if (Metadata is null)
			return "[]";

		return Metadata.ToString();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool RequiresMetadata(FusionCacheEntryOptions options, FusionCacheEntryMetadata? meta)
	{
		return
			options.IsFailSafeEnabled
			|| options.EagerRefreshThreshold.HasValue
			|| options.Size is not null
			|| meta is not null
		;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool RequiresMetadata(FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag)
	{
		return
			options.IsFailSafeEnabled
			|| options.EagerRefreshThreshold.HasValue
			|| options.Size is not null
			|| isFromFailSafe
			|| lastModified is not null
			|| etag is not null
		;
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOptions(object? value, string[]? tags, FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag, long? timestamp)
	{
		if (RequiresMetadata(options, isFromFailSafe, lastModified, etag) == false)
		{
			return new FusionCacheMemoryEntry<TValue>(
				value,
				tags,
				null,
				timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp()
			);
		}

		var exp = FusionCacheInternalUtils.GetNormalizedAbsoluteExpiration(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration, options, true);

		var eagerExp = FusionCacheInternalUtils.GetNormalizedEagerExpiration(isFromFailSafe, options.EagerRefreshThreshold, exp);

		return new FusionCacheMemoryEntry<TValue>(
			value,
			tags,
			new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, etag, lastModified, options.Size),
			timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp()
		);
	}

	public static FusionCacheMemoryEntry<TValue> CreateFromOtherEntry(IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (RequiresMetadata(options, entry.Metadata) == false)
		{
			return new FusionCacheMemoryEntry<TValue>(
				entry.GetValue<TValue>(),
				entry.Tags,
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
			entry.Tags,
			new FusionCacheEntryMetadata(exp, isFromFailSafe, eagerExp, entry.Metadata?.ETag, entry.Metadata?.LastModified, entry.Metadata?.Size ?? options.Size),
			entry.Timestamp
		);
	}

	public void UpdateFromDistributedEntry(FusionCacheDistributedEntry<TValue> distributedEntry)
	{
		Value = distributedEntry.Value;
		Tags = distributedEntry.Tags;
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
