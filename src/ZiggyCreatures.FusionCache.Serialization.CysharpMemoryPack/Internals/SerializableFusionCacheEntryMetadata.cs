using System;
using MemoryPack;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals;

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

	[MemoryPackInclude]
	public long? Size => Metadata?.Size;

	[MemoryPackConstructor]
	SerializableFusionCacheEntryMetadata(DateTimeOffset logicalExpiration, bool isFromFailSafe, DateTimeOffset? eagerExpiration, string? etag, DateTimeOffset? lastModified, long? size)
	{
		Metadata = new FusionCacheEntryMetadata(logicalExpiration, isFromFailSafe, eagerExpiration, etag, lastModified, size);
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
			reader.Advance(1);
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
