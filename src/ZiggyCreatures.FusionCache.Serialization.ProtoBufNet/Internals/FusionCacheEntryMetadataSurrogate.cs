using System;
using ProtoBuf;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals;

[ProtoContract]
internal class FusionCacheEntryMetadataSurrogate
{
	[ProtoMember(1)]
	public long LogicalExpirationUtcTicks { get; set; }

	[ProtoMember(2)]
	public bool IsFromFailSafe { get; set; }

	[ProtoMember(3)]
	public long? LastModifiedUtcTicks { get; set; }

	[ProtoMember(4)]
	public string? ETag { get; set; }

	[ProtoMember(5)]
	public long? EagerExpirationUtcTicks { get; set; }

	public static implicit operator FusionCacheEntryMetadataSurrogate?(FusionCacheEntryMetadata value)
	{
		if (value is null)
			return null;

		return new FusionCacheEntryMetadataSurrogate
		{
			LogicalExpirationUtcTicks = value.LogicalExpiration.UtcTicks,
			IsFromFailSafe = value.IsFromFailSafe,
			EagerExpirationUtcTicks = value.EagerExpiration?.UtcTicks,
			ETag = value.ETag,
			LastModifiedUtcTicks = value.LastModified?.UtcTicks
		};
	}

	public static implicit operator FusionCacheEntryMetadata?(FusionCacheEntryMetadataSurrogate value)
	{
		if (value is null)
			return null;

		return new FusionCacheEntryMetadata(
			new DateTimeOffset(value.LogicalExpirationUtcTicks, TimeSpan.Zero),
			value.IsFromFailSafe,
			value.EagerExpirationUtcTicks.HasValue ? new DateTimeOffset(value.EagerExpirationUtcTicks.Value, TimeSpan.Zero) : null,
			value.ETag,
			value.LastModifiedUtcTicks.HasValue ? new DateTimeOffset(value.LastModifiedUtcTicks.Value, TimeSpan.Zero) : null
		);
	}
}
