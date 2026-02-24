using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using ServiceStack.Text;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
/// </summary>
public class FusionCacheServiceStackJsonSerializer
	: IFusionCacheSerializer, IBufferFusionCacheSerializer
{
	static FusionCacheServiceStackJsonSerializer()
	{
		JsConfig.Init(new Config { DateHandler = DateHandler.ISO8601 });
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheServiceStackJsonSerializer"/> object.
	/// </summary>
	public FusionCacheServiceStackJsonSerializer()
	{
	}

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		using var bufferWriter = new ArrayPoolBufferWriter();

		using (var stream = new BufferWriterStream(bufferWriter))
		{
			JsonSerializer.SerializeToStream<T?>(obj, stream);
		}

		return bufferWriter.ToArray();
	}

	/// <inheritdoc />
	public void Serialize<T>(T? obj, IBufferWriter<byte> destination)
	{
		using var stream = new BufferWriterStream(destination);
		JsonSerializer.SerializeToStream<T?>(obj, stream);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		int numChars = Encoding.UTF8.GetCharCount(data);
		var chars = ArrayPool<char>.Shared.Rent(numChars);
		try
		{
			var written = Encoding.UTF8.GetChars(data, 0, data.Length, chars, 0);
			return JsonSerializer.DeserializeFromSpan<T?>(chars.AsSpan(0, written));
		}
		finally
		{
			ArrayPool<char>.Shared.Return(chars);
		}
	}

	/// <inheritdoc />
	public T? Deserialize<T>(in ReadOnlySequence<byte> data)
	{
		if (!data.IsSingleSegment || !MemoryMarshal.TryGetArray(data.First, out var segment))
		{
			var bytes = data.ToArray();
			segment = new ArraySegment<byte>(bytes);
		}

		int numChars = Encoding.UTF8.GetCharCount(segment.Array!, segment.Offset, segment.Count);
		var chars = ArrayPool<char>.Shared.Rent(numChars);
		try
		{
			var written = Encoding.UTF8.GetChars(segment.Array!, segment.Offset, segment.Count, chars, 0);
			return JsonSerializer.DeserializeFromSpan<T?>(chars.AsSpan(0, written));
		}
		finally
		{
			ArrayPool<char>.Shared.Return(chars);
		}
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize<T>(obj));

		// NOTE: DON'T USE THE STREAM VERSION, IT'S BUGGED
		//using var stream = new MemoryStream();
		//await JsonSerializer.SerializeToStreamAsync(obj, typeof(T?), stream);
		//return stream.ToArray();
	}

	/// <inheritdoc />
	public ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		Serialize(obj, destination);
		return new ValueTask();
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));

		// NOTE: DON'T USE THE STREAM VERSION, IT'S BUGGED
		//using var stream = new MemoryStream(data);
		//return await JsonSerializer.DeserializeFromStreamAsync<T?>(stream);
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(ReadOnlySequence<byte> data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(in data));
	}

	/// <inheritdoc />
	public override string ToString() => GetType().Name;
}
