using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;

namespace ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses Neuecc's famous MessagePack serializer.
/// </summary>
public class FusionCacheNeueccMessagePackSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheNeueccMessagePackSerializer"/> object.
	/// </summary>
	/// <param name="options">The <see cref="MessagePackSerializerOptions"/> to use: if not specified, the contract-less (<see cref="ContractlessStandardResolver"/>) options will be used.</param>
	public FusionCacheNeueccMessagePackSerializer(MessagePackSerializerOptions? options = null)
	{
		// PER @neuecc 'S SUGGESTION: DEFAULT TO THE CONTRACTLESS RESOLVER
		Options = options ?? ContractlessStandardResolver.Options;
	}

	private readonly MessagePackSerializerOptions? Options;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return MessagePackSerializer.Serialize<T?>(obj, Options);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return MessagePackSerializer.Deserialize<T?>(data, Options);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj)
	{
		// PER @neuecc 'S SUGGESTION: AVOID AWAITING ON A MEMORY STREAM
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data)
	{
		// PER @neuecc 'S SUGGESTION: AVOID AWAITING ON A MEMORY STREAM
		return new ValueTask<T?>(Deserialize<T>(data));
	}
}
