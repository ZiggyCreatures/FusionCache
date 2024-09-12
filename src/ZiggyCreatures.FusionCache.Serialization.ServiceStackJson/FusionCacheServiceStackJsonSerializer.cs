using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Text;
using IORSM = Microsoft.IO.RecyclableMemoryStreamManager;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the ServiceStack JSON serializer.
/// </summary>
public class FusionCacheServiceStackJsonSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// The options class for the <see cref="FusionCacheServiceStackJsonSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="IORSM"/> object to use.
		/// </summary>
		public IORSM? StreamManager { get; set; }
	}

	static FusionCacheServiceStackJsonSerializer()
	{
		JsConfig.Init(new Config
		{
			DateHandler = DateHandler.ISO8601
		});
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheServiceStackJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheServiceStackJsonSerializer(Options? options = null)
	{
		_streamManager = options?.StreamManager;
	}

	private readonly IORSM? _streamManager;

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
		using var stream = GetMemoryStream();
		JsonSerializer.SerializeToStream<T?>(obj, stream);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		using var stream = GetMemoryStream(data);
		return JsonSerializer.DeserializeFromStream<T?>(stream);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize<T>(obj));

		// NOTE: DON'T USE THE STREAM VERSION, IT'S BUGGED
		//using var stream = new MemoryStream();
		//await JsonSerializer.SerializeToStreamAsync(obj, typeof(T?), stream);
		//return stream.ToArray();
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));

		// NOTE: DON'T USE THE STREAM VERSION, IT'S BUGGED
		//using var stream = new MemoryStream(data);
		//return await JsonSerializer.DeserializeFromStreamAsync<T?>(stream);
	}
}
