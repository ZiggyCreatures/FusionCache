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
		_options = options;
	}

	private readonly MemoryPackSerializerOptions? _options;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return MemoryPackSerializer.Serialize<T>(obj, _options);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return MemoryPackSerializer.Deserialize<T?>(data, _options);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}
}
