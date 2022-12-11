using MemoryPack;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals
{
	[MemoryPackable]
	internal partial class SerializableFusionCacheDistributedEntry<TValue>
	{
		[MemoryPackIgnore]
		public readonly FusionCacheDistributedEntry<TValue>? Entry;

		[MemoryPackInclude]
		public TValue? Value => Entry is not null ? Entry.Value : default;

		[MemoryPackAllowSerialize]
		public FusionCacheEntryMetadata? Metadata => Entry?.Metadata;

		[MemoryPackConstructor]
		SerializableFusionCacheDistributedEntry(TValue value, FusionCacheEntryMetadata? metadata)
		{
			this.Entry = new FusionCacheDistributedEntry<TValue>(value, metadata);
		}

		public SerializableFusionCacheDistributedEntry(FusionCacheDistributedEntry<TValue>? entry)
		{
			this.Entry = entry;
		}
	}

	internal class FusionCacheDistributedEntryFormatter<TValue> : MemoryPackFormatter<FusionCacheDistributedEntry<TValue>>
	{
		public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref FusionCacheDistributedEntry<TValue>? value)
		{
			if (value is null)
			{
				writer.WriteNullObjectHeader();
				return;
			}

			writer.WritePackable(new SerializableFusionCacheDistributedEntry<TValue>(value));
		}

		public override void Deserialize(ref MemoryPackReader reader, scoped ref FusionCacheDistributedEntry<TValue>? value)
		{
			if (reader.PeekIsNull())
			{
				value = null;
				return;
			}

			var wrapped = reader.ReadPackable<SerializableFusionCacheDistributedEntry<TValue>>();
			if (wrapped is null)
			{
				value = null;
				return;
			}

			value = wrapped.Entry;
		}
	}
}
