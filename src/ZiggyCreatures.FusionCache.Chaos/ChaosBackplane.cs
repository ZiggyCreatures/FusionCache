using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheBackplane"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosBackplane
		: IFusionCacheBackplane
	{
		private readonly IFusionCacheBackplane _innerBackplane;

		/// <summary>
		/// Initializes a new instance of the ChaosBackplane class.
		/// </summary>
		/// <param name="innerBackplane">The actual <see cref="IFusionCacheBackplane"/> used if and when chaos does not happen.</param>
		public ChaosBackplane(IFusionCacheBackplane innerBackplane)
		{
			_innerBackplane = innerBackplane ?? throw new ArgumentNullException(nameof(innerBackplane));
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
		public void Publish(BackplaneMessage message, FusionCacheEntryOptions options)
		{
			FusioCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerBackplane.Publish(message, options);
		}

		/// <inheritdoc/>
		public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token)
		{
			await FusioCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
			await _innerBackplane.PublishAsync(message, options, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Subscribe(BackplaneSubscriptionOptions options)
		{
			_innerBackplane.Subscribe(options);
		}

		/// <inheritdoc/>
		public void Unsubscribe()
		{
			_innerBackplane.Unsubscribe();
		}
	}
}
