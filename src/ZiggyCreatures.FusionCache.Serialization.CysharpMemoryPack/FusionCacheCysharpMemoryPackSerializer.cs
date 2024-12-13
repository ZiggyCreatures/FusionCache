using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses Cysharp's MemoryPack serializer.
/// </summary>
public class FusionCacheCysharpMemoryPackSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// The options class for the <see cref="FusionCacheCysharpMemoryPackSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="MemoryPackSerializerOptions"/> object to use.
		/// </summary>
		public MemoryPackSerializerOptions? SerializerOptions { get; set; }
	}

	static FusionCacheCysharpMemoryPackSerializer()
	{
		MemoryPackFormatterProvider.Register<FusionCacheEntryMetadata>(new FusionCacheEntryMetadataFormatter());
		MemoryPackFormatterProvider.RegisterGenericType(typeof(FusionCacheDistributedEntry<>), typeof(FusionCacheDistributedEntryFormatter<>));
	}

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheCysharpMemoryPackSerializer"/> object.
	/// </summary>
	/// <param name="options">The <see cref="MemoryPackSerializerOptions"/> to use, or <see langword="null"/></param>
	public FusionCacheCysharpMemoryPackSerializer(MemoryPackSerializerOptions? options = null)
	{
		_serializerOptions = options;
	}

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheCysharpMemoryPackSerializer"/> object.
	/// </summary>
	/// <param name="options">The <see cref="Options"/> to use.</param>
	public FusionCacheCysharpMemoryPackSerializer(Options? options)
		: this(options?.SerializerOptions)
	{
		// EMPTY
	}

	private readonly MemoryPackSerializerOptions? _serializerOptions;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] Serialize<T>(T? obj)
	{
		var buffer = ArrayPoolBufferWriter.Rent();
		MemoryPackWriter<ArrayPoolBufferWriter> writer = new(ref buffer, MemoryPackWriterOptionalStatePool.Rent(_serializerOptions));
		try
		{
			MemoryPackSerializer.Serialize(ref writer, obj);
			return buffer.ToArray();
		}
		finally
		{
			ArrayPoolBufferWriter.Return(buffer);
		}
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T? Deserialize<T>(byte[] data)
	{
		return MemoryPackSerializer.Deserialize<T?>(data, _serializerOptions);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}

	/// <inheritdoc />
	public override string ToString() => $"{GetType().Name}";
}
