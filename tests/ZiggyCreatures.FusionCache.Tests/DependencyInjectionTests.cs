using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests;

public class DependencyInjectionTests
	: AbstractTests
{
	public DependencyInjectionTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	static ILogger? GetLogger(IFusionCache cache)
	{
		return typeof(FusionCache).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as ILogger;
	}

	static IFusionCacheMemoryLocker? GetMemoryLocker(IFusionCache cache)
	{
		return typeof(FusionCache).GetField("_memoryLocker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as IFusionCacheMemoryLocker;
	}

	static IMemoryCache? GetMemoryCache(IFusionCache cache)
	{
		var _mca = typeof(FusionCache).GetField("_mca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache);
		return _mca!.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_mca) as IMemoryCache;
	}

	static IFusionCacheSerializer? GetSerializer(IFusionCache cache)
	{
		var dca = typeof(FusionCache).GetField("_dca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as DistributedCacheAccessor;
		if (dca is null)
			return null;

		return typeof(DistributedCacheAccessor).GetField("_serializer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dca) as IFusionCacheSerializer;
	}

	static IDistributedCache? GetDistributedCache<TDistributedCache>(IFusionCache cache)
		where TDistributedCache : class, IDistributedCache
	{
		var dca = typeof(FusionCache).GetField("_dca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as DistributedCacheAccessor;
		if (dca is null)
			return null;

		return typeof(DistributedCacheAccessor).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dca) as TDistributedCache;
	}

	static TBackplane? GetBackplane<TBackplane>(IFusionCache cache)
		where TBackplane : class, IFusionCacheBackplane
	{
		var bpa = typeof(FusionCache).GetField("_bpa", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as BackplaneAccessor;
		if (bpa is null)
			return null;

		return typeof(BackplaneAccessor).GetField("_backplane", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bpa) as TBackplane;
	}

	static RedisBackplaneOptions? GetRedisBackplaneOptions(IFusionCache cache)
	{
		var backplane = GetBackplane<RedisBackplane>(cache);
		if (backplane is null)
			return null;

		return typeof(RedisBackplane).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(backplane) as RedisBackplaneOptions; ;
	}

	static IFusionCachePlugin[]? GetPlugins(IFusionCache cache)
	{
		return (typeof(FusionCache).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as List<IFusionCachePlugin>)?.ToArray();
	}

	[Fact]
	public void CanUseDependencyInjection()
	{
		var services = new ServiceCollection();

		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.NotNull(cache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, cache.CacheName);
	}

	[Fact]
	public void EmptyBuilderDoesNotUseExtraComponents()
	{
		var services = new ServiceCollection();

		services.AddDistributedMemoryCache();
		services.AddFusionCacheSystemTextJsonSerializer();
		services.AddFusionCacheMemoryBackplane();

		services.AddFusionCache("Foo");
		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("Foo");
		var defaultCache = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.NotNull(fooCache);
		Assert.Equal("Foo", fooCache.CacheName);
		Assert.False(fooCache.HasDistributedCache);
		Assert.False(fooCache.HasBackplane);

		Assert.NotNull(defaultCache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, defaultCache.CacheName);
		Assert.False(defaultCache.HasDistributedCache);
		Assert.False(defaultCache.HasBackplane);
	}

	[Fact]
	public void CanConfigureVariousOptions()
	{
		var services = new ServiceCollection();

		var options = new FusionCacheOptions
		{
			AutoRecoveryMaxItems = 123,
		};

		services.AddFusionCache()
			.WithOptions(options)
			.WithOptions(opt =>
			{
				opt.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromSeconds(123);
			})
			.WithDefaultEntryOptions(opt =>
			{
				opt.Duration = TimeSpan.FromMinutes(123);
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.NotNull(cache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, cache.CacheName);
		Assert.Equal(123, options.AutoRecoveryMaxItems);
		Assert.Equal(TimeSpan.FromSeconds(123), cache.DefaultEntryOptions.DistributedCacheDuration!.Value);
		Assert.Equal(TimeSpan.FromMinutes(123), cache.DefaultEntryOptions.Duration);
	}

	[Fact]
	public void CannotSpecifyCacheNameOfDefaultCacheViaOptions()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.WithOptions(opt =>
			{
				opt.CacheName = "foo";
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
		{
			var cache = serviceProvider.GetRequiredService<IFusionCache>();
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();
		});
	}

	[Fact]
	public void CannotSpecifyCacheNameOfNamedCacheViaOptions()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("foo")
			.WithOptions(opt =>
			{
				opt.CacheName = "bar";
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		Assert.Throws<InvalidOperationException>(() =>
		{
			var cache = cacheProvider.GetCache("foo");
		});
	}

	[Fact]
	public void CanAddPlugins()
	{
		var services = new ServiceCollection();
		services.AddTransient<IFusionCachePlugin>(sp => new SimplePlugin("P_1"));

		services.AddFusionCache()
			.WithAllRegisteredPlugins()
			.WithPlugin(new SimplePlugin("P_2"))
			.WithPlugin(sp => new SimplePlugin("P_3"))
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		static List<TPlugin> GetAllPlugins<TPlugin>(IFusionCache cache)
			where TPlugin : IFusionCachePlugin
		{
			return (typeof(FusionCache).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as List<IFusionCachePlugin>)!.Cast<TPlugin>().ToList();
		}

		var allPlugins = GetAllPlugins<SimplePlugin>(cache);

		Assert.NotNull(cache);
		Assert.NotNull(allPlugins);
		Assert.Equal(3, allPlugins.Count);
		Assert.NotNull(allPlugins.Single(p => p.Name == "P_1"));
		Assert.NotNull(allPlugins.Single(p => p.Name == "P_2"));
		Assert.NotNull(allPlugins.Single(p => p.Name == "P_3"));
	}

	[Fact]
	public void CanUseRegisteredMemoryLocker()
	{
		var services = new ServiceCollection();
		services.AddTransient<IFusionCacheMemoryLocker>(sp => new SimpleMemoryLocker());

		services.AddFusionCache()
			.WithRegisteredMemoryLocker()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		static IFusionCacheMemoryLocker GetMemoryLocker(IFusionCache cache)
		{
			return (IFusionCacheMemoryLocker)(typeof(FusionCache).GetField("_memoryLocker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!);
		}

		var memoryLocker = GetMemoryLocker(cache);

		Assert.NotNull(cache);
		Assert.IsType<SimpleMemoryLocker>(memoryLocker);
	}

	[Fact]
	public void CanThrowWithoutRegisteredMemoryLocker()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.WithRegisteredMemoryLocker()
		;

		using var serviceProvider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
		{
			_ = serviceProvider.GetRequiredService<IFusionCache>();
		});
	}

	[Fact]
	public void CanUseCustomMemoryLocker()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.WithMemoryLocker(new SimpleMemoryLocker())
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		static IFusionCacheMemoryLocker GetMemoryLocker(IFusionCache cache)
		{
			return (IFusionCacheMemoryLocker)(typeof(FusionCache).GetField("_memoryLocker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!);
		}

		var memoryLocker = GetMemoryLocker(cache);

		Assert.NotNull(cache);
		Assert.IsType<SimpleMemoryLocker>(memoryLocker);
	}

	[Fact]
	public void UsesStandardMemoryLockerByDefault()
	{
		var services = new ServiceCollection();

		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		static IFusionCacheMemoryLocker GetMemoryLocker(IFusionCache cache)
		{
			return (IFusionCacheMemoryLocker)(typeof(FusionCache).GetField("_memoryLocker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!);
		}

		var memoryLocker = GetMemoryLocker(cache);

		Assert.NotNull(cache);
		Assert.IsType<StandardMemoryLocker>(memoryLocker);
	}

	[Fact]
	public void TryAutoSetupWorks()
	{
		var services = new ServiceCollection();

		services.AddDistributedMemoryCache();
		services.AddFusionCacheSystemTextJsonSerializer();
		services.AddFusionCacheMemoryBackplane();

		services.AddFusionCache()
			.TryWithAutoSetup(false)
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.NotNull(cache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, cache.CacheName);
		Assert.True(cache.HasDistributedCache);
		Assert.True(cache.HasBackplane);
	}

	[Fact]
	public void ThrowsIfMissingRegisteredLogger()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.WithRegisteredLogger()
		;

		using var serviceProvider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
		{
			_ = serviceProvider.GetService<IFusionCache>();
		});
	}

	[Fact]
	public void DontThrowIfMissingRegisteredLogger()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.TryWithRegisteredLogger()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetService<IFusionCache>();

		Assert.NotNull(cache);
	}

	[Fact]
	public void ThrowsIfMissingRegisteredDistributedCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.WithRegisteredDistributedCache()
		;

		using var serviceProvider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
		{
			var cache = serviceProvider.GetRequiredService<IFusionCache>();
		});
	}

	[Fact]
	public void DontThrowIfMissingRegisteredDistributedCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache()
			.TryWithRegisteredDistributedCache()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetService<IFusionCache>();

		Assert.NotNull(cache);
		Assert.False(cache.HasDistributedCache);
	}

	[Fact]
	public void ThrowsIfMissingSerializerWhenUsingDistributedCache()
	{
		var services = new ServiceCollection();

		services.AddDistributedMemoryCache();

		services.AddFusionCache()
			.WithRegisteredDistributedCache(false)
		;

		using var serviceProvider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
		{
			_ = serviceProvider.GetService<IFusionCache>();
		});
	}

	[Fact]
	public void CanUseMultipleNamedCachesAndConfigureThem()
	{
		var services = new ServiceCollection();

		services.AddDistributedMemoryCache();
		services.AddFusionCacheNewtonsoftJsonSerializer();

		// FOO: 10 MIN DURATION + FAIL-SAFE
		services.Configure<FusionCacheOptions>("FooCache", opt =>
		{
			opt.BackplaneChannelPrefix = "AAA";
		});

		services.AddFusionCache("FooCache")
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromMinutes(10))
				.SetFailSafe(true)
			)
		;

		// BAR: 42 SEC DURATION + 3 SEC SOFT TIMEOUT + DIST CACHE
		services.AddFusionCache("BarCache")
			.WithOptions(opt =>
			{
				opt.BackplaneChannelPrefix = "BBB";
			})
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromSeconds(42))
				.SetFactoryTimeouts(TimeSpan.FromSeconds(3))
			)
			.WithRegisteredDistributedCache(false)
		;

		// BAZ: 3 HOURS DURATION + FAIL-SAFE + BACKPLANE (POST-SETUP)
		services.AddFusionCache("BazCache")
			.WithOptions(opt =>
			{
				opt.BackplaneChannelPrefix = "CCC";
			})
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromHours(3))
				.SetFailSafe(true)
			)
			.WithPostSetup((sp, c) =>
			{
				c.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			})
		;

		// QUX (CUSTOM INSTANCE): 1 SEC DURATION + 123 DAYS DIST DURATION
		var quxCacheOriginal = new FusionCache(new FusionCacheOptions()
		{
			CacheName = "QuxCache",
			DefaultEntryOptions = new FusionCacheEntryOptions()
				.SetDuration(TimeSpan.FromSeconds(1))
				.SetDistributedCacheDuration(TimeSpan.FromDays(123))
		});
		services.AddFusionCache(quxCacheOriginal);

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var fooCache = cacheProvider.GetCache("FooCache");
		var barCache = cacheProvider.GetCache("BarCache");
		var bazCache = cacheProvider.GetCache("BazCache");
		var quxCache = cacheProvider.GetCache("QuxCache");

		static FusionCacheOptions GetOptions(IFusionCache cache)
		{
			return (FusionCacheOptions)(typeof(FusionCache).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!);
		}

		var fooOptions = GetOptions(fooCache);
		var barOptions = GetOptions(barCache);
		var bazOptions = GetOptions(bazCache);

		Assert.NotNull(fooCache);
		Assert.Equal("FooCache", fooCache.CacheName);
		Assert.Equal(TimeSpan.FromMinutes(10), fooCache.DefaultEntryOptions.Duration);
		Assert.True(fooCache.DefaultEntryOptions.IsFailSafeEnabled);
		Assert.Null(fooCache.DefaultEntryOptions.DistributedCacheDuration);
		Assert.False(fooCache.HasDistributedCache);
		Assert.False(fooCache.HasBackplane);
		Assert.Equal("AAA", fooOptions.BackplaneChannelPrefix);

		Assert.NotNull(barCache);
		Assert.Equal("BarCache", barCache.CacheName);
		Assert.Equal(TimeSpan.FromSeconds(42), barCache.DefaultEntryOptions.Duration);
		Assert.Equal(TimeSpan.FromSeconds(3), barCache.DefaultEntryOptions.FactorySoftTimeout);
		Assert.Null(barCache.DefaultEntryOptions.DistributedCacheDuration);
		Assert.False(barCache.DefaultEntryOptions.IsFailSafeEnabled);
		Assert.True(barCache.HasDistributedCache);
		Assert.False(barCache.HasBackplane);
		Assert.Equal("BBB", barOptions.BackplaneChannelPrefix);

		Assert.NotNull(bazCache);
		Assert.Equal("BazCache", bazCache.CacheName);
		Assert.Equal(TimeSpan.FromHours(3), bazCache.DefaultEntryOptions.Duration);
		Assert.Null(bazCache.DefaultEntryOptions.DistributedCacheDuration);
		Assert.True(bazCache.DefaultEntryOptions.IsFailSafeEnabled);
		Assert.False(bazCache.HasDistributedCache);
		Assert.True(bazCache.HasBackplane);
		Assert.Equal("CCC", bazOptions.BackplaneChannelPrefix);

		Assert.NotNull(quxCache);
		Assert.Equal("QuxCache", quxCache.CacheName);
		Assert.Equal(quxCacheOriginal, quxCache);
		Assert.Equal(TimeSpan.FromSeconds(1), quxCache.DefaultEntryOptions.Duration);
		Assert.Equal(TimeSpan.FromDays(123), quxCache.DefaultEntryOptions.DistributedCacheDuration);
		Assert.True(bazCache.DefaultEntryOptions.IsFailSafeEnabled);
		Assert.False(bazCache.HasDistributedCache);
		Assert.True(bazCache.HasBackplane);
	}

	[Fact]
	public void CanUseDefaultCacheWithMultipleNamedCaches()
	{
		var services = new ServiceCollection();

		services.AddFusionCache().TryWithAutoSetup();
		services.AddFusionCache("FooCache").TryWithAutoSetup();
		services.AddFusionCache("BarCache").TryWithAutoSetup();
		services.AddFusionCache("BazCache").TryWithAutoSetup();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var fooCache = cacheProvider.GetCache("FooCache");
		var barCache = cacheProvider.GetCache("BarCache");
		var bazCache = cacheProvider.GetCache("BazCache");
		var defaultCache = cacheProvider.GetDefaultCache();

		Assert.NotNull(fooCache);
		Assert.Equal("FooCache", fooCache.CacheName);

		Assert.NotNull(barCache);
		Assert.Equal("BarCache", barCache.CacheName);

		Assert.NotNull(bazCache);
		Assert.Equal("BazCache", bazCache.CacheName);

		Assert.NotNull(defaultCache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, defaultCache.CacheName);
	}

	[Fact]
	public void CanUsePostSetupActions()
	{
		var services = new ServiceCollection();

		var entryOptions = new FusionCacheEntryOptions()
			.SetDuration(TimeSpan.FromMinutes(1))
		;

		services.AddFusionCache()
			.WithDefaultEntryOptions(entryOptions)
			.WithPostSetup((sp, c) =>
			{
				c.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(123);
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var cache = cacheProvider.GetDefaultCache();

		Assert.NotNull(cache);
		Assert.Equal(TimeSpan.FromMinutes(123), cache.DefaultEntryOptions.Duration);
	}

	[Fact]
	public void CanResetPostSetupActions()
	{
		var services = new ServiceCollection();

		var entryOptions = new FusionCacheEntryOptions()
			.SetDuration(TimeSpan.FromMinutes(1))
		;

		services.AddFusionCache()
			.WithDefaultEntryOptions(entryOptions)
			.WithPostSetup((sp, c) =>
			{
				c.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(123);
			})
			.WithPostSetup((sp, c) =>
			{
				c.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(456);
			})
			.WithoutPostSetup()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var cache = cacheProvider.GetDefaultCache();

		Assert.NotNull(cache);
		Assert.Equal(TimeSpan.FromMinutes(1), cache.DefaultEntryOptions.Duration);
	}

	[Fact]
	public void DontThrowWhenRequestingAnUnregisteredCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("FooCache");
		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		Assert.Null(cacheProvider.GetCacheOrNull("BarCache"));
	}

	[Fact]
	public void DefaultCacheIsTheSameWhenRequestedInDifferentWays()
	{
		var services = new ServiceCollection();

		services.AddFusionCache();
		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		Assert.Equal(cacheProvider.GetDefaultCache(), serviceProvider.GetService<IFusionCache>());
	}

	[Fact]
	public void ThrowsOrNotWhenRequestingUnregisteredNamedCaches()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo");
		services.AddFusionCache("Foo");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		Assert.Throws<InvalidOperationException>(() =>
		{
			// MULTIPLE Foo CACHES REGISTERED -> THROWS
			_ = cacheProvider.GetCache("Foo");
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			// MULTIPLE Foo CACHES REGISTERED -> THROWS
			_ = cacheProvider.GetCacheOrNull("Foo");
		});

		Assert.Throws<InvalidOperationException>(() =>
		{
			// NO Bar CACHE REGISTERED -> THROWS
			_ = cacheProvider.GetCache("Bar");
		});

		// NO Bar CACHE REGISTERED -> RETURNS NULL
		var maybeBarCache = cacheProvider.GetCacheOrNull("Bar");

		Assert.Null(maybeBarCache);
	}

	[Fact]
	public void ThrowsOrNotWhenRequestingUnregisteredDefaultCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		Assert.Throws<InvalidOperationException>(() =>
		{
			// NO DEFAULT CACHE REGISTERED -> THROWS
			_ = cacheProvider.GetDefaultCache();
		});

		// NO DEFAULT CACHE REGISTERED -> RETURNS NULL
		var maybeDefaultCache = cacheProvider.GetDefaultCacheOrNull();

		Assert.Null(maybeDefaultCache);
	}

	[Fact]
	public void CacheInstancesAreAlwaysTheSame()
	{
		var services = new ServiceCollection();

		services.AddFusionCache();
		services.AddFusionCache("Foo");
		services.AddFusionCache("Bar");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var defaultCache1 = cacheProvider.GetDefaultCache();
		var defaultCache2 = cacheProvider.GetDefaultCache();

		var fooCache1 = cacheProvider.GetCache("Foo");
		var fooCache2 = cacheProvider.GetCache("Foo");

		var barCache1 = cacheProvider.GetCache("Bar");
		var barCache2 = cacheProvider.GetCache("Bar");

		Assert.Same(defaultCache1, defaultCache2);
		Assert.Same(fooCache1, fooCache2);
		Assert.Same(barCache1, barCache2);
	}

	[Fact]
	public void DifferentNamedCachesDoNotShareTheSameMemoryCacheByDefault()
	{
		var services = new ServiceCollection();

		services.AddMemoryCache();

		// DEFAULT
		services.AddFusionCache();

		// FOO
		services.AddFusionCache("FooCache");

		// BAR
		services.AddFusionCache("BarCache");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var defaultCache = cacheProvider.GetDefaultCache();
		var fooCache = cacheProvider.GetCache("FooCache");
		var barCache = cacheProvider.GetCache("BarCache");

		var defaultCacheValue = defaultCache.GetOrSet("sloth", 1);
		var fooCacheValue = fooCache.GetOrSet("sloth", 2);
		var barCacheValue = barCache.GetOrSet("sloth", 3);

		Assert.Equal(1, defaultCacheValue);
		Assert.Equal(2, fooCacheValue);
		Assert.Equal(3, barCacheValue);
	}

	[Fact]
	public void DifferentNamedCachesCanShareTheSameMemoryCacheWithCollisions()
	{
		var services = new ServiceCollection();

		services.AddMemoryCache();

		// DEFAULT
		services.AddFusionCache()
			.WithRegisteredMemoryCache()
		;

		// FOO
		services.AddFusionCache("FooCache")
			.WithRegisteredMemoryCache()
		;

		// BAR
		services.AddFusionCache("BarCache")
			.WithRegisteredMemoryCache()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var defaultCache = cacheProvider.GetDefaultCache();
		var fooCache = cacheProvider.GetCache("FooCache");
		var barCache = cacheProvider.GetCache("BarCache");

		var defaultCacheValue = defaultCache.GetOrSet("sloth", 1);
		var fooCacheValue = fooCache.GetOrSet("sloth", 2);
		var barCacheValue = barCache.GetOrSet("sloth", 3);

		Assert.Equal(1, defaultCacheValue);
		Assert.Equal(1, fooCacheValue);
		Assert.Equal(1, barCacheValue);
	}

	[Fact]
	public void DifferentNamedCachesCanShareTheSameMemoryCacheWithoutCollisions()
	{
		var services = new ServiceCollection();

		services.AddMemoryCache();

		// DEFAULT
		services.AddFusionCache()
			.WithRegisteredMemoryCache()
		;

		// FOO
		services.AddFusionCache("FooCache")
			.WithRegisteredMemoryCache().WithCacheKeyPrefix()
		;

		// BAR
		services.AddFusionCache("BarCache")
			.WithRegisteredMemoryCache().WithCacheKeyPrefix()
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

		var defaultCache = cacheProvider.GetDefaultCache();
		var fooCache = cacheProvider.GetCache("FooCache");
		var barCache = cacheProvider.GetCache("BarCache");

		var defaultCacheValue = defaultCache.GetOrSet("sloth", 1);
		var fooCacheValue = fooCache.GetOrSet("sloth", 2);
		var barCacheValue = barCache.GetOrSet("sloth", 3);

		Assert.Equal(1, defaultCacheValue);
		Assert.Equal(2, fooCacheValue);
		Assert.Equal(3, barCacheValue);
	}

	[Fact]
	public void BuilderWithSpecificComponentsWorks()
	{
		var services = new ServiceCollection();

		services.AddLogging();

		// FOO: EXTERNAL (NAMED) OPTIONS + DISTRIBUTED CACHE (MEMORY, DIRECT) + SERIALIZER (FACTORY) + BACKPLANE (REDIS)
		services.Configure<RedisBackplaneOptions>("Foo", opt =>
		{
			opt.Configuration = "CONN_FOO";
		});

		services.AddFusionCache("Foo")
			.WithSerializer(sp => new FusionCacheSystemTextJsonSerializer())
			.WithDistributedCache(
				new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
			)
			.WithStackExchangeRedisBackplane()
		;

		// BAR: PLAIN
		services.AddFusionCache("Bar");

		// BAZ: DISTRIBUTED CACHE (MEMORY, VIA FACTORY) + BACKPLANE (MEMORY)
		services.AddFusionCache("Baz")
			.WithSystemTextJsonSerializer(new JsonSerializerOptions()
			{
				IncludeFields = false
			})
			.WithDistributedCache(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<MemoryDistributedCacheOptions>>()?.Get("Baz") ?? new MemoryDistributedCacheOptions();
				var loggerFactory = sp.GetService<ILoggerFactory>();

				if (loggerFactory is null)
					return new MemoryDistributedCache(Options.Create(options));

				return new MemoryDistributedCache(Options.Create(options), loggerFactory);
			})
			.WithMemoryBackplane()
		;

		// DEFAULT: BACKPLANE (REDIS) VIA DIRECT INSTANCE
		services.AddFusionCache()
			.WithBackplane(new RedisBackplane(new RedisBackplaneOptions
			{
				Configuration = "CONN_DEFAULT"
			}))
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("Foo");
		var barCache = cacheProvider.GetCache("Bar");
		var bazCache = cacheProvider.GetCache("Baz");
		var defaultCache = serviceProvider.GetRequiredService<IFusionCache>();

		var fooDistributedCache = GetDistributedCache<MemoryDistributedCache>(fooCache);
		var bazDistributedCache = GetDistributedCache<MemoryDistributedCache>(bazCache);

		var fooBackplane = GetBackplane<RedisBackplane>(fooCache);
		var fooBackplaneOptions = GetRedisBackplaneOptions(fooCache)!;
		var barBackplane = GetBackplane<IFusionCacheBackplane>(barCache);
		var bazBackplane = GetBackplane<MemoryBackplane>(bazCache);
		var defaultBackplane = GetBackplane<RedisBackplane>(defaultCache);
		var defaultBackplaneOptions = GetRedisBackplaneOptions(defaultCache)!;

		Assert.NotNull(fooCache);
		Assert.Equal("Foo", fooCache.CacheName);
		Assert.True(fooCache.HasDistributedCache);
		Assert.NotNull(fooDistributedCache);
		Assert.True(fooCache.HasBackplane);
		Assert.NotNull(fooBackplane);
		Assert.Equal("CONN_FOO", fooBackplaneOptions.Configuration);

		Assert.NotNull(barCache);
		Assert.Equal("Bar", barCache.CacheName);
		Assert.False(barCache.HasDistributedCache);
		Assert.False(barCache.HasBackplane);
		Assert.Null(barBackplane);

		Assert.NotNull(bazCache);
		Assert.Equal("Baz", bazCache.CacheName);
		Assert.True(bazCache.HasDistributedCache);
		Assert.NotNull(bazDistributedCache);
		Assert.True(bazCache.HasBackplane);
		Assert.NotNull(bazBackplane);

		Assert.NotNull(defaultCache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, defaultCache.CacheName);
		Assert.False(defaultCache.HasDistributedCache);
		Assert.True(defaultCache.HasBackplane);
		Assert.NotNull(defaultBackplane);
		Assert.Equal("CONN_DEFAULT", defaultBackplaneOptions.Configuration);
	}

	[Fact]
	public void CanDoWithoutLogger()
	{
		var services = new ServiceCollection();

		services.AddLogging();

		services.AddFusionCache()
			.WithoutLogger()
		;

		using var serviceProvider = services.BuildServiceProvider();
		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.Null(GetLogger(cache));
	}

	[Fact]
	public void CanActAsKeyedService()
	{
		var services = new ServiceCollection();

		services.AddDistributedMemoryCache();
		services.AddFusionCacheNewtonsoftJsonSerializer();

		// FOO: 10 MIN DURATION + FAIL-SAFE
		services.Configure<FusionCacheOptions>("FooCache", opt =>
		{
			opt.BackplaneChannelPrefix = "AAA";
		});

		services.AddFusionCache("FooCache")
			.AsKeyedService()
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromMinutes(10))
				.SetFailSafe(true)
			)
		;

		// BAR: 42 SEC DURATION + 3 SEC SOFT TIMEOUT + DIST CACHE
		services.AddFusionCache("BarCache")
			.AsKeyedService()
			.WithOptions(opt =>
			{
				opt.BackplaneChannelPrefix = "BBB";
			})
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromSeconds(42))
				.SetFactoryTimeouts(TimeSpan.FromSeconds(3))
			)
			.WithRegisteredDistributedCache(false)
		;

		// BAZ: 3 HOURS DURATION + FAIL-SAFE + BACKPLANE (POST-SETUP)
		services.AddFusionCache("BazCache")
			.AsKeyedService()
			.WithOptions(opt =>
			{
				opt.BackplaneChannelPrefix = "CCC";
			})
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromHours(3))
				.SetFailSafe(true)
			)
			.WithPostSetup((sp, c) =>
			{
				c.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
			})
		;

		// QUX (CUSTOM INSTANCE): 1 SEC DURATION + 123 DAYS DIST DURATION
		var quxCacheOriginal = new FusionCache(new FusionCacheOptions()
		{
			CacheName = "QuxCache",
			DefaultEntryOptions = new FusionCacheEntryOptions()
				.SetDuration(TimeSpan.FromSeconds(1))
				.SetDistributedCacheDuration(TimeSpan.FromDays(123))
		});
		services.AddFusionCache(quxCacheOriginal, true);

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("FooCache");
		var fooCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("FooCache");

		var barCache = cacheProvider.GetCache("BarCache");
		var barCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("BarCache");

		var bazCache = cacheProvider.GetCache("BazCache");
		var bazCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("BazCache");

		var quxCache = cacheProvider.GetCache("QuxCache");
		var quxCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("QuxCache");

		Assert.NotNull(fooCache);
		Assert.Equal(fooCache, fooCache2);

		Assert.NotNull(barCache);
		Assert.Equal(barCache, barCache2);

		Assert.NotNull(bazCache);
		Assert.Equal(bazCache, bazCache2);

		Assert.NotNull(quxCache);
		Assert.Equal(quxCacheOriginal, quxCache);
		Assert.Equal(quxCache, quxCache2);
	}

	[Fact]
	public void CanConsumeKeyedServices()
	{
		var services = new ServiceCollection();

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredLogger = new ListLogger<FusionCache>();
		services.AddKeyedSingleton<ILogger<FusionCache>>("FooLogger", registeredLogger);

		var registeredMemoryCache = new ChaosMemoryCache(new MemoryCache(new MemoryCacheOptions()));
		services.AddKeyedSingleton<IMemoryCache>("FooMemoryCache", registeredMemoryCache);

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredSerializer = new ChaosSerializer(new FusionCacheSystemTextJsonSerializer());
		services.AddKeyedSingleton<IFusionCacheSerializer>("FooSerializer", registeredSerializer);

		var registeredDistributedCache = new ChaosDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
		services.AddKeyedSingleton<IDistributedCache>("FooDistributedCache", registeredDistributedCache);

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredMemoryLocker = new ChaosMemoryLocker(new StandardMemoryLocker());
		services.AddKeyedSingleton<IFusionCacheMemoryLocker>("FooMemoryLocker", registeredMemoryLocker);

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredBackplane = new ChaosBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
		services.AddKeyedSingleton<IFusionCacheBackplane>("FooBackplane", registeredBackplane);

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		IFusionCachePlugin[] registeredKeyedPlugins = [
			new SimplePlugin("KP1"),
			new SimplePlugin("KP2"),
			new SimplePlugin("KP3")
		];
		foreach (var plugin in registeredKeyedPlugins)
		{
			services.AddKeyedSingleton<IFusionCachePlugin>("FooPlugins", plugin);
		}
		IFusionCachePlugin[] registeredNonKeyedPlugins = [
			new SimplePlugin("NKP1"),
			new SimplePlugin("NKP2"),
			new SimplePlugin("NKP3")
		];
		foreach (var plugin in registeredNonKeyedPlugins)
		{
			services.AddSingleton<IFusionCachePlugin>(plugin);
		}

		services.AddFusionCache()
			.WithRegisteredKeyedLogger("FooLogger")
			.WithRegisteredKeyedMemoryCache("FooMemoryCache")
			.WithRegisteredKeyedSerializer("FooSerializer")
			.WithRegisteredKeyedDistributedCache("FooDistributedCache")
			.WithRegisteredKeyedMemoryLocker("FooMemoryLocker")
			.WithRegisteredKeyedBackplane("FooBackplane")
			.WithAllRegisteredPlugins()
			.WithAllRegisteredKeyedPlugins("FooPlugins");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var logger = GetLogger(cache);
		var memoryCache = GetMemoryCache(cache);
		var serializer = GetSerializer(cache);
		var distributedCache = GetDistributedCache<IDistributedCache>(cache);
		var memoryLocker = GetMemoryLocker(cache);
		var backplane = GetBackplane<IFusionCacheBackplane>(cache);
		var plugins = GetPlugins(cache);

		Assert.NotNull(cache);

		Assert.NotNull(logger);
		Assert.Equal(registeredLogger, logger);

		Assert.NotNull(memoryCache);
		Assert.Equal(registeredMemoryCache, memoryCache);

		Assert.NotNull(serializer);
		Assert.Equal(registeredSerializer, serializer);

		Assert.NotNull(distributedCache);
		Assert.Equal(registeredDistributedCache, distributedCache);

		Assert.NotNull(memoryLocker);
		Assert.Equal(registeredMemoryLocker, memoryLocker);

		Assert.NotNull(backplane);
		Assert.Equal(registeredBackplane, backplane);

		foreach (var plugin in registeredKeyedPlugins)
		{
			Assert.Contains(plugin, plugins!);
		}

		foreach (var plugin in registeredNonKeyedPlugins)
		{
			Assert.Contains(plugin, plugins!);
		}
	}

	[Fact]
	public void CanUseNamedCachesWithoutDefaultCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo");
		services.AddFusionCache("Bar");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("Foo");
		var barCache = cacheProvider.GetCache("Bar");

		Assert.NotNull(fooCache);
		Assert.NotNull(barCache);
	}
}
