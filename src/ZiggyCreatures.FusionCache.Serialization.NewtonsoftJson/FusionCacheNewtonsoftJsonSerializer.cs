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
	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheNewtonsoftJsonSerializer"/> object.
	/// </summary>
	/// <param name="settings">The optional <see cref="JsonSerializerSettings"/> object to use.</param>
	public FusionCacheNewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
	{
		Settings = settings;
	}

	private readonly JsonSerializerSettings? Settings;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
#pragma warning disable CS8604 // Possible null reference argument.
		return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Settings));
#pragma warning restore CS8604 // Possible null reference argument.
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data), Settings);
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
