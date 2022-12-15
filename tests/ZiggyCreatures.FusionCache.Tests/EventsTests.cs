﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Events;

namespace FusionCacheTests
{
	public class EventsTests
	{
		public enum EntryActionKind
		{
			Miss = 0,
			HitNormal = 1,
			HitStale = 2,
			Set = 3,
			Remove = 4,
			FailSafeActivate = 5,
			FactoryError = 6,
			BackplaneMessagePublished = 7,
			BackplaneMessageReceived = 8
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

			var duration = TimeSpan.FromSeconds(1);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(2);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);
				EventHandler<FusionCacheEntryEventArgs> onFactoryError = (s, e) => stats.RecordAction(EntryActionKind.FactoryError);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;
				cache.Events.FactoryError += onFactoryError;

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

				await Task.Delay(duration.PlusALittleBit());

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// FACTORY ERROR: +1
				_ = await cache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				await cache.TryGetAsync<int>("bar");

				// LET THE THROTTLE DURATION PASS
				await Task.Delay(throttleDuration.PlusALittleBit());

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// FACTORY ERROR: +1
				_ = await cache.GetOrSetAsync<int>("foo", _ => throw new Exception("Sloths are cool"));

				// REMOVE: +1
				await cache.RemoveAsync("foo");

				// REMOVE: +1
				await cache.RemoveAsync("bar");

				await Task.Delay(TimeSpan.FromSeconds(2));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;
				cache.Events.FactoryError -= onFactoryError;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitNormal]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
				Assert.Equal(2, stats.Data[EntryActionKind.FailSafeActivate]);
				Assert.Equal(2, stats.Data[EntryActionKind.FactoryError]);
			}
		}

		[Fact]
		public void EntryEventsWork()
		{
			var stats = new EntryActionsStats();

			var duration = TimeSpan.FromSeconds(1);
			var maxDuration = TimeSpan.FromDays(1);
			var throttleDuration = TimeSpan.FromSeconds(2);

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				cache.DefaultEntryOptions.Duration = duration;
				cache.DefaultEntryOptions.IsFailSafeEnabled = true;
				cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
				cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

				EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);
				EventHandler<FusionCacheEntryEventArgs> onFactoryError = (s, e) => stats.RecordAction(EntryActionKind.FactoryError);

				// SETUP HANDLERS
				cache.Events.Miss += onMiss;
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.Remove += onRemove;
				cache.Events.FailSafeActivate += onFailSafeActivate;
				cache.Events.FactoryError += onFactoryError;

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

				Thread.Sleep(duration.PlusALittleBit());

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// FACTORY ERROR: +1
				cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"));

				// MISS: +1
				cache.TryGet<int>("bar");

				// LET THE THROTTLE DURATION PASS
				Thread.Sleep(throttleDuration.PlusALittleBit());

				// HIT (STALE): +1
				// FAIL-SAFE: +1
				// FACTORY ERROR: +1
				cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"));

				// REMOVE: +1
				cache.Remove("foo");

				// REMOVE: +1
				cache.Remove("bar");

				Thread.Sleep(TimeSpan.FromSeconds(2));

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;
				cache.Events.FactoryError -= onFactoryError;

				Assert.Equal(3, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitNormal]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(2, stats.Data[EntryActionKind.Remove]);
				Assert.Equal(2, stats.Data[EntryActionKind.FailSafeActivate]);
				Assert.Equal(2, stats.Data[EntryActionKind.FactoryError]);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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

				// MISS: +1
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo2", 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(2, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Set]);
				Assert.Equal(4, stats.Data.Values.Sum());
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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

				// MISS: +1
				// SET: +1
				cache.GetOrSet<int>("foo2", 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(2, stats.Data[EntryActionKind.Miss]);
				Assert.Equal(2, stats.Data[EntryActionKind.Set]);
				Assert.Equal(4, stats.Data.Values.Sum());
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				await Task.Delay(duration.PlusALittleBit());

				// MISS: +1
				// SET: +1
				_ = await cache.GetOrSetAsync<int>("foo", async _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(0, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				Thread.Sleep(duration.PlusALittleBit());

				// MISS: +1
				// SET: +1
				cache.GetOrSet<int>("foo", _ => 42);

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(0, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.Miss]);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				await Task.Delay(duration.PlusALittleBit());

				// HIT (STALE): +1
				_ = await cache.TryGetAsync<int>("foo");

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.HitStale]);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				Thread.Sleep(duration.PlusALittleBit());

				// HIT (STALE): +1
				cache.TryGet<int>("foo");

				// REMOVE HANDLERS
				cache.Events.Miss -= onMiss;
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.Remove -= onRemove;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(1, stats.Data[EntryActionKind.HitStale]);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				await Task.Delay(duration.PlusALittleBit());

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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				Thread.Sleep(duration.PlusALittleBit());

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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
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

		[Fact]
		public async Task BackplaneEventsAsync()
		{
			var stats2 = new EntryActionsStats();
			var stats3 = new EntryActionsStats();

			var entryOptions = new FusionCacheEntryOptions
			{
				Duration = TimeSpan.FromMinutes(10),
				AllowBackgroundDistributedCacheOperations = false,
				AllowBackgroundBackplaneOperations = false
			};

			using var cache1 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });
			using var cache2 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });
			using var cache3 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });

			cache1.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			cache2.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			cache3.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));

			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessagePublished2 = (s, e) => stats2.RecordAction(EntryActionKind.BackplaneMessagePublished);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessageReceived2 = (s, e) => stats2.RecordAction(EntryActionKind.BackplaneMessageReceived);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessagePublished3 = (s, e) => stats3.RecordAction(EntryActionKind.BackplaneMessagePublished);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessageReceived3 = (s, e) => stats3.RecordAction(EntryActionKind.BackplaneMessageReceived);

			// SETUP HANDLERS
			cache2.Events.Backplane.MessagePublished += onMessagePublished2;
			cache2.Events.Backplane.MessageReceived += onMessageReceived2;
			cache3.Events.Backplane.MessagePublished += onMessagePublished3;
			cache3.Events.Backplane.MessageReceived += onMessageReceived3;

			// CACHE 1
			await cache1.SetAsync("foo", 21);
			await cache1.SetAsync("foo", 42);

			// CACHE 2
			await cache2.RemoveAsync("foo");

			// REMOVE HANDLERS
			cache2.Events.Backplane.MessagePublished -= onMessagePublished2;
			cache2.Events.Backplane.MessageReceived -= onMessageReceived2;
			cache3.Events.Backplane.MessagePublished -= onMessagePublished3;
			cache3.Events.Backplane.MessageReceived -= onMessageReceived3;

			Assert.Equal(1, stats2.Data[EntryActionKind.BackplaneMessagePublished]);
			Assert.Equal(2, stats2.Data[EntryActionKind.BackplaneMessageReceived]);
			Assert.Equal(0, stats3.Data[EntryActionKind.BackplaneMessagePublished]);
			Assert.Equal(3, stats3.Data[EntryActionKind.BackplaneMessageReceived]);
		}

		[Fact]
		public void BackplaneEvents()
		{
			var stats2 = new EntryActionsStats();
			var stats3 = new EntryActionsStats();

			var entryOptions = new FusionCacheEntryOptions
			{
				Duration = TimeSpan.FromMinutes(10),
				AllowBackgroundDistributedCacheOperations = false,
				AllowBackgroundBackplaneOperations = false
			};

			using var cache1 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });
			using var cache2 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });
			using var cache3 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions });

			cache1.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			cache2.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			cache3.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));

			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessagePublished2 = (s, e) => stats2.RecordAction(EntryActionKind.BackplaneMessagePublished);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessageReceived2 = (s, e) => stats2.RecordAction(EntryActionKind.BackplaneMessageReceived);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessagePublished3 = (s, e) => stats3.RecordAction(EntryActionKind.BackplaneMessagePublished);
			EventHandler<FusionCacheBackplaneMessageEventArgs> onMessageReceived3 = (s, e) => stats3.RecordAction(EntryActionKind.BackplaneMessageReceived);

			// SETUP HANDLERS
			cache2.Events.Backplane.MessagePublished += onMessagePublished2;
			cache2.Events.Backplane.MessageReceived += onMessageReceived2;
			cache3.Events.Backplane.MessagePublished += onMessagePublished3;
			cache3.Events.Backplane.MessageReceived += onMessageReceived3;

			// CACHE 1
			cache1.Set("foo", 21);
			cache1.Set("foo", 42);

			// CACHE 2
			cache2.Remove("foo");

			// REMOVE HANDLERS
			cache2.Events.Backplane.MessagePublished -= onMessagePublished2;
			cache2.Events.Backplane.MessageReceived -= onMessageReceived2;
			cache3.Events.Backplane.MessagePublished -= onMessagePublished3;
			cache3.Events.Backplane.MessageReceived -= onMessageReceived3;

			Assert.Equal(1, stats2.Data[EntryActionKind.BackplaneMessagePublished]);
			Assert.Equal(2, stats2.Data[EntryActionKind.BackplaneMessageReceived]);
			Assert.Equal(0, stats3.Data[EntryActionKind.BackplaneMessagePublished]);
			Assert.Equal(3, stats3.Data[EntryActionKind.BackplaneMessageReceived]);
		}

		[Fact]
		public async Task StaleHitForOldStaleDataAsync()
		{
			var stats = new EntryActionsStats();

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// SETUP HANDLERS
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// SET: +1
				var firstValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				// HIT (NORMAL): +1
				var secondValue = await cache.GetOrSetAsync<int>("foo", async _ => 10, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				await Task.Delay(1_500);
				// FAIL-SAFE: +1
				// HIT (STALE): +1
				var thirdValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				// HIT (STALE): +1
				var fourthValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

				// REMOVE HANDLERS
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(21, firstValue);
				Assert.Equal(21, secondValue);
				Assert.Equal(21, thirdValue);
				Assert.Equal(21, fourthValue);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(1, stats.Data[EntryActionKind.HitNormal]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.FailSafeActivate]);
			}
		}

		[Fact]
		public void StaleHitForOldStaleData()
		{
			var stats = new EntryActionsStats();

			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
				EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordAction(EntryActionKind.Set);
				EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

				// SETUP HANDLERS
				cache.Events.Hit += onHit;
				cache.Events.Set += onSet;
				cache.Events.FailSafeActivate += onFailSafeActivate;

				// SET: +1
				var firstValue = cache.GetOrSet<int>("foo", _ => 21, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				// HIT (NORMAL): +1
				var secondValue = cache.GetOrSet<int>("foo", _ => 10, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				Thread.Sleep(1_500);
				// FAIL-SAFE: +1
				// HIT (STALE): +1
				var thirdValue = cache.GetOrSet<int>("foo", _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));
				// HIT (STALE): +1
				var fourthValue = cache.GetOrSet<int>("foo", _ => 42, new FusionCacheEntryOptions(TimeSpan.FromSeconds(1)).SetFailSafe(true));

				// REMOVE HANDLERS
				cache.Events.Hit -= onHit;
				cache.Events.Set -= onSet;
				cache.Events.FailSafeActivate -= onFailSafeActivate;

				Assert.Equal(21, firstValue);
				Assert.Equal(21, secondValue);
				Assert.Equal(21, thirdValue);
				Assert.Equal(21, fourthValue);
				Assert.Equal(1, stats.Data[EntryActionKind.Set]);
				Assert.Equal(1, stats.Data[EntryActionKind.HitNormal]);
				Assert.Equal(2, stats.Data[EntryActionKind.HitStale]);
				Assert.Equal(1, stats.Data[EntryActionKind.FailSafeActivate]);
			}
		}
	}
}
