using System.Buffers;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public sealed class SerializationTests_Buffered(ITestOutputHelper output) : SerializationTestsBase(output)
{
	private static readonly Random Rnd = new();

	protected override byte[] Serialize<T>(IBufferFusionCacheSerializer serializer, T sourceEntry)
	{
		using var writer = new ArrayPoolBufferWriter();
		serializer.Serialize<T>(sourceEntry, writer);
		return writer.ToArray();
	}

	protected override T? Deserialize<T>(IBufferFusionCacheSerializer serializer, byte[] serializedData)
		where T : default
	{
		return serializer.Deserialize<T>(new ReadOnlySequence<byte>(serializedData));
	}

	protected override async ValueTask<byte[]> SerializeAsync<T>(IBufferFusionCacheSerializer serializer, T? obj,
		CancellationToken ct)
		where T : default
	{
		using var writer = new ArrayPoolBufferWriter();
		await serializer.SerializeAsync(obj, writer, ct);
		return writer.ToArray();
	}

	protected override async ValueTask<T?> DeserializeAsync<T>(IBufferFusionCacheSerializer serializer, byte[] data, CancellationToken ct)
		where T : default
	{
		return await serializer.DeserializeAsync<T>(new ReadOnlySequence<byte>(data), ct);
	}

	private sealed class Segment() : ReadOnlySequenceSegment<byte>
	{
		public Segment(Memory<byte> memory, int index) : this()
		{
			Memory = memory;
			RunningIndex = index;
		}

		public void SetNext(Segment next) => Next = next;
	}
}
