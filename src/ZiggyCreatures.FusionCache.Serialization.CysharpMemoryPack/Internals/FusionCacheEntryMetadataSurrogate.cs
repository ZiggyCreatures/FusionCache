using System;
using MemoryPack;
using MemoryPack.Formatters;
using MemoryPack.Internal;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals
{
	static internal class FusionCacheEntryMetadataSurrogate
	{
		[Preserve]
		public static void RegisterFormatter()
		{
			if (!MemoryPackFormatterProvider.IsRegistered<FusionCacheEntryMetadata>())
			{
				MemoryPackFormatterProvider.Register(new FusionCacheEntryMetadataFormatter());
			}
			if (!MemoryPackFormatterProvider.IsRegistered<FusionCacheEntryMetadata[]>())
			{
				MemoryPackFormatterProvider.Register(new ArrayFormatter<FusionCacheEntryMetadata>());
			}
		}

		[Preserve]
		public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref FusionCacheEntryMetadata? value) where TBufferWriter : class, System.Buffers.IBufferWriter<byte>
		{
			if (value == null)
			{
				writer.WriteNullObjectHeader();
				goto END;
			}

			writer.WriteObjectHeader(2);
			writer.WriteVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<DateTimeOffset>());
			writer.WriteVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<bool>());

			writer.WriteUnmanaged(value.LogicalExpiration, value.IsFromFailSafe);

END:

			return;
		}

		[Preserve]
		public static void Deserialize(ref MemoryPackReader reader, ref FusionCacheEntryMetadata? value)
		{
			if (!reader.TryReadObjectHeader(out var count))
			{
				value = default!;
				goto END;
			}

			Span<int> deltas = stackalloc int[count];
			for (int i = 0; i < count; i++)
			{
				deltas[i] = reader.ReadVarIntInt32();
			}

			DateTimeOffset __LogicalExpiration;
			bool __IsFromFailSafe;

			var readCount = 2;
			if (count == 2)
			{
				if (value == null)
				{
					if (deltas[0] == 0) { __LogicalExpiration = default; } else reader.ReadUnmanaged(out __LogicalExpiration);
					if (deltas[1] == 0) { __IsFromFailSafe = default; } else reader.ReadUnmanaged(out __IsFromFailSafe);

					goto NEW;
				}
				else
				{
					__LogicalExpiration = value.LogicalExpiration;
					__IsFromFailSafe = value.IsFromFailSafe;

					if (deltas[0] != 0) reader.ReadUnmanaged(out __LogicalExpiration);
					if (deltas[1] != 0) reader.ReadUnmanaged(out __IsFromFailSafe);

					goto SET;
				}
			}
			// else if (count > 2)
			// {
			// MemoryPackSerializationException.ThrowInvalidPropertyCount(2, count);
			// goto READ_END;
			// }
			else
			{
				if (value == null)
				{
					__LogicalExpiration = default!;
					__IsFromFailSafe = default!;
				}
				else
				{
					__LogicalExpiration = value.LogicalExpiration;
					__IsFromFailSafe = value.IsFromFailSafe;
				}

				if (count == 0) goto SKIP_READ;
				if (deltas[0] != 0) reader.ReadUnmanaged(out __LogicalExpiration); if (count == 1) goto SKIP_READ;
				if (deltas[1] != 0) reader.ReadUnmanaged(out __IsFromFailSafe); if (count == 2) goto SKIP_READ;

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

			value.LogicalExpiration = __LogicalExpiration;
			value.IsFromFailSafe = __IsFromFailSafe;
			goto READ_END;

NEW:
			value = new FusionCacheEntryMetadata(__LogicalExpiration, __IsFromFailSafe);
READ_END:
			if (count == readCount) goto END;

			for (int i = readCount; i < count; i++)
			{
				reader.Advance(deltas[i]);
			}
END:

			return;
		}
	}

	[Preserve]
	internal sealed class FusionCacheEntryMetadataFormatter : MemoryPackFormatter<FusionCacheEntryMetadata>
	{
		[Preserve]
		public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref FusionCacheEntryMetadata value)
		{
			FusionCacheEntryMetadataSurrogate.Serialize(ref writer, ref value);
		}

		[Preserve]
		public override void Deserialize(ref MemoryPackReader reader, ref FusionCacheEntryMetadata value)
		{
			FusionCacheEntryMetadataSurrogate.Deserialize(ref reader, ref value);
		}
	}
}
