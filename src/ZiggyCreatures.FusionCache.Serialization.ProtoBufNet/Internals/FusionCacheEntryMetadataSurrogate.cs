using ProtoBuf;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals;

[ProtoContract]
internal class FusionCacheEntryMetadataSurrogate
{
	[ProtoMember(1)]
	public bool IsStale { get; set; }

	[ProtoMember(2)]
	public long? LastModifiedTimestamp { get; set; }

	[ProtoMember(3)]
	public string? ETag { get; set; }

	[ProtoMember(4)]
	public long? EagerExpirationTimestamp { get; set; }

	[ProtoMember(5)]
	public long? Size { get; set; }

	[ProtoMember(6)]
	public byte? Priority { get; set; }

	public static implicit operator FusionCacheEntryMetadataSurrogate?(FusionCacheEntryMetadata value)
	{
		if (value is null)
			return null;

		return new FusionCacheEntryMetadataSurrogate
		{
			IsStale = value.IsStale,
			EagerExpirationTimestamp = value.EagerExpirationTimestamp,
			ETag = value.ETag,
			LastModifiedTimestamp = value.LastModifiedTimestamp,
			Size = value.Size,
			Priority = value.Priority
		};
	}

	public static implicit operator FusionCacheEntryMetadata?(FusionCacheEntryMetadataSurrogate value)
	{
		if (value is null)
			return null;

		return new FusionCacheEntryMetadata(
			value.IsStale,
			value.EagerExpirationTimestamp,
			value.ETag,
			value.LastModifiedTimestamp,
			value.Size,
			value.Priority
		);
	}
}
