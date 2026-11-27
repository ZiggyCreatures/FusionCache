using System.Buffers;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IBufferFusionCacheSerializer"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// <br/><br/>
/// Use this instead of <see cref="ChaosSerializer"/> when the inner serializer implements <see cref="IBufferFusionCacheSerializer"/>, so that FusionCache keeps using the buffered distributed cache path (which a plain <see cref="ChaosSerializer"/> would hide).
/// </summary>
public class ChaosBufferSerializer
	: ChaosSerializer
	, IBufferFusionCacheSerializer
{
	private readonly IBufferFusionCacheSerializer _innerSerializer;

	/// <summary>
	/// Initializes a new instance of the ChaosBufferSerializer class.
	/// </summary>
	/// <param name="innerSerializer">The actual <see cref="IBufferFusionCacheSerializer"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosBufferSerializer(IBufferFusionCacheSerializer innerSerializer, ILogger<ChaosBufferSerializer>? logger = null)
		: base(innerSerializer, logger)
	{
		_innerSerializer = innerSerializer;
	}

	/// <inheritdoc/>
	public void Serialize<T>(T? obj, IBufferWriter<byte> destination)
	{
		MaybeChaos();
		_innerSerializer.Serialize(obj, destination);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(in ReadOnlySequence<byte> data)
	{
		MaybeChaos();
		return _innerSerializer.Deserialize<T>(data);
	}
}
