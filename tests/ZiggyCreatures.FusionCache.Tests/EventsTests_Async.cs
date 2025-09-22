using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Events;

namespace FusionCacheTests;

public partial class EventsTests
{
	[Fact]
	public async Task EntryEventsWorkAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromMilliseconds(100);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromMilliseconds(200);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });

		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

		EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
		EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
		EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordActionIf(EntryActionKind.Set, e is FusionCacheEntrySetEventArgs);
		EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
		EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);
		EventHandler<FusionCacheEntryEventArgs> onFactoryError = (s, e) => stats.RecordAction(EntryActionKind.FactoryError);
		EventHandler<FusionCacheEntryEventArgs> onFactorySuccess = (s, e) => stats.RecordAction(EntryActionKind.FactorySuccess);

		// SETUP HANDLERS
		cache.Events.Miss += onMiss;
		cache.Events.Hit += onHit;
		cache.Events.Set += onSet;
		cache.Events.Remove += onRemove;
		cache.Events.FailSafeActivate += onFailSafeActivate;
		cache.Events.FactoryError += onFactoryError;
		cache.Events.FactorySuccess += onFactorySuccess;

		// MISS: +1
		await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// MISS: +1
		await cache.TryGetAsync<int>("bar", token: TestContext.Current.CancellationToken);

		// SET: +1
		await cache.SetAsync<int>("foo", 123, token: TestContext.Current.CancellationToken);

		// HIT: +1
		await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// HIT: +1
		await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// HIT (STALE): +1
		// FAIL-SAFE: +1
		// FACTORY ERROR: +1
		_ = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), token: TestContext.Current.CancellationToken);

		// MISS: +1
		await cache.TryGetAsync<int>("bar", token: TestContext.Current.CancellationToken);

		// LET THE THROTTLE DURATION PASS
		await Task.Delay(throttleDuration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// HIT (STALE): +1
		// FAIL-SAFE: +1
		// FACTORY ERROR: +1
		_ = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), token: TestContext.Current.CancellationToken);

		// REMOVE: +1
		await cache.RemoveAsync("foo", token: TestContext.Current.CancellationToken);

		// MISS: +1
		// SET: +1
		// FACTORY SUCCESS: +1
		_ = await cache.GetOrSetAsync<int>("foo", async _ => 123, token: TestContext.Current.CancellationToken);

		// REMOVE: +1
		await cache.RemoveAsync("bar", token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;
		cache.Events.FactoryError -= onFactoryError;
		cache.Events.FactorySuccess -= onFactorySuccess;

		Assert.Equal(4, stats[EntryActionKind.Miss]);
		Assert.Equal(2, stats[EntryActionKind.HitNormal]);
		Assert.Equal(2, stats[EntryActionKind.HitStale]);
		Assert.Equal(2, stats[EntryActionKind.Set]);
		Assert.Equal(2, stats[EntryActionKind.Remove]);
		Assert.Equal(2, stats[EntryActionKind.FailSafeActivate]);
		Assert.Equal(2, stats[EntryActionKind.FactoryError]);
		Assert.Equal(1, stats[EntryActionKind.FactorySuccess]);
	}

	[Fact]
	public async Task GetOrSetAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromSeconds(2);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromSeconds(3);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

		EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
		EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
		EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordActionIf(EntryActionKind.Set, e is FusionCacheEntrySetEventArgs);
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
		_ = await cache.GetOrSetAsync<int>("foo", async _ => 42, token: TestContext.Current.CancellationToken);

		// MISS: +1
		// SET: +1
		_ = await cache.GetOrSetAsync<int>("foo2", 42, token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(2, stats[EntryActionKind.Miss]);
		Assert.Equal(2, stats[EntryActionKind.Set]);
		Assert.Equal(4, stats.Total);
	}

	[Fact]
	public async Task GetOrSetStaleAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromMilliseconds(200);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromMilliseconds(300);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

		EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
		EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
		EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordActionIf(EntryActionKind.Set, e is FusionCacheEntrySetEventArgs);
		EventHandler<FusionCacheEntryEventArgs> onRemove = (s, e) => stats.RecordAction(EntryActionKind.Remove);
		EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

		// INITIAL, NON-TRACKED SET
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);

		// SETUP HANDLERS
		cache.Events.Miss += onMiss;
		cache.Events.Hit += onHit;
		cache.Events.Set += onSet;
		cache.Events.Remove += onRemove;
		cache.Events.FailSafeActivate += onFailSafeActivate;

		// LET IT BECOME STALE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// MISS: +1
		// SET: +1
		_ = await cache.GetOrSetAsync<int>("foo", async _ => 42, token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(0, stats[EntryActionKind.HitStale]);
		Assert.Equal(1, stats[EntryActionKind.Miss]);
		Assert.Equal(1, stats[EntryActionKind.Set]);
		Assert.Equal(2, stats.Total);
	}

	[Fact]
	public async Task TryGetAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromSeconds(2);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromSeconds(3);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
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
		_ = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(1, stats[EntryActionKind.Miss]);
		Assert.Equal(1, stats.Total);
	}

	[Fact]
	public async Task TryGetStaleFailSafeAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromMilliseconds(200);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromMilliseconds(300);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
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
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);

		// SETUP HANDLERS
		cache.Events.Miss += onMiss;
		cache.Events.Hit += onHit;
		cache.Events.Set += onSet;
		cache.Events.Remove += onRemove;
		cache.Events.FailSafeActivate += onFailSafeActivate;

		// LET IT BECOME STALE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// HIT (STALE): +1
		_ = await cache.TryGetAsync<int>("foo", options => options.SetAllowStaleOnReadOnly(true), token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(1, stats[EntryActionKind.HitStale]);
		Assert.Equal(1, stats.Total);
	}

	[Fact]
	public async Task TryGetStaleNoAllowStaleOnReadOnlyAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromMilliseconds(200);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromMilliseconds(300);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
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
		await cache.SetAsync<int>("foo", 42, token: TestContext.Current.CancellationToken);

		// SETUP HANDLERS
		cache.Events.Miss += onMiss;
		cache.Events.Hit += onHit;
		cache.Events.Set += onSet;
		cache.Events.Remove += onRemove;
		cache.Events.FailSafeActivate += onFailSafeActivate;

		// LET IT BECOME STALE
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);

		// MISS: +1
		_ = await cache.TryGetAsync<int>("foo", token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Miss -= onMiss;
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.Remove -= onRemove;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(1, stats[EntryActionKind.Miss]);
		Assert.Equal(1, stats.Total);
	}

	[Fact]
	public async Task MemoryLevelEventsAsync()
	{
		var stats = new EntryActionsStats();

		var duration = TimeSpan.FromSeconds(2);
		var maxDuration = TimeSpan.FromDays(1);
		var throttleDuration = TimeSpan.FromSeconds(3);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });
		cache.DefaultEntryOptions.Duration = duration;
		cache.DefaultEntryOptions.IsFailSafeEnabled = true;
		cache.DefaultEntryOptions.FailSafeMaxDuration = maxDuration;
		cache.DefaultEntryOptions.FailSafeThrottleDuration = throttleDuration;

		EventHandler<FusionCacheEntryEventArgs> onMiss = (s, e) => stats.RecordAction(EntryActionKind.Miss);
		EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
		EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordActionIf(EntryActionKind.Set, e is FusionCacheEntrySetEventArgs);

		// SETUP HANDLERS
		cache.Events.Memory.Miss += onMiss;
		cache.Events.Memory.Hit += onHit;
		cache.Events.Memory.Set += onSet;

		// MISS: +2
		// SET: +1
		_ = await cache.GetOrSetAsync<int>("foo", async _ => 42, token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Memory.Miss -= onMiss;
		cache.Events.Memory.Hit -= onHit;
		cache.Events.Memory.Set -= onSet;

		Assert.Equal(2, stats[EntryActionKind.Miss]);
		Assert.Equal(1, stats[EntryActionKind.Set]);
		Assert.Equal(3, stats.Total);
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
			AllowBackgroundBackplaneOperations = false,
			SkipBackplaneNotifications = true
		};

		using var cache1 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions }, logger: CreateXUnitLogger<FusionCache>());
		using var cache2 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions }, logger: CreateXUnitLogger<FusionCache>());
		using var cache3 = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true, DefaultEntryOptions = entryOptions }, logger: CreateXUnitLogger<FusionCache>());

		var backplaneConnectionId = Guid.NewGuid().ToString("N");

		cache1.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		cache2.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));
		cache3.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }));

		await Task.Delay(InitialBackplaneDelay, TestContext.Current.CancellationToken);

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
		await cache1.SetAsync("foo", 21, opt => opt.SetSkipBackplaneNotifications(false), token: TestContext.Current.CancellationToken);
		await cache1.SetAsync("foo", 42, opt => opt.SetSkipBackplaneNotifications(false), token: TestContext.Current.CancellationToken);

		// CACHE 2
		await cache2.RemoveAsync("foo", opt => opt.SetSkipBackplaneNotifications(false), token: TestContext.Current.CancellationToken);

		Thread.Sleep(TimeSpan.FromMilliseconds(1_000));

		// REMOVE HANDLERS
		cache2.Events.Backplane.MessagePublished -= onMessagePublished2;
		cache2.Events.Backplane.MessageReceived -= onMessageReceived2;
		cache3.Events.Backplane.MessagePublished -= onMessagePublished3;
		cache3.Events.Backplane.MessageReceived -= onMessageReceived3;

		Assert.Equal(1, stats2[EntryActionKind.BackplaneMessagePublished]);
		Assert.Equal(2, stats2[EntryActionKind.BackplaneMessageReceived]);
		Assert.Equal(0, stats3[EntryActionKind.BackplaneMessagePublished]);
		Assert.Equal(3, stats3[EntryActionKind.BackplaneMessageReceived]);
	}

	[Fact]
	public async Task StaleHitForOldStaleDataAsync()
	{
		var stats = new EntryActionsStats();
		var duration = TimeSpan.FromMilliseconds(200);

		using var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true });

		EventHandler<FusionCacheEntryHitEventArgs> onHit = (s, e) => stats.RecordAction(e.IsStale ? EntryActionKind.HitStale : EntryActionKind.HitNormal);
		EventHandler<FusionCacheEntryEventArgs> onSet = (s, e) => stats.RecordActionIf(EntryActionKind.Set, e is FusionCacheEntrySetEventArgs);
		EventHandler<FusionCacheEntryEventArgs> onFailSafeActivate = (s, e) => stats.RecordAction(EntryActionKind.FailSafeActivate);

		// SETUP HANDLERS
		cache.Events.Hit += onHit;
		cache.Events.Set += onSet;
		cache.Events.FailSafeActivate += onFailSafeActivate;

		// SET: +1
		var firstValue = await cache.GetOrSetAsync<int>("foo", async _ => 21, new FusionCacheEntryOptions(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		// HIT (NORMAL): +1
		var secondValue = await cache.GetOrSetAsync<int>("foo", async _ => 10, new FusionCacheEntryOptions(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		await Task.Delay(duration.PlusALittleBit(), TestContext.Current.CancellationToken);
		// FAIL-SAFE: +1
		// HIT (STALE): +1
		var thirdValue = await cache.GetOrSetAsync<int>("foo", async _ => throw new Exception("Sloths are cool"), new FusionCacheEntryOptions(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);
		// HIT (STALE): +1
		var fourthValue = await cache.GetOrSetAsync<int>("foo", async _ => 42, new FusionCacheEntryOptions(duration).SetFailSafe(true), token: TestContext.Current.CancellationToken);

		// REMOVE HANDLERS
		cache.Events.Hit -= onHit;
		cache.Events.Set -= onSet;
		cache.Events.FailSafeActivate -= onFailSafeActivate;

		Assert.Equal(21, firstValue);
		Assert.Equal(21, secondValue);
		Assert.Equal(21, thirdValue);
		Assert.Equal(21, fourthValue);
		Assert.Equal(1, stats[EntryActionKind.Set]);
		Assert.Equal(1, stats[EntryActionKind.HitNormal]);
		Assert.Equal(2, stats[EntryActionKind.HitStale]);
		Assert.Equal(1, stats[EntryActionKind.FailSafeActivate]);
	}
}
