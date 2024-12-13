using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Text;

using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
/// </summary>
public class FusionCacheServiceStackJsonSerializer
	: IFusionCacheSerializer
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
