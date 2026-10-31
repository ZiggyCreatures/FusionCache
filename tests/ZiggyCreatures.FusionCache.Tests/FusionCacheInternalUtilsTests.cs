using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public class FusionCacheInternalUtilsTests
{
	[Fact]
	public void AdvanceTimestampOnlyMovesForward()
	{
		long timestamp = -1;

		// NEWER: ADVANCES
		Assert.Equal(-1, FusionCacheInternalUtils.AdvanceTimestamp(ref timestamp, 1_000));
		Assert.Equal(1_000, timestamp);

		// OLDER: IGNORED
		Assert.Equal(1_000, FusionCacheInternalUtils.AdvanceTimestamp(ref timestamp, 500));
		Assert.Equal(1_000, timestamp);

		// SAME: IGNORED
		Assert.Equal(1_000, FusionCacheInternalUtils.AdvanceTimestamp(ref timestamp, 1_000));
		Assert.Equal(1_000, timestamp);

		// NEWER AGAIN: ADVANCES
		Assert.Equal(1_000, FusionCacheInternalUtils.AdvanceTimestamp(ref timestamp, 2_000));
		Assert.Equal(2_000, timestamp);
	}

	[Fact]
	public void AdvanceTimestampKeepsTheMaxWithConcurrentWriters()
	{
		const int writersCount = 8;
		const int valuesPerWriter = 100_000;

		// EACH WRITER WRITES ITS OWN VALUES, IN RANDOM ORDER
		var random = new Random(42);
		var values = new long[writersCount][];
		for (int w = 0; w < writersCount; w++)
		{
			values[w] = Enumerable.Range(0, valuesPerWriter).Select(_ => (long)random.Next(1, int.MaxValue)).ToArray();
		}
		var expectedMax = values.SelectMany(x => x).Max();

		long timestamp = -1;
		var hasMovedBackwards = false;
		var writersDone = 0;

		using var barrier = new Barrier(writersCount + 1);

		// A READER CHECKING THAT THE TIMESTAMP NEVER MOVES BACKWARDS
		var reader = new Thread(() =>
		{
			barrier.SignalAndWait();
			var last = Volatile.Read(ref timestamp);
			while (Volatile.Read(ref writersDone) < writersCount)
			{
				var current = Volatile.Read(ref timestamp);
				if (current < last)
					hasMovedBackwards = true;
				last = current;
			}
		});

		var writers = Enumerable.Range(0, writersCount).Select(w => new Thread(() =>
		{
			barrier.SignalAndWait();
			foreach (var value in values[w])
			{
				FusionCacheInternalUtils.AdvanceTimestamp(ref timestamp, value);
			}
			Interlocked.Increment(ref writersDone);
		})).ToArray();

		reader.Start();
		foreach (var writer in writers)
			writer.Start();

		foreach (var writer in writers)
			writer.Join();
		reader.Join();

		Assert.False(hasMovedBackwards);
		Assert.Equal(expectedMax, timestamp);
	}
}
