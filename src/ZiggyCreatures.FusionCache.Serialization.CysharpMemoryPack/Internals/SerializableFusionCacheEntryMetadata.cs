using System;
using System.Runtime.Serialization;
using MemoryPack;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals
{
	[MemoryPackable]
	internal partial class SerializableFusionCacheEntryMetadata
	{
		[MemoryPackIgnore]
		public readonly FusionCacheEntryMetadata? Metadata;

		[MemoryPackInclude]
		public DateTimeOffset LogicalExpiration => Metadata?.LogicalExpiration ?? default;

		[MemoryPackInclude]
		public bool IsFromFailSafe => Metadata?.IsFromFailSafe ?? default;

		[MemoryPackInclude]
		public DateTimeOffset? LastModified => Metadata?.LastModified;

		[MemoryPackInclude]
		public string? ETag => Metadata?.ETag;

		[MemoryPackInclude]
		public DateTimeOffset? EagerExpiration => Metadata?.EagerExpiration;

		[MemoryPackConstructor]
		SerializableFusionCacheEntryMetadata(DateTimeOffset logicalExpiration, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag, DateTimeOffset? eagerExpiration)
		{
			Metadata = new FusionCacheEntryMetadata(logicalExpiration, isFromFailSafe, lastModified, etag, eagerExpiration);
		}

		public SerializableFusionCacheEntryMetadata(FusionCacheEntryMetadata? metadata)
		{
			Metadata = metadata;
		}
	}

	internal class FusionCacheEntryMetadataFormatter : MemoryPackFormatter<FusionCacheEntryMetadata>
	{
		public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref FusionCacheEntryMetadata? value)
		{
			if (value is null)
			{
				writer.WriteNullObjectHeader();
				return;
			}

			writer.WritePackable(new SerializableFusionCacheEntryMetadata(value));
		}

		public override void Deserialize(ref MemoryPackReader reader, scoped ref FusionCacheEntryMetadata? value)
		{
			if (reader.PeekIsNull())
			{
				value = null;
				return;
			}

			var wrapped = reader.ReadPackable<SerializableFusionCacheEntryMetadata>();
			if (wrapped is null)
			{
				value = null;
				return;
			}

			value = wrapped.Metadata;
		}
	}
}
