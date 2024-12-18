using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IO;

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

		/// <summary>
		/// The optional <see cref="RecyclableMemoryStreamManager"/> object to use.
		/// </summary>
		public RecyclableMemoryStreamManager? StreamManager { get; set; }
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
		_streamManager = options?.StreamManager;
	}

	private readonly JsonSerializerOptions? _serializerOptions;
	private readonly RecyclableMemoryStreamManager? _streamManager;

	private MemoryStream GetMemoryStream()
	{
		return _streamManager?.GetStream() ?? new MemoryStream();
	}

	private MemoryStream GetMemoryStream(byte[] buffer)
	{
		return _streamManager?.GetStream(buffer) ?? new MemoryStream(buffer);
	}

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
	public async ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		using var stream = GetMemoryStream();
		await JsonSerializer.SerializeAsync<T?>(stream, obj, _serializerOptions, token);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public async ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		using var stream = GetMemoryStream(data);
		return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, token);
	}

	/// <inheritdoc />
	public override string ToString() => $"{(_streamManager != null ? "Recyclable" : "")}{GetType().Name}";
}
