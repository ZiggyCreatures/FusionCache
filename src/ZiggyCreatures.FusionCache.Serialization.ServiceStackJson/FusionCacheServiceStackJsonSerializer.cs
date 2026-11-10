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
	: IBufferFusionCacheSerializer
{
	static FusionCacheServiceStackJsonSerializer()
	{
		JsConfig.Init(new Config
		{
			DateHandler = DateHandler.ISO8601
		});
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
		using var stream = new ArrayPoolWritableStream();
		JsonSerializer.SerializeToStream<T?>(obj, stream);
		return stream.GetBytes();
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
			Encoding.UTF8.GetChars(data, 0, data.Length, chars, 0);
			return JsonSerializer.DeserializeFromSpan<T?>(chars.AsSpan(0, numChars));
		}
		finally
		{
			ArrayPool<char>.Shared.Return(chars);
		}
	}

	/// <inheritdoc />
	public T? Deserialize<T>(in ReadOnlySequence<byte> data)
	{
		// SERVICESTACK NEEDS CHARS: GET AN ARRAY SEGMENT WITHOUT COPYING WHEN POSSIBLE (SINGLE-SEGMENT,
		// ARRAY-BACKED), OTHERWISE FALL BACK TO A POOLED COPY
		byte[]? rented = null;
		try
		{
			ArraySegment<byte> segment;
			if (data.IsSingleSegment && MemoryMarshal.TryGetArray(data.First, out segment))
			{
				// NO COPY NEEDED
			}
			else
			{
				var length = checked((int)data.Length);
				rented = ArrayPool<byte>.Shared.Rent(length);
				data.CopyTo(rented);
				segment = new ArraySegment<byte>(rented, 0, length);
			}

			int numChars = Encoding.UTF8.GetCharCount(segment.Array!, segment.Offset, segment.Count);
			var chars = ArrayPool<char>.Shared.Rent(numChars);
			try
			{
				Encoding.UTF8.GetChars(segment.Array!, segment.Offset, segment.Count, chars, 0);
				return JsonSerializer.DeserializeFromSpan<T?>(chars.AsSpan(0, numChars));
			}
			finally
			{
				ArrayPool<char>.Shared.Return(chars);
			}
		}
		finally
		{
			if (rented is not null)
				ArrayPool<byte>.Shared.Return(rented);
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
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));

		// NOTE: DON'T USE THE STREAM VERSION, IT'S BUGGED
		//using var stream = new MemoryStream(data);
		//return await JsonSerializer.DeserializeFromStreamAsync<T?>(stream);
	}

	/// <inheritdoc />
	public override string ToString() => GetType().Name;
}
