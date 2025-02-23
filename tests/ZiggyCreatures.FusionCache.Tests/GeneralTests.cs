﻿using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public partial class GeneralTests
	: AbstractTests
{
	public GeneralTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private static FusionCacheEntryOptions CreateEntryOptionsSample()
	{
		return new FusionCacheEntryOptions()
		{
			IsSafeForAdaptiveCaching = true,

			Duration = TimeSpan.FromSeconds(1),
			LockTimeout = TimeSpan.FromSeconds(2),
			Size = 123,
			Priority = CacheItemPriority.High,
			JitterMaxDuration = TimeSpan.FromSeconds(3),

			EagerRefreshThreshold = 0.456f,

			EnableAutoClone = !FusionCacheGlobalDefaults.EntryOptionsEnableAutoClone,

			AllowStaleOnReadOnly = true,

			IsFailSafeEnabled = !FusionCacheGlobalDefaults.EntryOptionsIsFailSafeEnabled,
			FailSafeMaxDuration = TimeSpan.FromSeconds(4),
			FailSafeThrottleDuration = TimeSpan.FromSeconds(5),

			FactorySoftTimeout = TimeSpan.FromSeconds(6),
			FactoryHardTimeout = TimeSpan.FromSeconds(7),
			AllowTimedOutFactoryBackgroundCompletion = !FusionCacheGlobalDefaults.EntryOptionsAllowTimedOutFactoryBackgroundCompletion,

			DistributedCacheDuration = TimeSpan.FromSeconds(8),
			DistributedCacheFailSafeMaxDuration = TimeSpan.FromSeconds(9),
			DistributedCacheSoftTimeout = TimeSpan.FromSeconds(10),
			DistributedCacheHardTimeout = TimeSpan.FromSeconds(11),

			ReThrowDistributedCacheExceptions = !FusionCacheGlobalDefaults.EntryOptionsReThrowDistributedCacheExceptions,
			ReThrowSerializationExceptions = !FusionCacheGlobalDefaults.EntryOptionsReThrowSerializationExceptions,
			ReThrowBackplaneExceptions = !FusionCacheGlobalDefaults.EntryOptionsReThrowBackplaneExceptions,

			AllowBackgroundDistributedCacheOperations = !FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundDistributedCacheOperations,
			AllowBackgroundBackplaneOperations = !FusionCacheGlobalDefaults.EntryOptionsAllowBackgroundBackplaneOperations,

			SkipBackplaneNotifications = !FusionCacheGlobalDefaults.EntryOptionsSkipBackplaneNotifications,

			SkipDistributedCacheRead = !FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheRead,
			SkipDistributedCacheWrite = !FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheWrite,
			SkipDistributedCacheReadWhenStale = !FusionCacheGlobalDefaults.EntryOptionsSkipDistributedCacheReadWhenStale,

			SkipMemoryCacheRead = !FusionCacheGlobalDefaults.EntryOptionsSkipMemoryCacheRead,
			SkipMemoryCacheWrite = !FusionCacheGlobalDefaults.EntryOptionsSkipMemoryCacheWrite,
		};
	}

	[Fact]
	public void CannotAssignNullToDefaultEntryOptions()
	{
		Assert.Throws<ArgumentNullException>(() =>
		{
			var foo = new FusionCacheOptions() { DefaultEntryOptions = null! };
		});
	}

	[Fact]
	public void CanDuplicateOptions()
	{
		var defaultState = new FusionCacheOptions();
		var original = new FusionCacheOptions()
		{
			CacheName = "Foo",
			CacheKeyPrefix = "Foo:",

			DefaultEntryOptions = CreateEntryOptionsSample(),

			TagsDefaultEntryOptions = CreateEntryOptionsSample(),

			EnableAutoRecovery = !defaultState.EnableAutoRecovery,
			AutoRecoveryDelay = TimeSpan.FromSeconds(123),
			AutoRecoveryMaxItems = 123,
			AutoRecoveryMaxRetryCount = 123,

			BackplaneChannelPrefix = "Foo",
			IgnoreIncomingBackplaneNotifications = !defaultState.IgnoreIncomingBackplaneNotifications,
			BackplaneCircuitBreakerDuration = TimeSpan.FromSeconds(123),

			DistributedCacheKeyModifierMode = CacheKeyModifierMode.Suffix,
			DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(123),

			EnableSyncEventHandlersExecution = !defaultState.EnableSyncEventHandlersExecution,

			ReThrowOriginalExceptions = !defaultState.ReThrowOriginalExceptions,

			PreferSyncSerialization = !defaultState.PreferSyncSerialization,

			IncludeTagsInLogs = !defaultState.IncludeTagsInLogs,
			IncludeTagsInTraces = !defaultState.IncludeTagsInTraces,
			IncludeTagsInMetrics = !defaultState.IncludeTagsInMetrics,

			DisableTagging = !defaultState.DisableTagging,

			SkipAutoCloneForImmutableObjects = !defaultState.SkipAutoCloneForImmutableObjects,

			// LOG LEVELS
			IncoherentOptionsNormalizationLogLevel = LogLevel.Critical,

			FailSafeActivationLogLevel = LogLevel.Critical,
			FactorySyntheticTimeoutsLogLevel = LogLevel.Critical,
			FactoryErrorsLogLevel = LogLevel.Critical,

			DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Critical,
			DistributedCacheErrorsLogLevel = LogLevel.Critical,
			SerializationErrorsLogLevel = LogLevel.Critical,

			BackplaneSyntheticTimeoutsLogLevel = LogLevel.Critical,
			BackplaneErrorsLogLevel = LogLevel.Critical,

			EventHandlingErrorsLogLevel = LogLevel.Critical,

			PluginsErrorsLogLevel = LogLevel.Critical,
			PluginsInfoLogLevel = LogLevel.Critical,

			MissingCacheKeyPrefixWarningLogLevel = LogLevel.Critical,
		};

		var duplicated = original.Duplicate();

		Assert.Equal(
			JsonConvert.SerializeObject(original),
			JsonConvert.SerializeObject(duplicated)
		);
	}

	[Fact]
	public void CanDuplicateEntryOptions()
	{
		var original = CreateEntryOptionsSample();

		var duplicated = original.Duplicate();

		Assert.Equal(
			JsonConvert.SerializeObject(original),
			JsonConvert.SerializeObject(duplicated)
		);
	}

	[Fact]
	public void CanCacheNullValues()
	{
		var duration = TimeSpan.FromSeconds(1);
		using var cache = new FusionCache(new FusionCacheOptions()
		{
			DefaultEntryOptions = new FusionCacheEntryOptions()
			{
				Duration = duration
			}
		});

		object? foo = new object();
		int factoryCallCount = 0;

		for (int i = 0; i < 10; i++)
		{
			foo = cache.GetOrSet<object?>(
				"foo",
				_ =>
				{
					factoryCallCount++;

					return null;
				}
			);
		}

		Assert.Null(foo);
		Assert.Equal(1, factoryCallCount);
	}

	[Fact]
	public void CanDisposeEvictedEntries()
	{
		var duration = TimeSpan.FromSeconds(1);
		var memoryCache = new MemoryCache(new MemoryCacheOptions()
		{
			ExpirationScanFrequency = TimeSpan.FromMilliseconds(100)
		});

		using var cache = new FusionCache(
			new FusionCacheOptions()
			{
				DefaultEntryOptions = new FusionCacheEntryOptions()
				{
					Duration = duration
				}
			},
			memoryCache
		);

		cache.Set("foo", new SimpleDisposable());

		var d1 = cache.GetOrDefault<SimpleDisposable>("foo");

		Assert.NotNull(d1);
		Assert.False(d1.IsDisposed);

		Thread.Sleep(duration.PlusALittleBit());

		var d2 = cache.GetOrDefault<SimpleDisposable>("foo");

		memoryCache.Compact(1);

		Thread.Sleep(duration.PlusALittleBit());

		Assert.Null(d2);
		Assert.False(d1.IsDisposed);

		// ADD EVENT TO AUTO-DISPOSE EVICTED ENTRIES
		cache.Events.Memory.Eviction += (sender, args) =>
		{
			((IDisposable?)args.Value)?.Dispose();
		};

		cache.Set("foo", new SimpleDisposable());

		var d3 = cache.GetOrDefault<SimpleDisposable>("foo");

		Assert.NotNull(d3);
		Assert.False(d3.IsDisposed);

		Thread.Sleep(duration.PlusALittleBit());

		var d4 = cache.GetOrDefault<SimpleDisposable>("foo");

		memoryCache.Compact(1);

		Thread.Sleep(duration.PlusALittleBit());

		Assert.Null(d4);
		Assert.True(d3.IsDisposed);
	}
}
