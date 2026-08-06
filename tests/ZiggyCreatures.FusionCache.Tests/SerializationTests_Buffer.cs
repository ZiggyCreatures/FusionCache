using System.Buffers;
using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.NullObjects;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public partial class SerializationTests
	: AbstractTests
{
	private sealed class ChainSegment : ReadOnlySequenceSegment<byte>
	{
		public ChainSegment(ReadOnlyMemory<byte> memory, ChainSegment? previous)
		{
			Memory = memory;
			if (previous is not null)
			{
				RunningIndex = previous.RunningIndex + previous.Memory.Length;
				previous.Next = this;
			}
		}
	}

	private static ReadOnlySequence<byte> ToMultiSegmentSequence(byte[] data, int segmentSize)
	{
		if (data.Length <= segmentSize)
			return new ReadOnlySequence<byte>(data);

		ChainSegment? first = null;
		ChainSegment? last = null;
		for (var offset = 0; offset < data.Length; offset += segmentSize)
		{
			last = new ChainSegment(data.AsMemory(offset, Math.Min(segmentSize, data.Length - offset)), last);
			first ??= last;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	private static IBufferFusionCacheSerializer GetBufferSerializer(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		return Assert.IsAssignableFrom<IBufferFusionCacheSerializer>(serializer);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void AllFirstPartySerializersSupportBuffers(SerializerType serializerType)
	{
		GetBufferSerializer(serializerType);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferSerializeMatchesClassicDeserialize(SerializerType serializerType)
	{
		var serializer = GetBufferSerializer(serializerType);
		var obj = ComplexType.CreateSample();

		var writer = new ArrayBufferWriter<byte>();
		serializer.Serialize(obj, writer);
		Assert.True(writer.WrittenCount > 0);

		// BYTES PRODUCED VIA THE BUFFERED PATH MUST BE READABLE VIA THE CLASSIC ONE (MIXED FLEETS)
		var looped = serializer.Deserialize<ComplexType>(writer.WrittenSpan.ToArray());
		Assert.Equal(obj, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ClassicSerializeMatchesBufferDeserialize(SerializerType serializerType)
	{
		var serializer = GetBufferSerializer(serializerType);
		var obj = ComplexType.CreateSample();

		// BYTES PRODUCED VIA THE CLASSIC PATH MUST BE READABLE VIA THE BUFFERED ONE (MIXED FLEETS)
		var data = serializer.Serialize(obj);
		var looped = serializer.Deserialize<ComplexType>(new ReadOnlySequence<byte>(data));
		Assert.Equal(obj, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferDeserializeSupportsMultiSegmentSequences(SerializerType serializerType)
	{
		var serializer = GetBufferSerializer(serializerType);
		var obj = Enumerable.Range(0, 100).Select(_ => ComplexType.CreateSample()).ToArray();

		var data = serializer.Serialize(obj);
		var sequence = ToMultiSegmentSequence(data, Math.Max(1, data.Length / 4));
		Assert.False(sequence.IsSingleSegment);

		var looped = serializer.Deserialize<ComplexType[]>(in sequence);
		Assert.Equal(obj, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferLoopSucceedsWithDistributedEntry(SerializerType serializerType)
	{
		var serializer = GetBufferSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, now.UtcTicks, now.AddSeconds(10).UtcTicks, [], new FusionCacheEntryMetadata(true, now.AddSeconds(9).UtcTicks, "abc123", now.UtcTicks, 123, 1));

		var writer = new ArrayBufferWriter<byte>();
		serializer.Serialize(obj, writer);
		Assert.True(writer.WrittenCount > 0);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(new ReadOnlySequence<byte>(writer.WrittenSpan.ToArray()));
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.LogicalExpirationTimestamp, looped.LogicalExpirationTimestamp);
		Assert.Equal(obj.Metadata!.IsStale, looped.Metadata!.IsStale);
		Assert.Equal(obj.Metadata!.EagerExpirationTimestamp, looped.Metadata!.EagerExpirationTimestamp);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModifiedTimestamp, looped.Metadata!.LastModifiedTimestamp);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
		Assert.Equal(obj.Metadata!.Priority, looped.Metadata!.Priority);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferLoopDoesNotFailWithNull(SerializerType serializerType)
	{
		var serializer = GetBufferSerializer(serializerType);

		var writer = new ArrayBufferWriter<byte>();
		serializer.Serialize<string>(null, writer);

		var looped = serializer.Deserialize<string>(new ReadOnlySequence<byte>(writer.WrittenSpan.ToArray()));
		Assert.Null(looped);
	}

	[Fact]
	public void NullSerializerSupportsBuffers()
	{
		var serializer = Assert.IsAssignableFrom<IBufferFusionCacheSerializer>(new NullSerializer());

		// SAME AS THE CLASSIC PATH: WRITES NOTHING, READS BACK NOTHING
		var writer = new ArrayBufferWriter<byte>();
		serializer.Serialize(SampleString, writer);
		Assert.Equal(0, writer.WrittenCount);

		Assert.Null(serializer.Deserialize<string>(ReadOnlySequence<byte>.Empty));
	}
}
