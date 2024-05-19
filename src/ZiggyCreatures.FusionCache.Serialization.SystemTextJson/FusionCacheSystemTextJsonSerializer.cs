using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the System.Text.Json serializer.
/// </summary>
public class FusionCacheSystemTextJsonSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="JsonSerializerOptions"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(JsonSerializerOptions? options = null)
	{
		_options = options;
	}

	private readonly JsonSerializerOptions? _options;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes<T?>(obj, _options);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonSerializer.Deserialize<T>(data, _options);
	}

	/// <inheritdoc />
	public async ValueTask<byte[]> SerializeAsync<T>(T? obj)
	{
		using var stream = new MemoryStream();
		await JsonSerializer.SerializeAsync<T?>(stream, obj, _options);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public async ValueTask<T?> DeserializeAsync<T>(byte[] data)
	{
		using var stream = new MemoryStream(data);
		return await JsonSerializer.DeserializeAsync<T>(stream, _options);
	}
}
