using System.IO;
using System.Threading.Tasks;
using ServiceStack.Text;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the Newtonsoft Json.NET serializer.
/// </summary>
public class FusionCacheServiceStackJsonSerializer
	: IFusionCacheSerializer
{

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		using (var stream = new MemoryStream())
		{
			JsonSerializer.SerializeToStream<T?>(obj, stream);
			return stream.ToArray();
		}
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		using (var stream = new MemoryStream(data))
		{
			return JsonSerializer.DeserializeFromStream<T?>(stream);
		}
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj)
	{
		return new ValueTask<byte[]>(Serialize<T>(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}
}
