using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
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
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;
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

		//services.Configure<FusionCacheOptions>(o =>
		//{
		//	o.AutoRecoveryMaxItems = 456;
		//});

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

		var options2 = cache.GetOptions();

		Assert.NotNull(cache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, cache.CacheName);
		Assert.Equal(123, options2.AutoRecoveryMaxItems);
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
	public void CanDirectlyAddANamedCacheInstance()
	{
		var logger = CreateXUnitLogger<FusionCache>();

		var namedCache1 = new FusionCache(
			new FusionCacheOptions()
			{
				CacheName = "foo",
			},
			logger: logger
		);
		var namedCache2 = new FusionCache(
			new FusionCacheOptions()
			{
				CacheName = "bar",
			},
			logger: logger
		);

		var services = new ServiceCollection();

		services.AddSingleton<ILogger<FusionCache>>(logger);

		services.AddFusionCache(namedCache1);
		services.AddFusionCache(namedCache2);
		services.AddFusionCache();
		services.AddFusionCache("baz");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var defaultCache = cacheProvider.GetDefaultCacheOrNull();
		var fooCache = cacheProvider.GetCacheOrNull("foo");
		var barCache = cacheProvider.GetCacheOrNull("bar");
		var bazCache = cacheProvider.GetCacheOrNull("baz");

		Assert.NotNull(defaultCache);
		Assert.Equal(FusionCacheOptions.DefaultCacheName, defaultCache!.CacheName);

		Assert.NotNull(fooCache);
		Assert.Equal("foo", fooCache!.CacheName);
		Assert.Equal(namedCache1, fooCache);

		Assert.NotNull(barCache);
		Assert.Equal("bar", barCache!.CacheName);
		Assert.Equal(namedCache2, barCache);

		Assert.NotNull(bazCache);
		Assert.Equal("baz", bazCache!.CacheName);
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
	public void CanRegisterMultipleDefaultCaches()
	{
		// NOTE: EVEN THOUGH IT'S POSSIBLE TO REGISTER MULTIPLE DEFAULT CACHES, IT'S NOT RECOMMENDED,
		// AND IT'S NOT POSSIBLE TO USE THEM IN A MEANINGFUL WAY, AS THE LAST ONE REGISTERED WILL BE THE ONE USED.
		// THIS FOLLOWS THE STANDARD BEHAVIOR OF MICROSOFT'S DI CONTAINER.

		var logger = CreateXUnitLogger<FusionCache>();

		var services = new ServiceCollection();

		services.AddSingleton<ILogger<FusionCache>>(logger);

		var defaultCache = new FusionCache(new FusionCacheOptions(), logger: logger);

		services.AddFusionCache();
		services.AddFusionCache();
		services.AddSingleton<IFusionCache>(defaultCache);

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var cache1 = cacheProvider.GetDefaultCache();
		var cache2 = cacheProvider.GetDefaultCache();
		var cache3 = serviceProvider.GetRequiredService<IFusionCache>();

		Assert.NotNull(cache1);
		Assert.NotNull(cache2);
		Assert.NotNull(cache3);
		Assert.Equal(defaultCache, cache1);
		Assert.Equal(cache1, cache2);
		Assert.Equal(cache2, cache3);
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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		Assert.Null(cacheProvider.GetCacheOrNull("BarCache"));
	}

	[Fact]
	public void DefaultCacheIsTheSameWhenRequestedInDifferentWays()
	{
		var services = new ServiceCollection();

		services.AddFusionCache();
		services.AddFusionCache();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		Assert.Equal(cacheProvider.GetDefaultCache(), serviceProvider.GetService<IFusionCache>());
	}

	[Fact]
	public void ThrowsOrNotWhenRequestingUnregisteredNamedCaches()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo");
		services.AddFusionCache("Foo");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		Assert.Throws<InvalidOperationException>(() =>
		{
			// NO DEFAULT CACHE REGISTERED -> THROWS
			_ = cacheProvider.GetDefaultCache();
		});

		// NO DEFAULT CACHE REGISTERED -> RETURNS NULL
		var defaultCache = cacheProvider.GetDefaultCacheOrNull();
	}

	[Fact]
	public void ThrowsOrNotWhenRequestingUnregisteredDefaultCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo");

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var defaultCache1 = cacheProvider.GetDefaultCache();
		var defaultCache2 = cacheProvider.GetDefaultCache();
		var defaultCache3 = serviceProvider.GetRequiredService<IFusionCache>();

		var fooCache1 = cacheProvider.GetCache("Foo");
		var fooCache2 = cacheProvider.GetCache("Foo");

		var barCache1 = cacheProvider.GetCache("Bar");
		var barCache2 = cacheProvider.GetCache("Bar");

		Assert.Same(defaultCache1, defaultCache2);
		Assert.Same(defaultCache2, defaultCache3);
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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

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

		var fooDistributedCache = TestsUtils.GetDistributedCache<MemoryDistributedCache>(fooCache);
		var bazDistributedCache = TestsUtils.GetDistributedCache<MemoryDistributedCache>(bazCache);

		var fooBackplane = TestsUtils.GetBackplane<RedisBackplane>(fooCache);
		var fooBackplaneOptions = TestsUtils.GetRedisBackplaneOptions(fooCache)!;
		var barBackplane = TestsUtils.GetBackplane<IFusionCacheBackplane>(barCache);
		var bazBackplane = TestsUtils.GetBackplane<MemoryBackplane>(bazCache);
		var defaultBackplane = TestsUtils.GetBackplane<RedisBackplane>(defaultCache);
		var defaultBackplaneOptions = TestsUtils.GetRedisBackplaneOptions(defaultCache)!;

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

		Assert.Null(TestsUtils.GetLogger(cache));
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
			.AsKeyedServiceByCacheName()
			.WithDefaultEntryOptions(opt => opt
				.SetDuration(TimeSpan.FromMinutes(10))
				.SetFailSafe(true)
			)
		;

		// BAR: 42 SEC DURATION + 3 SEC SOFT TIMEOUT + DIST CACHE
		services.AddFusionCache("BarCache")
			.AsKeyedServiceByCacheName()
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
		var bazServiceKey = new SimpleServiceKey(123);
		services.AddFusionCache("BazCache")
			.AsKeyedService(bazServiceKey)
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

		// USE [FromKeyedService] ATTRIBUTE
		services.AddSingleton<SimpleServiceWithKeyedDependency>();

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("FooCache");
		var fooCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("FooCache");

		var barCache = cacheProvider.GetCache("BarCache");
		var barCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("BarCache");

		var bazCache = cacheProvider.GetCache("BazCache");
		var bazCache1 = serviceProvider.GetKeyedService<IFusionCache>("BazCache");
		var bazCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>(bazServiceKey);
		var bazCache3 = serviceProvider.GetRequiredKeyedService<IFusionCache>(new SimpleServiceKey(123));

		var quxCache = cacheProvider.GetCache("QuxCache");
		var quxCache2 = serviceProvider.GetRequiredKeyedService<IFusionCache>("QuxCache");

		var simpleService = serviceProvider.GetRequiredService<SimpleServiceWithKeyedDependency>();

		Assert.NotNull(fooCache);
		Assert.Equal(fooCache, fooCache2);

		Assert.NotNull(barCache);
		Assert.Equal(barCache, barCache2);

		Assert.NotNull(bazCache);
		Assert.Null(bazCache1);
		Assert.Equal(bazCache, bazCache2);
		Assert.Equal(bazCache, bazCache3);

		Assert.NotNull(quxCache);
		Assert.Equal(quxCacheOriginal, quxCache);
		Assert.Equal(quxCache, quxCache2);

		Assert.NotNull(simpleService);
		Assert.NotNull(simpleService);
	}

	[Fact]
	public void CanUseKeyedLogger()
	{
		var services = new ServiceCollection();

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredLogger = new ListLogger<FusionCache>();
		services.AddKeyedSingleton<ILogger<FusionCache>>("FooLogger", registeredLogger);

		services.AddFusionCache()
			.TryWithRegisteredKeyedLogger("FooLogger");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var logger = TestsUtils.GetLogger(cache);

		Assert.NotNull(cache);

		Assert.NotNull(logger);
		Assert.Equal(registeredLogger, logger);
	}

	[Fact]
	public void CanUseKeyedMemoryCache()
	{
		var services = new ServiceCollection();

		var registeredMemoryCache = new ChaosMemoryCache(new MemoryCache(new MemoryCacheOptions()));
		services.AddKeyedSingleton<IMemoryCache>("FooMemoryCache", registeredMemoryCache);

		services.AddFusionCache()
			.TryWithRegisteredKeyedMemoryCache("FooMemoryCache");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var memoryCache = TestsUtils.GetMemoryCache(cache);

		Assert.NotNull(cache);

		Assert.NotNull(memoryCache);
		Assert.Equal(registeredMemoryCache, memoryCache);
	}

	[Fact]
	public void CanUseKeyedDistributedCache()
	{
		var services = new ServiceCollection();

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredSerializer = new ChaosSerializer(new FusionCacheSystemTextJsonSerializer());
		services.AddKeyedSingleton<IFusionCacheSerializer>("FooSerializer", registeredSerializer);

		var registeredDistributedCache = new ChaosDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
		services.AddKeyedSingleton<IDistributedCache>("FooDistributedCache", registeredDistributedCache);

		services.AddFusionCache()
			.WithRegisteredKeyedSerializer("FooSerializer")
			.TryWithRegisteredKeyedDistributedCache("FooDistributedCache");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var serializer = TestsUtils.GetSerializer(cache);
		var distributedCache = TestsUtils.GetDistributedCache<IDistributedCache>(cache);

		Assert.NotNull(cache);

		Assert.NotNull(serializer);
		Assert.Equal(registeredSerializer, serializer);
	}

	[Fact]
	public void CanUseKeyedMemoryLocker()
	{
		var services = new ServiceCollection();

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredMemoryLocker = new ChaosMemoryLocker(new StandardMemoryLocker());
		services.AddKeyedSingleton<IFusionCacheMemoryLocker>("FooMemoryLocker", registeredMemoryLocker);

		services.AddFusionCache()
			.TryWithRegisteredKeyedMemoryLocker("FooMemoryLocker");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var memoryLocker = TestsUtils.GetMemoryLocker(cache);

		Assert.NotNull(cache);

		Assert.NotNull(memoryLocker);
		Assert.Equal(registeredMemoryLocker, memoryLocker);
	}

	[Fact]
	public void CanUseKeyedBackplane()
	{
		var services = new ServiceCollection();

		// NOTE: THIS SHOULD BE TRANSIENT, NOT SINGLETON: I'M DOING THIS ONLY FOR TESTING PURPOSES
		var registeredBackplane = new ChaosBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
		services.AddKeyedSingleton<IFusionCacheBackplane>("FooBackplane", registeredBackplane);

		services.AddFusionCache()
			.TryWithRegisteredKeyedBackplane("FooBackplane");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var backplane = TestsUtils.GetBackplane<IFusionCacheBackplane>(cache);

		Assert.NotNull(cache);

		Assert.NotNull(backplane);
		Assert.Equal(registeredBackplane, backplane);
	}

	[Fact]
	public void CanUseKeyedPlugins()
	{
		var services = new ServiceCollection();

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
			.WithAllRegisteredPlugins()
			.WithAllRegisteredKeyedPlugins("FooPlugins");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var plugins = TestsUtils.GetPlugins(cache);

		Assert.NotNull(cache);

		foreach (var plugin in registeredKeyedPlugins)
		{
			Assert.Contains(plugin, plugins!);
		}

		foreach (var plugin in registeredNonKeyedPlugins)
		{
			Assert.Contains(plugin, plugins!);
		}
		Assert.Equal(registeredKeyedPlugins.Length + registeredNonKeyedPlugins.Length, plugins!.Count());
	}

	[Fact]
	public void CanUseKeyedServices()
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
			.TryWithRegisteredKeyedLogger("FooLogger")
			.TryWithRegisteredKeyedMemoryCache("FooMemoryCache")
			.TryWithRegisteredKeyedSerializer("FooSerializer")
			.TryWithRegisteredKeyedDistributedCache("FooDistributedCache")
			.TryWithRegisteredKeyedMemoryLocker("FooMemoryLocker")
			.TryWithRegisteredKeyedBackplane("FooBackplane")
			.WithAllRegisteredPlugins()
			.WithAllRegisteredKeyedPlugins("FooPlugins");

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		var logger = TestsUtils.GetLogger(cache);
		var memoryCache = TestsUtils.GetMemoryCache(cache);
		var serializer = TestsUtils.GetSerializer(cache);
		var distributedCache = TestsUtils.GetDistributedCache<IDistributedCache>(cache);
		var memoryLocker = TestsUtils.GetMemoryLocker(cache);
		var backplane = TestsUtils.GetBackplane<IFusionCacheBackplane>(cache);
		var plugins = TestsUtils.GetPlugins(cache);

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
		Assert.Equal(registeredKeyedPlugins.Length + registeredNonKeyedPlugins.Length, plugins!.Count());
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

	[Fact]
	public void CanUseASerializerWithoutADistributedCache()
	{
		var services = new ServiceCollection();

		services.AddFusionCache("Foo")
			.WithDefaultEntryOptions(opt =>
			{
				opt.EnableAutoClone = true;
			})
			.WithSerializer(new FusionCacheSystemTextJsonSerializer());

		services.AddFusionCache("Bar")
			.WithDefaultEntryOptions(opt =>
			{
				opt.EnableAutoClone = true;
			});

		using var serviceProvider = services.BuildServiceProvider();

		var cacheProvider = serviceProvider.GetRequiredService<IFusionCacheProvider>();

		var fooCache = cacheProvider.GetCache("Foo");
		var barCache = cacheProvider.GetCache("Bar");

		fooCache.Set("foo", 123);
		barCache.Set("bar", 456);

		var foo = fooCache.GetOrDefault<int>("foo");
		Assert.Equal(123, foo);

		Assert.Throws<InvalidOperationException>(() =>
		{
			var bar = barCache.GetOrDefault<int>("bar");
		});
	}

	[Fact]
	public void CanUseAsHybridCache()
	{
		var services = new ServiceCollection();

		services
			.AddFusionCache()
			.AsHybridCache()
			.AsKeyedHybridCache("Foo")
		;

		using var serviceProvider = services.BuildServiceProvider();

		var fusionCache = serviceProvider.GetRequiredService<IFusionCache>();
		var hybridCache1 = serviceProvider.GetRequiredService<HybridCache>();
		var hybridCache2 = serviceProvider.GetRequiredKeyedService<HybridCache>("Foo");
		var fusionHybridCache1 = (FusionHybridCache)hybridCache1;
		var fusionHybridCache2 = (FusionHybridCache)hybridCache2;

		Assert.NotNull(fusionCache);
		Assert.NotNull(hybridCache1);
		Assert.NotNull(hybridCache2);
		Assert.NotNull(fusionHybridCache1);
		Assert.NotNull(fusionHybridCache2);
		Assert.IsType<FusionHybridCache>(hybridCache1);
		Assert.IsType<FusionHybridCache>(hybridCache2);
		Assert.NotSame(hybridCache1, hybridCache2);
		Assert.Same(fusionCache, fusionHybridCache1.InnerFusionCache);
		Assert.Same(fusionCache, fusionHybridCache2.InnerFusionCache);
	}
}
