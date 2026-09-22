using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZiggyCreatures.Caching.Fusion.Internals;

internal static class StopwatchPolyfill
{
#if !NET7_0_OR_GREATER
	private static readonly double _tickFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TimeSpan GetElapsedTime(long startingTimestamp)
	{
#if NET7_0_OR_GREATER
		return Stopwatch.GetElapsedTime(startingTimestamp);
#else
		return new TimeSpan((long)((Stopwatch.GetTimestamp() - startingTimestamp) * _tickFrequency));
#endif
	}
}
