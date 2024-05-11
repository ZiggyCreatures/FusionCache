using System.Threading.Tasks;
using ServiceStack.Text;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
/// </summary>
public class FusionCacheServiceStackJsonSerializer
	: IFusionCacheSerializer
{
	private static readonly RecyclableMemoryStreamManager _manager = new RecyclableMemoryStreamManager();

	static FusionCacheServiceStackJsonSerializer()
	{
		JsConfig.Init(new Config
		{
			DateHandler = DateHandler.ISO8601
		});
	}

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		using var stream = _manager.GetStream();

		JsonSerializer.SerializeToStream<T?>(obj, stream);

		return stream.ToArray();
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		using var stream = _manager.GetStream(data);

		return JsonSerializer.DeserializeFromStream<T?>(stream);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj)
	{
		return new ValueTask<byte[]>(Serialize<T>(obj));
	}

	/// <inheritdoc />
	public async ValueTask<T?> DeserializeAsync<T>(byte[] data)
	{
		using var stream = _manager.GetStream(data);

		return await JsonSerializer.DeserializeFromStreamAsync<T?>(stream);
	}
}
