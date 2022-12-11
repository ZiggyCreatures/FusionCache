using System;
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
		public bool IsNull => Metadata is null;

		[MemoryPackInclude]
		public DateTimeOffset LogicalExpiration => (Metadata?.LogicalExpiration) ?? default;

		[MemoryPackInclude]
		public bool IsFromFailSafe => (Metadata?.IsFromFailSafe) ?? default;

		[MemoryPackConstructor]
		SerializableFusionCacheEntryMetadata(bool isNull, DateTimeOffset logicalExpiration, bool isFromFailSafe)
		{
			if (isNull)
			{
				this.Metadata = null;
				return;
			}

			this.Metadata = new FusionCacheEntryMetadata(logicalExpiration, isFromFailSafe);
		}

		public SerializableFusionCacheEntryMetadata(FusionCacheEntryMetadata? metadata)
		{
			this.Metadata = metadata;
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
