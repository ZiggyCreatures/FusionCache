using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the System.Text.Json serializer.
/// </summary>
public class FusionCacheSystemTextJsonSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// The options class for the <see cref="FusionCacheSystemTextJsonSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="JsonSerializerOptions"/> object to use.
		/// </summary>
		public JsonSerializerOptions? SerializerOptions { get; set; }
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="JsonSerializerOptions"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(JsonSerializerOptions? options = null)
	{
		_serializerOptions = options;
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(Options? options)
		: this(options?.SerializerOptions)
	{
	}

	private readonly JsonSerializerOptions? _serializerOptions;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes<T?>(obj, _serializerOptions);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonSerializer.Deserialize<T>(data, _serializerOptions);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}

	/// <inheritdoc />
	public override string ToString() => GetType().Name;
}
