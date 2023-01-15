using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;

namespace FusionCacheTests
{
	public class DependencyInjectionTests
	{
		[Fact]
		public void CanUseDependencyInjection()
		{
			var services = new ServiceCollection();

			services.AddFusionCache(b => b);

			using var serviceProvider = services.BuildServiceProvider();

			var cache = serviceProvider.GetRequiredService<IFusionCache>();

			Assert.NotNull(cache);
		}

		[Fact]
		public void EmptyBuilderDoesNotUseExtraComponents()
		{
			var services = new ServiceCollection();

			services.AddDistributedMemoryCache();
			services.AddFusionCacheSystemTextJsonSerializer();
			services.AddFusionCacheMemoryBackplane();

			services.AddFusionCache(b => b);

			using var serviceProvider = services.BuildServiceProvider();

			var cache = serviceProvider.GetRequiredService<IFusionCache>();

			Assert.NotNull(cache);
			Assert.False(cache.HasDistributedCache);
			Assert.False(cache.HasBackplane);
		}

		[Fact]
		public void CanConfigureVariousOptions()
		{
			var services = new ServiceCollection();

			var options = new FusionCacheOptions
			{
				BackplaneAutoRecoveryMaxItems = 123,
			};

			services.AddFusionCache(b => b
				.WithOptions(options)
				.WithOptions(opt =>
				{
					opt.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromSeconds(123);
				})
				.WithDefaultEntryOptions(opt =>
				{
					opt.Duration = TimeSpan.FromMinutes(123);
				})
			);

			using var serviceProvider = services.BuildServiceProvider();

			var cache = serviceProvider.GetRequiredService<IFusionCache>();

			Assert.NotNull(cache);
			Assert.Equal(123, options.BackplaneAutoRecoveryMaxItems);
			Assert.Equal(TimeSpan.FromSeconds(123), cache.DefaultEntryOptions.DistributedCacheDuration!.Value);
			Assert.Equal(TimeSpan.FromMinutes(123), cache.DefaultEntryOptions.Duration);
		}

		[Fact]
		public void CanAutoSetup()
		{
			var services = new ServiceCollection();

			services.AddDistributedMemoryCache();
			services.AddFusionCacheSystemTextJsonSerializer();
			services.AddFusionCacheMemoryBackplane();

			services.AddFusionCache(b => b
				.WithAutoSetup(false)
			);

			using var serviceProvider = services.BuildServiceProvider();

			var cache = serviceProvider.GetRequiredService<IFusionCache>();

			Assert.NotNull(cache);
			Assert.True(cache.HasDistributedCache);
			Assert.True(cache.HasBackplane);
		}

		[Fact]
		public void ThrowsIfMissingSerializer()
		{
			var services = new ServiceCollection();

			services.AddDistributedMemoryCache();

			services.AddFusionCache(b => b
				.WithAutoSetup(false)
			);

			using var serviceProvider = services.BuildServiceProvider();

			Assert.Throws<InvalidOperationException>(() =>
			{
				_ = serviceProvider.GetService<IFusionCache>();
			});
		}

		[Fact]
		public void CanInitMultipleTimes()
		{
			var services = new ServiceCollection();

			services.AddFusionCache(b => b);
			services.AddFusionCache(b => b);
			services.AddFusionCache(b => b);
			services.AddFusionCache(b => b);

			using var serviceProvider = services.BuildServiceProvider();

			var cache = serviceProvider.GetRequiredService<IFusionCache>();

			Assert.NotNull(cache);
		}

		[Fact]
		public void CanUseMultipleNamedCachesAndConfigureThem()
		{
			var services = new ServiceCollection();

			services.AddDistributedMemoryCache();
			services.AddFusionCacheNewtonsoftJsonSerializer();

			// FOO: 10 MIN DURATION + FAIL-SAFE
			services.AddFusionCache("FooCache", b => b
				.WithDefaultEntryOptions(opt => opt
					.SetDuration(TimeSpan.FromMinutes(10))
					.SetFailSafe(true)
				)
			);

			// BAR: 42 SEC DURATION + 3 SEC SOFT TIMEOUT + DIST CACHE
			services.AddFusionCache("BarCache", b => b
				.WithDefaultEntryOptions(opt => opt
					.SetDuration(TimeSpan.FromSeconds(42))
					.SetFactoryTimeouts(TimeSpan.FromSeconds(3))
				)
				.WithRegisteredDistributedCache(false)
			);

			// BAZ: 3 HOURS DURATION + FAIL-SAFE + BACKPLANE
			services.AddFusionCache("BazCache", b => b
				.WithDefaultEntryOptions(opt => opt
					.SetDuration(TimeSpan.FromHours(3))
					.SetFailSafe(true)
				)
				.WithPostSetup((sp, c) =>
				{
					c.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions()));
				})
			);

			// QUX (CUSTOM INSTANCE): 1 SEC DURATION + 123 DAYS DIST DURATION
			var quxCacheOriginal = new FusionCache(new FusionCacheOptions()
			{
				CacheName = "QuxCache",
				DefaultEntryOptions = new FusionCacheEntryOptions()
					.SetDuration(TimeSpan.FromSeconds(1))
					.SetDistributedCacheDuration(TimeSpan.FromDays(123))
			});
			services.AddFusionCache("QuxCache", quxCacheOriginal);

			using var serviceProvider = services.BuildServiceProvider();

			var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

			var fooCache = cacheProvider.GetCache("FooCache");
			var barCache = cacheProvider.GetCache("BarCache");
			var bazCache = cacheProvider.GetCache("BazCache");
			var quxCache = cacheProvider.GetCache("QuxCache");

			Assert.NotNull(fooCache);
			Assert.Equal("FooCache", fooCache.CacheName);
			Assert.Equal(TimeSpan.FromMinutes(10), fooCache.DefaultEntryOptions.Duration);
			Assert.True(fooCache.DefaultEntryOptions.IsFailSafeEnabled);
			Assert.Null(fooCache.DefaultEntryOptions.DistributedCacheDuration);
			Assert.False(fooCache.HasDistributedCache);
			Assert.False(fooCache.HasBackplane);

			Assert.NotNull(barCache);
			Assert.Equal("BarCache", barCache.CacheName);
			Assert.Equal(TimeSpan.FromSeconds(42), barCache.DefaultEntryOptions.Duration);
			Assert.Equal(TimeSpan.FromSeconds(3), barCache.DefaultEntryOptions.FactorySoftTimeout);
			Assert.Null(barCache.DefaultEntryOptions.DistributedCacheDuration);
			Assert.False(barCache.DefaultEntryOptions.IsFailSafeEnabled);
			Assert.True(barCache.HasDistributedCache);
			Assert.False(barCache.HasBackplane);

			Assert.NotNull(bazCache);
			Assert.Equal("BazCache", bazCache.CacheName);
			Assert.Equal(TimeSpan.FromHours(3), bazCache.DefaultEntryOptions.Duration);
			Assert.Null(bazCache.DefaultEntryOptions.DistributedCacheDuration);
			Assert.True(bazCache.DefaultEntryOptions.IsFailSafeEnabled);
			Assert.False(bazCache.HasDistributedCache);
			Assert.True(bazCache.HasBackplane);

			Assert.NotNull(quxCache);
			Assert.Equal("QuxCache", quxCache.CacheName);
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

			services.AddFusionCache("FooCache", b => b
				.WithAutoSetup()
			);
			services.AddFusionCache("BarCache", b => b
				.WithAutoSetup()
			);
			services.AddFusionCache("BazCache", b => b
				.WithAutoSetup()
			);
			services.AddFusionCache(b => b
				.WithAutoSetup()
			);

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
		public void ThrowsWhenRequestingAnUnregisteredCache()
		{
			var services = new ServiceCollection();

			services.AddFusionCache("FooCache", b => b);
			services.AddFusionCache(b => b);

			using var serviceProvider = services.BuildServiceProvider();

			var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

			Assert.Throws<ArgumentException>(() =>
			{
				cacheProvider.GetCache("BarCache");
			});
		}

		[Fact]
		public void ExistingAndObsoleteCallsStillWork()
		{
			var services = new ServiceCollection();

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache();
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache(
				opt =>
				{
					opt.BackplaneAutoRecoveryMaxItems = 123;
				}
			);
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache(
				opt =>
				{
					opt.BackplaneAutoRecoveryMaxItems = 123;
				},
				false
			);
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache(
				opt =>
				{
					opt.BackplaneAutoRecoveryMaxItems = 123;
				},
				false,
				false
			);
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache(
				useDistributedCacheIfAvailable: false
			);
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
			services.AddFusionCache(
				ignoreMemoryDistributedCache: false
			);
#pragma warning restore CS0618 // Type or member is obsolete


			using var serviceProvider = services.BuildServiceProvider();

			var cacheProvider = serviceProvider.GetService<IFusionCacheProvider>()!;

			Assert.Equal(1, 1);
		}
	}
}
