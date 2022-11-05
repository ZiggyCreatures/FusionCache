using System;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosSerializer
		: IFusionCacheSerializer
	{
		private readonly IFusionCacheSerializer _innerSerializer;

		/// <summary>
		/// Initializes a new instance of the ChaosSerializer class.
		/// </summary>
		/// <param name="innerSerializer">The actual <see cref="IFusionCacheSerializer"/> used if and when chaos does not happen.</param>
		public ChaosSerializer(IFusionCacheSerializer innerSerializer)
		{
			_innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
			SetNeverChaos();
		}

		/// <summary>
		/// A <see cref="float"/> value from 0.0 to 1.0 that represents the probabilty of throwing an exception: set it to 0.0 to never throw or to 1.0 to always throw.
		/// </summary>
		public float ChaosThrowProbability { get; set; }

		/// <summary>
		/// The minimum amount of randomized delay.
		/// </summary>
		public TimeSpan ChaosMinDelay { get; set; }

		/// <summary>
		/// The maximum amount of randomized delay.
		/// </summary>
		public TimeSpan ChaosMaxDelay { get; set; }

		/// <summary>
		/// Force chaos exceptions to never be thrown.
		/// </summary>
		public void SetNeverThrow()
		{
			ChaosThrowProbability = 0f;
		}

		/// <summary>
		/// Force chaos exceptions to always be thrown.
		/// </summary>
		public void SetAlwaysThrow()
		{
			ChaosThrowProbability = 1f;
		}

		/// <summary>
		/// Force chaos delays to never happen.
		/// </summary>
		public void SetNeverDelay()
		{
			ChaosMinDelay = TimeSpan.Zero;
			ChaosMaxDelay = TimeSpan.Zero;
		}

		/// <summary>
		/// Force chaos delays to always be of exactly this amount.
		/// </summary>
		/// <param name="delay"></param>
		public void SetAlwaysDelayExactly(TimeSpan delay)
		{
			ChaosMinDelay = delay;
			ChaosMaxDelay = delay;
		}

		/// <summary>
		/// Force chaos exceptions and delays to never happen.
		/// </summary>
		public void SetNeverChaos()
		{
			SetNeverThrow();
			SetNeverDelay();
		}

		/// <summary>
		/// Force chaos exceptions to always throw, and chaos delays to always be of exactly this amount.
		/// </summary>
		/// <param name="delay"></param>
		public void SetAlwaysChaos(TimeSpan delay)
		{
			SetAlwaysThrow();
			SetAlwaysDelayExactly(delay);
		}

		/// <inheritdoc/>
		public T? Deserialize<T>(byte[] data)
		{
			FusionCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			return _innerSerializer.Deserialize<T>(data);
		}

		/// <inheritdoc/>
		public async ValueTask<T?> DeserializeAsync<T>(byte[] data)
		{
			await FusionCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			return await _innerSerializer.DeserializeAsync<T>(data);
		}

		/// <inheritdoc/>
		public byte[] Serialize<T>(T? obj)
		{
			FusionCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			return _innerSerializer.Serialize<T>(obj);
		}

		/// <inheritdoc/>
		public async ValueTask<byte[]> SerializeAsync<T>(T? obj)
		{
			await FusionCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			return await _innerSerializer.SerializeAsync<T>(obj);
		}
	}
}
