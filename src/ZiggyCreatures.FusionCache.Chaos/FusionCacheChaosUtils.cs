using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// Various utils to work with randomized controllable chaos.
/// </summary>
public static class FusionCacheChaosUtils
{
	/// <summary>
	/// Determines if an exception should be thrown.
	/// </summary>
	/// <param name="throwProbability">The probability that an exception will be thrown.</param>
	/// <returns><see langword="true"/> if an exception should be thrown, <see langword="false"/> otherwise.</returns>
	public static bool ShouldThrow(float throwProbability)
	{
		if (throwProbability <= 0f)
			return false;

		if (throwProbability >= 1f)
			return true;

		return ConcurrentRandom.NextDouble() < throwProbability;
	}

	/// <summary>
	/// Maybe throw a <see cref="ChaosException"/> based on the specified probability.
	/// </summary>
	/// <param name="throwProbability">The probability that an exception will be thrown.</param>
	public static void MaybeThrow(float throwProbability)
	{
		if (ShouldThrow(throwProbability))
			throw new ChaosException("Just a little bit of controlled chaos");
	}

	/// <summary>
	/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>.
	/// </summary>
	/// <param name="minDelay">The minimum amount of delay.</param>
	/// <param name="maxDelay">The maximum amount of delay.</param>
	/// <returns>The randomized delay.</returns>
	public static TimeSpan GetRandomDelay(TimeSpan minDelay, TimeSpan maxDelay)
	{
		if (minDelay <= TimeSpan.Zero && maxDelay <= TimeSpan.Zero)
			return TimeSpan.Zero;

		if (minDelay >= maxDelay)
			return minDelay;

		return minDelay + TimeSpan.FromMilliseconds(ConcurrentRandom.NextDouble() * (maxDelay - minDelay).TotalMilliseconds);
	}

	/// <summary>
	/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
	/// </summary>
	/// <param name="minDelay">The minimum amount of delay.</param>
	/// <param name="maxDelay">The maximum amount of delay.</param>
	/// <param name="token">The cancellation token.</param>
	public static void MaybeDelay(TimeSpan minDelay, TimeSpan maxDelay, CancellationToken token = default)
	{
		var delay = GetRandomDelay(minDelay, maxDelay);

		if (delay > TimeSpan.Zero)
			Thread.Sleep(delay);
	}

	/// <summary>
	/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
	/// </summary>
	/// <param name="minDelay">The minimum amount of delay.</param>
	/// <param name="maxDelay">The maximum amount of delay.</param>
	/// <param name="token">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> instance to await.</returns>
	public static async Task MaybeDelayAsync(TimeSpan minDelay, TimeSpan maxDelay, CancellationToken token = default)
	{
		var delay = GetRandomDelay(minDelay, maxDelay);

		if (delay > TimeSpan.Zero)
			await Task.Delay(delay, token).ConfigureAwait(false);
	}

	/// <summary>
	/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
	/// Then, maybe, throw a <see cref="ChaosException"/> based on the specified probability.
	/// </summary>
	/// <param name="minDelay">The minimum amount of delay.</param>
	/// <param name="maxDelay">The maximum amount of delay.</param>
	/// <param name="throwProbability">The probability that an exception will be thrown.</param>
	/// <param name="token">The cancellation token.</param>
	public static void MaybeChaos(TimeSpan minDelay, TimeSpan maxDelay, float throwProbability, CancellationToken token = default)
	{
		MaybeDelay(minDelay, maxDelay, token);
		MaybeThrow(throwProbability);
	}

	/// <summary>
	/// Randomize an actual delay with a value between <paramref name="minDelay"/> and <paramref name="maxDelay"/>, and waits for it.
	/// Then, maybe, throw a <see cref="ChaosException"/> based on the specified probability.
	/// </summary>
	/// <param name="minDelay">The minimum amount of delay.</param>
	/// <param name="maxDelay">The maximum amount of delay.</param>
	/// <param name="throwProbability">The probability that an exception will be thrown.</param>
	/// <param name="token">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> instance to await.</returns>
	public static async Task MaybeChaosAsync(TimeSpan minDelay, TimeSpan maxDelay, float throwProbability, CancellationToken token = default)
	{
		await MaybeDelayAsync(minDelay, maxDelay, token).ConfigureAwait(false);
		MaybeThrow(throwProbability);
	}
}
