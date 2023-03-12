using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	[MemoryDiagnoser]
	[Config(typeof(Config))]
	public class DateTimeOperationsBenchmark
	{

		private class Config : ManualConfig
		{
			public Config()
			{
				AddColumn(
					StatisticColumn.P95
				);
			}
		}

		static TimeSpan Duration { get; set; }
		static TimeSpan JitterMaxDuration { get; set; }
		static long DateTimeOffsetMaxTicks = DateTimeOffset.MaxValue.Ticks;

		[GlobalSetup]
		public void Setup()
		{
			Duration = TimeSpan.FromSeconds(42);
			JitterMaxDuration = TimeSpan.FromSeconds(3);
		}

		static double GetJitterDurationMs(TimeSpan jitterMaxDuration)
		{
			if (jitterMaxDuration <= TimeSpan.Zero)
				return 0d;

			return ConcurrentRandom.NextDouble() * jitterMaxDuration.TotalMilliseconds;
		}

		static long GetJitterDurationTicks(TimeSpan jitterMaxDuration)
		{
			if (jitterMaxDuration <= TimeSpan.Zero)
				return 0;

			return ConcurrentRandom.NextInt64(jitterMaxDuration.Ticks);
		}

		static DateTimeOffset V1_Internal(TimeSpan duration, TimeSpan jitterMaxDuration, bool addJittering)
		{
			var now = DateTimeOffset.UtcNow;
			if (addJittering && jitterMaxDuration > TimeSpan.Zero)
			{
				duration += TimeSpan.FromMilliseconds(GetJitterDurationMs(jitterMaxDuration));
			}

			if (duration > (DateTimeOffset.MaxValue - now))
			{
				return DateTimeOffset.MaxValue;
			}

			return now.Add(duration);
		}

		[Benchmark(Baseline = true)]
		public DateTimeOffset V1()
		{
			return V1_Internal(Duration, JitterMaxDuration, true);
		}

		static DateTimeOffset V2_Internal(TimeSpan duration, TimeSpan jitterMaxDuration, bool addJittering)
		{
			var now = DateTimeOffset.UtcNow;
			if (addJittering && jitterMaxDuration > TimeSpan.Zero)
			{
				duration = new TimeSpan(duration.Ticks + GetJitterDurationTicks(jitterMaxDuration));
			}

			if (duration > (DateTimeOffset.MaxValue - now))
			{
				return DateTimeOffset.MaxValue;
			}

			return now.Add(duration);
		}

		[Benchmark]
		public DateTimeOffset V2()
		{
			return V2_Internal(Duration, JitterMaxDuration, true);
		}

		static DateTimeOffset V3_Internal(TimeSpan duration, TimeSpan jitterMaxDuration, bool addJittering)
		{
			var nowTicks = DateTimeOffset.UtcNow.Ticks;
			var durationTicks = duration.Ticks;
			if (addJittering && jitterMaxDuration > TimeSpan.Zero)
			{
				durationTicks += GetJitterDurationTicks(jitterMaxDuration);
			}

			if (durationTicks > (DateTimeOffsetMaxTicks - nowTicks))
			{
				return DateTimeOffset.MaxValue;
			}

			return new DateTimeOffset(nowTicks + durationTicks, TimeSpan.Zero);
		}

		[Benchmark]
		public DateTimeOffset V3()
		{
			return V3_Internal(Duration, JitterMaxDuration, true);
		}
	}
}

/*
RESULTS

*/
