using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Serialization;

/// <summary>
/// A serializer that can convert object instances to and from their binary representation using pooled buffers, avoiding intermediate byte[] allocations.
/// <br/><br/>
/// When both the serializer and the distributed cache support buffers (see <see cref="Microsoft.Extensions.Caching.Distributed.IBufferDistributedCache"/>), FusionCache will use this more efficient path automatically.
/// </summary>
public interface IBufferFusionCacheSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// Serializes the specified <paramref name="obj"/> into the <paramref name="destination"/> buffer writer.
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="destination">The buffer writer to write the serialized data to.</param>
	void Serialize<T>(T? obj, IBufferWriter<byte> destination);

	/// <summary>
	/// Deserializes the specified <paramref name="data"/> into an object of type <typeparamref name="T"/>.
	/// <br/><br/>
	/// <strong>IMPORTANT:</strong> the <paramref name="data"/> is backed by pooled memory which is only valid for the duration of this call: the returned object must not keep references to it.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <returns>The deserialized object.</returns>
	T? Deserialize<T>(in ReadOnlySequence<byte> data);
}
