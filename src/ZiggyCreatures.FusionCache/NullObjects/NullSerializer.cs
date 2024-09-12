using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> that implements the null object pattern, meaning that it does nothing.
/// </summary>
public class NullSerializer
	: IFusionCacheSerializer
{
	/// <inheritdoc/>
	public byte[] Serialize<T>(T? obj)
	{
		return [];
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data)
	{
		return default;
	}

	/// <inheritdoc/>
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>([]);
	}

	/// <inheritdoc/>
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(default(T?));
	}
}
