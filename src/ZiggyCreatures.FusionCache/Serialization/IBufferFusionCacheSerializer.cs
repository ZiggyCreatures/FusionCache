using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Serialization;

/// <summary>
/// A generic serializer that converts any object instance to and from binary representation, using low-allocation primitives.
/// Used along the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// </summary>
public interface IBufferFusionCacheSerializer : IFusionCacheSerializer
{
	/// <summary>
	/// Serializes the specified <paramref name="obj"/> into the <paramref name="destination"/>.
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="destination">The target to write the serialized value.</param>
	void Serialize<T>(T? obj, IBufferWriter<byte> destination);

	/// <summary>
	/// Serializes the specified <paramref name="obj"/> into the <paramref name="destination"/>.
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="destination">The target to write the serialized value.</param>
	/// <param name="token">The cancellation token.</param>
	ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> destination, CancellationToken token = default);

	/// <summary>
	/// Deserialized the specified <see cref="ReadOnlySequence{Byte}"/> <paramref name="data"/> into an object of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <returns>The deserialized object.</returns>
	T? Deserialize<T>(in ReadOnlySequence<byte> data);

	/// <summary>
	/// Deserialized the specified <see cref="ReadOnlySequence{Byte}"/> <paramref name="data"/> into an object of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <param name="token">The cancellation token.</param>
	/// <returns>The deserialized object.</returns>
	ValueTask<T?> DeserializeAsync<T>(ReadOnlySequence<byte> data, CancellationToken token = default);
}
