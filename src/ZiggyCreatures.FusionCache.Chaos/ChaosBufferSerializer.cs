using System.Buffers;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IBufferFusionCacheSerializer"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosBufferSerializer : ChaosSerializer, IBufferFusionCacheSerializer
{
	private readonly IBufferFusionCacheSerializer _innerSerializer;

	/// <summary>
	/// Initializes a new instance of the ChaosSerializer class.
	/// </summary>
	/// <param name="innerSerializer">The actual <see cref="IBufferFusionCacheSerializer"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosBufferSerializer(IBufferFusionCacheSerializer innerSerializer, ILogger<ChaosSerializer>? logger = null)
		: base(innerSerializer, logger)
	{
		_innerSerializer = innerSerializer;
	}

	/// <inheritdoc/>
	public void Serialize<T>(T? obj, IBufferWriter<byte> destination)
	{
		MaybeChaos();
		_innerSerializer.Serialize<T>(obj, destination);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(in ReadOnlySequence<byte> data)
	{
		MaybeChaos();
		return _innerSerializer.Deserialize<T>(data);
	}

	/// <inheritdoc/>
	public async ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		await MaybeChaosAsync().ConfigureAwait(false);
		await _innerSerializer.SerializeAsync<T>(obj, destination, token);
	}

	/// <inheritdoc/>
	public async ValueTask<T?> DeserializeAsync<T>(ReadOnlySequence<byte> data, CancellationToken token = default)
	{
		await MaybeChaosAsync().ConfigureAwait(false);
		return await _innerSerializer.DeserializeAsync<T>(data, token);
	}
}
