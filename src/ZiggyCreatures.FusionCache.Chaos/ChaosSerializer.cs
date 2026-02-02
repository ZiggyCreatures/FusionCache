using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosSerializer
	: AbstractChaosComponent
	, IFusionCacheSerializer
{
	private readonly IFusionCacheSerializer _innerSerializer;

	/// <summary>
	/// Initializes a new instance of the ChaosSerializer class.
	/// </summary>
	/// <param name="innerSerializer">The actual <see cref="IFusionCacheSerializer"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosSerializer(IFusionCacheSerializer innerSerializer, ILogger<ChaosSerializer>? logger = null)
		: base(logger)
	{
		_innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
	}

	/// <summary>
	/// Initializes a new instance of the ChaosSerializer class that implements the <see cref="IBufferFusionCacheSerializer"/> interface if the given <paramref name="innerSerializer"/> does.
	/// </summary>
	/// <param name="innerSerializer">The actual <see cref="IFusionCacheSerializer"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public static ChaosSerializer Create(IFusionCacheSerializer innerSerializer, ILogger<ChaosSerializer>? logger = null)
	{
		return innerSerializer is IBufferFusionCacheSerializer bufferSerializer
			? new ChaosBufferSerializer(bufferSerializer, logger)
			: new ChaosSerializer(innerSerializer, logger);
	}

	/// <inheritdoc/>
	public byte[] Serialize<T>(T? obj)
	{
		MaybeChaos();
		return _innerSerializer.Serialize<T>(obj);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data)
	{
		MaybeChaos();
		return _innerSerializer.Deserialize<T>(data);
	}

	/// <inheritdoc/>
	public async ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		await MaybeChaosAsync().ConfigureAwait(false);
		return await _innerSerializer.SerializeAsync<T>(obj, token);
	}

	/// <inheritdoc/>
	public async ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		await MaybeChaosAsync().ConfigureAwait(false);
		return await _innerSerializer.DeserializeAsync<T>(data, token);
	}
}
