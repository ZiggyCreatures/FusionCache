using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Tests
{

	public class EventsTests
	{
		public enum EntryActionKind
		{
			Miss = 0,
			Hit = 1,
			Set = 2,
			Remove = 3
		}

		public class EntryActionsStats
		{
			public EntryActionsStats()
			{
				Data = new ConcurrentDictionary<EntryActionKind, int>();
			}

			public ConcurrentDictionary<EntryActionKind, int> Data { get; }
			public void RecordAction(EntryActionKind kind)
			{
				Data.AddOrUpdate(kind, 1, (_, x) => x + 1);
			}
		}

		[Fact]
		public async Task EntryEventsWorkAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(2);

			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);

				// SETUP HANDLERS
				cache.Events.General.Miss += onMiss;
				cache.Events.General.Hit += onHit;
				cache.Events.General.Set += onSet;
				cache.Events.General.Remove += onRemove;

				// MISS: +1
				await cache.TryGetAsync<int>("foo");

				// MISS: +1
				await cache.TryGetAsync<int>("bar");

				// SET: +1
				await cache.SetAsync<int>("foo", 123);

				// HIT: +1
				await cache.TryGetAsync<int>("foo");

				// HIT: +1
				await cache.TryGetAsync<int>("foo");

				// MISS: +1
				await cache.TryGetAsync<int>("bar");

				// REMOVE: +1
				await cache.RemoveAsync("foo");

				// REMOVE: +1
				await cache.RemoveAsync("bar");

				await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

				// REMOVE HANDLERS
				cache.Events.General.Miss -= onMiss;
				cache.Events.General.Hit -= onHit;
				cache.Events.General.Set -= onSet;
				cache.Events.General.Remove -= onRemove;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Hit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
			}
		}

		[Fact]
		public void EntryEventsWork()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(2);

			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);

				// SETUP HANDLERS
				cache.Events.General.Miss += onMiss;
				cache.Events.General.Hit += onHit;
				cache.Events.General.Set += onSet;
				cache.Events.General.Remove += onRemove;

				// MISS: +1
				cache.TryGet<int>("foo");

				// MISS: +1
				cache.TryGet<int>("bar");

				// SET: +1
				cache.Set<int>("foo", 123);

				// HIT: +1
				cache.TryGet<int>("foo");

				// HIT: +1
				cache.TryGet<int>("foo");

				// MISS: +1
				cache.TryGet<int>("bar");

				// REMOVE: +1
				cache.Remove("foo");

				// REMOVE: +1
				cache.Remove("bar");

				Thread.Sleep(TimeSpan.FromSeconds(5));

				// REMOVE HANDLERS
				cache.Events.General.Miss -= onMiss;
				cache.Events.General.Hit -= onHit;
				cache.Events.General.Set -= onSet;
				cache.Events.General.Remove -= onRemove;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Hit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
			}
		}

	}
}
