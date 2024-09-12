using System.Text;
using System.Threading;
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
	/// The options class for the <see cref="FusionCacheNewtonsoftJsonSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="JsonSerializerSettings"/> object to use.
		/// </summary>
		public JsonSerializerSettings? SerializerSettings { get; set; }
	}

	private static Encoding _encoding = Encoding.UTF8;

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheNewtonsoftJsonSerializer"/> object.
	/// </summary>
	/// <param name="settings">The optional <see cref="JsonSerializerSettings"/> object to use.</param>
	public FusionCacheNewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
	{
		_serializerSettings = settings;
	}

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheNewtonsoftJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheNewtonsoftJsonSerializer(Options? options)
		: this(options?.SerializerSettings)
	{
		// EMPTY
	}

	private readonly JsonSerializerSettings? _serializerSettings;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return _encoding.GetBytes(JsonConvert.SerializeObject(obj, _serializerSettings));
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonConvert.DeserializeObject<T>(_encoding.GetString(data), _serializerSettings);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize<T>(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}
}
