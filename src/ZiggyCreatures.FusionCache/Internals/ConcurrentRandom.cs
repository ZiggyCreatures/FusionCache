using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// A thread-safe version of the Random class.
/// <br/>
/// Inspired by Ben Adam's ConcurrentRandom (see <a href="https://github.com/benaadams/Ben.Http/blob/main/src/Ben.Http/Random.cs">here</a>).
/// </summary>
public static class ConcurrentRandom
{
	[ThreadStatic]
	private static Random? _random;

	private static Random CreateRandom()
	{
		return new Random((int)Stopwatch.GetTimestamp());
	}

	private static Random Random
	{
		get { return _random ??= CreateRandom(); }
	}

	/// <summary>
	/// Returns a non-negative random integer that is greater than or equal to 0 and less than <see cref="int.MaxValue"/>.
	/// </summary>
	public static int Next()
	{
		return Random.Next();
	}

	/// <summary>
	/// Returns a non-negative random integer that is less than the specified maximum.
	/// </summary>
	/// <param name="maxValue">The exclusive upper bound of the random number to be generated: must be greater than or equal to 0.</param>
	public static int Next(int maxValue)
	{
		return Random.Next(maxValue);
	}

	/// <summary>
	/// Returns a random integer that is within a specified range.
	/// </summary>
	/// <param name="minValue">The inclusive lower bound of the random number returned.</param>
	/// <param name="maxValue">The exclusive upper bound of the random number returned: <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
	public static int Next(int minValue, int maxValue)
	{
		return Random.Next(minValue, maxValue);
	}

	/// <summary>
	/// Returns a random long that is within a specified range.
	/// </summary>
	/// <param name="maxValue">The exclusive upper bound of the random number returned: must be greater than or equal to 0.</param>
	public static long NextInt64(long maxValue)
	{
		var buf = new byte[8];
		Random.NextBytes(buf);
		return Math.Abs(BitConverter.ToInt64(buf, 0) % maxValue);
	}

	/// <summary>
	/// Returns a random long that is within a specified range.
	/// </summary>
	/// <param name="minValue">The inclusive lower bound of the random number returned.</param>
	/// <param name="maxValue">The exclusive upper bound of the random number returned: <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
	public static long NextInt64(long minValue, long maxValue)
	{
		var buf = new byte[8];
		Random.NextBytes(buf);
		return Math.Abs(BitConverter.ToInt64(buf, 0) % (maxValue - minValue)) + minValue;
	}

	/// <summary>
	/// Fills the elements of a specified array of bytes with random numbers.
	/// </summary>
	/// <param name="buffer">The array to be filled with random numbers.</param>
	public static void NextBytes(byte[] buffer)
	{
		Random.NextBytes(buffer);
	}

	/// <summary>
	/// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
	/// </summary>
	public static double NextDouble()
	{
		return Random.NextDouble();
	}
}
