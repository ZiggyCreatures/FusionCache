using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

internal class JsonArrayPool : IArrayPool<char>
{
	internal static JsonArrayPool Shared { get; } = new JsonArrayPool();
	public char[] Rent(int minimumLength) => ArrayPool<char>.Shared.Rent(minimumLength);
	public void Return(char[]? array) => ArrayPool<char>.Shared.Return(array);
}

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the Newtonsoft Json.NET serializer.
/// </summary>
public class FusionCacheNewtonsoftJsonSerializer
	: IFusionCacheSerializer
{
	private readonly JsonSerializer _jsonSerializer;
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
		_jsonSerializer = JsonSerializer.Create(settings);
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
		using var stream = new ArrayPoolWritableStream();
		using var writer = new StreamWriter(stream);
		using JsonTextWriter jsonWriter = new JsonTextWriter(writer);
		jsonWriter.ArrayPool = JsonArrayPool.Shared;
		_jsonSerializer.Serialize(jsonWriter, obj);
		jsonWriter.Flush();
		return stream.GetBytes();
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		using var stream = new MemoryStream(data);
		using var reader = new StreamReader(stream, _encoding);
		using var jsonReader = new JsonTextReader(reader);
		jsonReader.ArrayPool = JsonArrayPool.Shared;
		return _jsonSerializer.Deserialize<T>(jsonReader);
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

	/// <inheritdoc />
	public override string ToString() => $"{GetType().Name}";
}
