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
			StaleHit = 2,
			Set = 3,
			Remove = 4,
			FailSafeActivate = 5,
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
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) =>
				{
					if (e.IsStale)
					{
						stats.RecordAction(EntryActionKind.StaleHit);
					}
					else
					{
						stats.RecordAction(EntryActionKind.Hit);
					}
				};

				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

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

				await Task.Delay(duration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				_ = await cache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				await cache.TryGetAsync<int>("bar");

				// LET THE THROTTLE DURATION PASS
				await Task.Delay(throttleDuration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				_ = await cache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"));

				// REMOVE: +1
				await cache.RemoveAsync("foo");

				// REMOVE: +1
				await cache.RemoveAsync("bar");

				await Task.Delay(TimeSpan.FromSeconds(5));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Hit]);
				Assert.Equal(2, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
				Assert.Equal(2, stats.Data[EntryActionKind.FailSafeActivate]);
			}
		}

		[Fact]
		public void EntryEventsWork()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions()))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) =>
				{
					if (e.IsStale)
					{
						stats.RecordAction(EntryActionKind.StaleHit);
					}
					else
					{
						stats.RecordAction(EntryActionKind.Hit);
					}
				};

				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

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

				Thread.Sleep(duration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				cache.TryGet<int>("bar");

				// LET THE THROTTLE DURATION PASS
				Thread.Sleep(throttleDuration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"));

				// REMOVE: +1
				cache.Remove("foo");

				// REMOVE: +1
				cache.Remove("bar");

				Thread.Sleep(TimeSpan.FromSeconds(5));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Hit]);
				Assert.Equal(2, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
				Assert.Equal(2, stats.Data[EntryActionKind.FailSafeActivate]);
			}
		}

	}
}
