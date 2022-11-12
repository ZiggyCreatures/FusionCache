using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// Various utils to work with randomized controllable chaos.
	/// </summary>
	[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class FusioCacheChaosUtils
	{
		/// <summary>
		/// Determines if an exception should be thrown.
		/// </summary>
		/// <param name="throwProbability">The probabilty that an exception will be thrown.</param>
		/// <returns><see langword="true"/> if an exception should be thrown, <see langword="false"/> otherwise.</returns>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool ShouldCreateChaos(float throwProbability)
		{
			return FusionCacheChaosUtils.ShouldCreateChaos(throwProbability);
		}

		/// <summary>
		/// Maybe throw a <see cref="ChaosException"/> based on the specified probabilty.
		/// </summary>
		/// <param name="throwProbability">The probabilty that an exception will be thrown.</param>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void MaybeThrow(float throwProbability)
		{
			FusionCacheChaosUtils.MaybeThrow(throwProbability);
		}

		/// <summary>
		/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>.
		/// </summary>
		/// <param name="minDelay">The minimun amount of delay.</param>
		/// <param name="maxDelay">The maximum amount of delay.</param>
		/// <returns>The randomized delay.</returns>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TimeSpan RandomizeDelay(TimeSpan minDelay, TimeSpan maxDelay)
		{
			return FusionCacheChaosUtils.RandomizeDelay(minDelay, maxDelay);
		}

		/// <summary>
		/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
		/// </summary>
		/// <param name="minDelay">The minimun amount of delay.</param>
		/// <param name="maxDelay">The maximum amount of delay.</param>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void MaybeDelay(TimeSpan minDelay, TimeSpan maxDelay)
		{
			FusionCacheChaosUtils.MaybeDelay(minDelay, maxDelay);
		}

		/// <summary>
		/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
		/// </summary>
		/// <param name="minDelay">The minimun amount of delay.</param>
		/// <param name="maxDelay">The maximum amount of delay.</param>
		/// <returns>A <see cref="Task"/> instance to await.</returns>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Task MaybeDelayAsync(TimeSpan minDelay, TimeSpan maxDelay)
		{
			return FusionCacheChaosUtils.MaybeDelayAsync(minDelay, maxDelay);
		}

		/// <summary>
		/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
		/// Then, maybe, throw a <see cref="ChaosException"/> based on the specified probabilty.
		/// </summary>
		/// <param name="throwProbability">The probabilty that an exception will be thrown.</param>
		/// <param name="minDelay">The minimun amount of delay.</param>
		/// <param name="maxDelay">The maximum amount of delay.</param>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void MaybeChaos(TimeSpan minDelay, TimeSpan maxDelay, float throwProbability)
		{
			FusionCacheChaosUtils.MaybeChaos(minDelay, maxDelay, throwProbability);
		}

		/// <summary>
		/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
		/// Then, maybe, throw a <see cref="ChaosException"/> based on the specified probabilty.
		/// </summary>
		/// <param name="throwProbability">The probabilty that an exception will be thrown.</param>
		/// <param name="minDelay">The minimun amount of delay.</param>
		/// <param name="maxDelay">The maximum amount of delay.</param>
		/// <returns>A <see cref="Task"/> instance to await.</returns>
		[Obsolete("Please use Fusio(n)CacheChaosUtils class and methods (the method names are the same), there was a typo in the class name (sorry)", true)]
		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Task MaybeChaosAsync(TimeSpan minDelay, TimeSpan maxDelay, float throwProbability)
		{
			return FusionCacheChaosUtils.MaybeChaosAsync(minDelay, maxDelay, throwProbability);
		}
	}
}
