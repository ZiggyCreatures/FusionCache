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
		public DateTimeOffset? EagerExpiration => Metadata?.EagerExpiration;

		[MemoryPackInclude]
		public string? ETag => Metadata?.ETag;

		[MemoryPackInclude]
		public DateTimeOffset? LastModified => Metadata?.LastModified;

		[MemoryPackConstructor]
		SerializableFusionCacheEntryMetadata(DateTimeOffset logicalExpiration, bool isFromFailSafe, DateTimeOffset? eagerExpiration, string? etag, DateTimeOffset? lastModified)
		{
			Metadata = new FusionCacheEntryMetadata(logicalExpiration, isFromFailSafe, eagerExpiration, etag, lastModified);
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
