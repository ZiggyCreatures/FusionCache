using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the Newtonsoft Json.NET serializer.
/// </summary>
public class FusionCacheNewtonsoftJsonSerializer
	: IFusionCacheSerializer
{
	private static Encoding _encoding = Encoding.UTF8;

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheNewtonsoftJsonSerializer"/> object.
	/// </summary>
	/// <param name="settings">The optional <see cref="JsonSerializerSettings"/> object to use.</param>
	public FusionCacheNewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
	{
		_settings = settings;
	}

	private readonly JsonSerializerSettings? _settings;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return _encoding.GetBytes(JsonConvert.SerializeObject(obj, _settings));
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonConvert.DeserializeObject<T>(_encoding.GetString(data), _settings);
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
