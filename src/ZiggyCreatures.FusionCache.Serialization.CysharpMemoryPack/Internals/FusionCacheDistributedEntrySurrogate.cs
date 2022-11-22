using MemoryPack;
using MemoryPack.Formatters;
using MemoryPack.Internal;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals
{
	static internal class FusionCacheDistributedEntrySurrogate<TValue>
	{
		[Preserve]
		public static void RegisterFormatter()
		{
			try
			{
				if (MemoryPackFormatterProvider.IsRegistered<FusionCacheDistributedEntry<TValue>>() == false)
				{
					var formatter = new FusionCacheDistributedEntryFormatter<TValue>();
					MemoryPackFormatterProvider.Register(formatter);
				}

				if (MemoryPackFormatterProvider.IsRegistered<FusionCacheDistributedEntry<TValue>[]>() == false)
				{
					MemoryPackFormatterProvider.Register(new ArrayFormatter<FusionCacheDistributedEntry<TValue>>());
				}
			}
			catch
			{
				// EMPTY
			}
		}

		[Preserve]
		public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref FusionCacheDistributedEntry<TValue>? value) where TBufferWriter : class, System.Buffers.IBufferWriter<byte>
		{
			if (value == null)
			{
				writer.WriteNullObjectHeader();
				goto END;
			}

			writer.WriteObjectHeader(2);
			writer.WriteValue(value.Value);
			writer.WriteValue(value.Metadata);

END:

			return;
		}

		[Preserve]
		public static void Deserialize(ref MemoryPackReader reader, ref FusionCacheDistributedEntry<TValue>? value)
		{
			if (!reader.TryReadObjectHeader(out var count))
			{
				value = default!;
				goto END;
			}

			TValue __Value;
			FusionCacheEntryMetadata __Metadata;

			if (count == 2)
			{
				if (value == null)
				{
					__Value = reader.ReadValue<TValue>();
					__Metadata = reader.ReadValue<FusionCacheEntryMetadata>();

					goto NEW;
				}
				else
				{
					__Value = value.Value;
					__Metadata = value.Metadata;

					reader.ReadValue(ref __Value);
					reader.ReadValue(ref __Metadata);

					goto SET;
				}
			}
			else if (count > 2)
			{
				MemoryPackSerializationException.ThrowInvalidPropertyCount(2, count);
				goto READ_END;
			}
			else
			{
				if (value == null)
				{
					__Value = default!;
					__Metadata = default!;
				}
				else
				{
					__Value = value.Value;
					__Metadata = value.Metadata;
				}

				if (count == 0) goto SKIP_READ;
				reader.ReadValue(ref __Value); if (count == 1) goto SKIP_READ;
				reader.ReadValue(ref __Metadata); if (count == 2) goto SKIP_READ;

SKIP_READ:
				if (value == null)
				{
					goto NEW;
				}
				else
				{
					goto SET;
				}
			}

SET:
			goto NEW;
			// value.Value = __Value;
			// value.Metadata = __Metadata;
			goto READ_END;

NEW:
			value = new FusionCacheDistributedEntry<TValue>(__Value, __Metadata)
			{

			};
READ_END:

END:

			return;
		}
	}

	[Preserve]
	internal sealed class FusionCacheDistributedEntryFormatter<TValue> : MemoryPackFormatter<FusionCacheDistributedEntry<TValue>>
	{
		[Preserve]
		public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref FusionCacheDistributedEntry<TValue> value)
		{
			FusionCacheDistributedEntrySurrogate<TValue>.Serialize(ref writer, ref value);
		}

		[Preserve]
		public override void Deserialize(ref MemoryPackReader reader, ref FusionCacheDistributedEntry<TValue> value)
		{
			FusionCacheDistributedEntrySurrogate<TValue>.Deserialize(ref reader, ref value);
		}
	}
}
