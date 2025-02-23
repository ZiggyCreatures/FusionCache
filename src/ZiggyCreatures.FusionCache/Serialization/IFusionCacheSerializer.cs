namespace ZiggyCreatures.Caching.Fusion.Serialization;

/// <summary>
/// A generic serializer that converts any object instance to and from byte[], used along the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> .
/// </summary>
public interface IFusionCacheSerializer
{
	/// <summary>
	/// Serialized the specified <paramref name="obj"/> into a byte[].
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <returns>The byte[] which represents the serialized <paramref name="obj"/>.</returns>
	byte[] Serialize<T>(T? obj);

	/// <summary>
	/// Deserialized the specified byte[] <paramref name="data"/> into an object of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <returns>The deserialized object.</returns>
	T? Deserialize<T>(byte[] data);

	/// <summary>
	/// Serialized the specified <paramref name="obj"/> into a byte[].
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="token">The cancellation token.</param>
	/// <returns>The byte[] which represents the serialized <paramref name="obj"/>.</returns>
	ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default);

	/// <summary>
	/// Deserialized the specified byte[] <paramref name="data"/> into an object of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// /// <param name="token">The cancellation token.</param>
	/// <returns>The deserialized object.</returns>
	ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default);
}
