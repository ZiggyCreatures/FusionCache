using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IFusionCacheSerializer"/> wrapper that deliberately does NOT implement <see cref="IBufferFusionCacheSerializer"/>, so it can be used to check that the buffered path is not used when the serializer does not support it (while still producing the exact same bytes as the wrapped one).
/// </summary>
internal class ClassicOnlySerializer
	: IFusionCacheSerializer
{
	private readonly IFusionCacheSerializer _serializer;

	public ClassicOnlySerializer(IFusionCacheSerializer serializer)
	{
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
	}

	/// <inheritdoc/>
	public byte[] Serialize<T>(T? obj)
	{
		return _serializer.Serialize(obj);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data)
	{
		return _serializer.Deserialize<T>(data);
	}

	/// <inheritdoc/>
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return _serializer.SerializeAsync(obj, token);
	}

	/// <inheritdoc/>
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return _serializer.DeserializeAsync<T>(data, token);
	}

	/// <inheritdoc/>
	public override string ToString() => GetType().Name;
}
