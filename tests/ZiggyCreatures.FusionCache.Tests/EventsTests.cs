using System;
using System.Collections.Concurrent;
using System.Linq;
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
				foreach (EntryActionKind kind in Enum.GetValues(typeof(EntryActionKind)))
				{
					Data[kind] = 0;
				}
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

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				await cache.TryGetAsync<int>("bar");

				// LET THE THROTTLE DURATION PASS
				await Task.Delay(throttleDuration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// SET: +1
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
				Assert.Equal(3, stats.Data[EntryActionKind.Set]);
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

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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
				// SET: +1
				cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				cache.TryGet<int>("bar");

				// LET THE THROTTLE DURATION PASS
				Thread.Sleep(throttleDuration);

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// SET: +1
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
				Assert.Equal(3, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
				Assert.Equal(2, stats.Data[EntryActionKind.FailSafeActivate]);
			}
		}

		[Fact]
		public async Task GetOrSetAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo", async _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void GetOrSet()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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
				// SET: +1
				cache.GetOrSet<int>("foo", _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public async Task GetOrSetStaleAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				await cache.SetAsync<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				await Task.Delay(duration);

				// HIT (STALE): +1
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo", async _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void GetOrSetStale()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				cache.Set<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				Thread.Sleep(duration);

				// HIT (STALE): +1
				// SET: +1
				cache.GetOrSet<int>("foo", _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public async Task TryGetAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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
				_ = await cache.TryGetAsync<int>("foo");

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void TryGet()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
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

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public async Task TryGetStaleFailSafeAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				await cache.SetAsync<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				await Task.Delay(duration);

				// HIT (STALE): +1
				_ = await cache.TryGetAsync<int>("foo");

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void TryGetStaleFailSafe()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				cache.Set<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				Thread.Sleep(duration);

				// HIT (STALE): +1
				cache.TryGet<int>("foo");

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.StaleHit]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public async Task TryGetStaleNoFailSafeAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				await cache.SetAsync<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				await Task.Delay(duration);

				// MISS: +1
				_ = await cache.TryGetAsync<int>("foo", options => options.SetFailSafe(false));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void TryGetStaleNoFailSafe()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// INITIAL, NON-TRACKED SET
				cache.Set<int>("foo", 42);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// LET IT BECOME STALE
				Thread.Sleep(duration);

				// MISS: +1
				cache.TryGet<int>("foo", options => options.SetFailSafe(false));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public async Task MemoryLayerEventsAsync()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);

				// SETUP HANDLERS
				cache.Events.Memory.Miss += onMiss;
				cache.Events.Memory.Hit += onHit;
				cache.Events.Memory.Set += onSet;

				// MISS: +2
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo", async _ => 42);

				// REMOVE HANDLERS
				cache.Events.Memory.Miss -= onMiss;
				cache.Events.Memory.Hit -= onHit;
				cache.Events.Memory.Set -= onSet;

				Assert.Equal(2, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(3, stats.Data.Values.Sum());
			}
		}

		[Fact]
		public void MemoryLayerEvents()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(2);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(3);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.StaleHit : EntryActionKind.Hit);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);

				// SETUP HANDLERS
				cache.Events.Memory.Miss += onMiss;
				cache.Events.Memory.Hit += onHit;
				cache.Events.Memory.Set += onSet;

				// MISS: +2
				// SET: +1
				cache.GetOrSet<int>("foo", _ => 42);

				// REMOVE HANDLERS
				cache.Events.Memory.Miss -= onMiss;
				cache.Events.Memory.Hit -= onHit;
				cache.Events.Memory.Set -= onSet;

				Assert.Equal(2, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(3, stats.Data.Values.Sum());
			}
		}
	}
}
