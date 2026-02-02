using System.Buffers;
using System.Text.Json;

namespace ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the System.Text.Json serializer.
/// </summary>
public class FusionCacheSystemTextJsonSerializer
	: IFusionCacheSerializer, IBufferFusionCacheSerializer
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
		: this(new Options { SerializerOptions = options })
	{
		// EMPTY
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(Options? options)
	{
		_options = options;
	}

	private readonly Options? _options;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes<T?>(obj, _options?.SerializerOptions);
	}

	/// <inheritdoc />
	public void Serialize<T>(T? obj, IBufferWriter<byte> destination)
	{
		var options = _options?.SerializerOptions ?? JsonSerializerOptions.Default;
		var writerOptions = new JsonWriterOptions
		{
			Encoder = options.Encoder,
			Indented = options.WriteIndented,
			MaxDepth = options.MaxDepth,
			SkipValidation = true,
		};

		using var writer = new Utf8JsonWriter(destination, writerOptions);

		JsonSerializer.Serialize<T?>(writer, obj, options);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonSerializer.Deserialize<T>(data, _options?.SerializerOptions);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(in ReadOnlySequence<byte> data)
	{
		var options = _options?.SerializerOptions ?? JsonSerializerOptions.Default;

		if (data.IsSingleSegment)
		{
			return JsonSerializer.Deserialize<T>(data.First.Span, options);
		}

		var readerOptions = new JsonReaderOptions
		{
			AllowTrailingCommas = options.AllowTrailingCommas,
			CommentHandling = options.ReadCommentHandling,
			MaxDepth = options.MaxDepth,
		};

		var reader = new Utf8JsonReader(data, readerOptions);
		return JsonSerializer.Deserialize<T>(ref reader, options);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		Serialize(obj, destination);
		return new ValueTask();
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(ReadOnlySequence<byte> data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(in data));
	}

	/// <inheritdoc />
	public override string ToString() => GetType().Name;
}
