using System;
using ProtoBuf;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals
{
	[ProtoContract]
	internal class FusionCacheEntryMetadataSurrogate
	{
		[ProtoMember(1)]
		public long LogicalExpirationUtcTicks { get; set; }

		[ProtoMember(2)]
		public bool IsFromFailSafe { get; set; }

		public static implicit operator FusionCacheEntryMetadataSurrogate?(FusionCacheEntryMetadata value)
		{
			if (value is null)
				return null;

			return new FusionCacheEntryMetadataSurrogate
			{
				LogicalExpirationUtcTicks = value.LogicalExpiration.UtcTicks,
				IsFromFailSafe = value.IsFromFailSafe
			};
		}

		public static implicit operator FusionCacheEntryMetadata?(FusionCacheEntryMetadataSurrogate value)
		{
			if (value is null)
				return null;

			return new FusionCacheEntryMetadata(
				new DateTimeOffset(value.LogicalExpirationUtcTicks, TimeSpan.Zero),
				value.IsFromFailSafe
			);
		}
	}
}
