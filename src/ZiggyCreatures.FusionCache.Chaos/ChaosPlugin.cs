using System;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IFusionCachePlugin"/> with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosPlugin
		: IFusionCachePlugin
	{
		IFusionCachePlugin _innerPlugin;

		/// <summary>
		/// Initializes a new instance of the ChaosPlugin class.
		/// </summary>
		/// <param name="innerPlugin">The actual <see cref="IFusionCachePlugin"/> used if and when chaos does not happen.</param>
		public ChaosPlugin(IFusionCachePlugin innerPlugin)
		{
			_innerPlugin = innerPlugin ?? throw new ArgumentNullException(nameof(innerPlugin));

			ChaosThrowProbability = 0f;
			ChaosMinDelay = TimeSpan.Zero;
			ChaosMaxDelay = TimeSpan.Zero;
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
		public void Start(IFusionCache cache)
		{
			FusionCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerPlugin.Start(cache);
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			FusionCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerPlugin.Stop(cache);
		}
	}
}
