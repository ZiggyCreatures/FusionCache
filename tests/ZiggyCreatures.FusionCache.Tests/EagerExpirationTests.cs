using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace FusionCacheTests;

public class EagerExpirationTests
{
	[Theory]
	[InlineData(5)]
	[InlineData(30)]
	[InlineData(60)]
	public void DistributedEntryUsesItsOwnDuration(int distributedMinutes)
	{
		var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
		var options = new FusionCacheEntryOptions
		{
			Duration = TimeSpan.FromMinutes(30),
			MemoryCacheDuration = TimeSpan.FromMinutes(5),
			DistributedCacheDuration = TimeSpan.FromMinutes(distributedMinutes),
			EagerRefreshThreshold = 0.5f
		};
		var memoryEntry = FusionCacheMemoryEntry<string>.CreateFromOptions("value", null, timestamp, null, options, false, null, null);
		var distributedEntry = FusionCacheDistributedEntry<string>.CreateFromOtherEntry(memoryEntry, options);

		var expected = timestamp + TimeSpan.FromMinutes(distributedMinutes).Ticks / 2;
		Assert.InRange(distributedEntry.Metadata!.EagerExpirationTimestamp!.Value, expected - TimeSpan.TicksPerMillisecond, expected + TimeSpan.TicksPerMillisecond);
	}

	[Theory]
	[InlineData(false, 0.5f)]
	[InlineData(false, null)]
	[InlineData(true, 0.5f)]
	public void ReloadingFromDistributedCacheDoesNotMoveEagerExpiration(bool isStale, float? threshold)
	{
		var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).UtcTicks;
		var expiration = timestamp + TimeSpan.FromMinutes(30).Ticks;
		long? eagerExpiration = !isStale && threshold.HasValue ? timestamp + TimeSpan.FromMinutes(15).Ticks : null;
		var metadata = new FusionCacheEntryMetadata(isStale, eagerExpiration, "etag", timestamp, 123, 1);
		var distributedEntry = new FusionCacheDistributedEntry<string>("value", timestamp, expiration, ["tag"], metadata);
		var options = new FusionCacheEntryOptions
		{
			MemoryCacheDuration = TimeSpan.FromMinutes(5),
			EagerRefreshThreshold = threshold
		};

		long? firstEagerExpiration = null;
		for (var i = 0; i < 3; i++)
		{
			var memoryEntry = FusionCacheMemoryEntry<string>.CreateFromOtherEntry(distributedEntry, options);
			if (eagerExpiration.HasValue)
				Assert.InRange(memoryEntry.Metadata!.EagerExpirationTimestamp!.Value, eagerExpiration.Value - TimeSpan.TicksPerMillisecond, eagerExpiration.Value + TimeSpan.TicksPerMillisecond);
			else
				Assert.Null(memoryEntry.Metadata!.EagerExpirationTimestamp);
			if (i == 0)
				firstEagerExpiration = memoryEntry.Metadata.EagerExpirationTimestamp;
			Assert.Equal(firstEagerExpiration, memoryEntry.Metadata.EagerExpirationTimestamp);
			Assert.Equal(timestamp, memoryEntry.Timestamp);
			Assert.Equal("etag", memoryEntry.Metadata.ETag);
			Assert.Equal("value", memoryEntry.GetValue<string>());
			Assert.True(memoryEntry.LogicalExpirationTimestamp < expiration);
		}
	}
}
