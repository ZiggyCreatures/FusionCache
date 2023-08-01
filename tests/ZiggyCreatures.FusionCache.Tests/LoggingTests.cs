using System;
using System.Linq;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.NullObjects;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace FusionCacheTests
{
	public class LoggingTests
	{
		private ListLogger<FusionCache> CreateListLogger(LogLevel minLogLevel)
		{
			return new ListLogger<FusionCache>(minLogLevel);
		}

		[Fact]
		public async Task CommonLogLevelsWork()
		{
			var logger = CreateListLogger(LogLevel.Debug);
			using (var cache = new FusionCache(new FusionCacheOptions(), logger: logger))
			{
				cache.AddPlugin(new NullPlugin());

				cache.TryGet<int>("foo");
				cache.TryGet<int>("bar");
				cache.Set<int>("foo", 123);
				cache.TryGet<int>("foo");
				cache.GetOrSet<int>("qux", _ => throw new Exception("Sloths!"), 123, opt => opt.SetFailSafe(true));
			}

			Assert.Equal(22, logger.Items.Count);
			Assert.Equal(2, logger.Items.Count(x => x.LogLevel == LogLevel.Warning));
			Assert.Equal(2, logger.Items.Count(x => x.LogLevel == LogLevel.Information));
		}

		[Fact]
		public async Task PluginsInfoWork()
		{
			var logger = CreateListLogger(LogLevel.Information);
			var options = new FusionCacheOptions();
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.AddPlugin(new NullPlugin());
			}

			Assert.Equal(2, logger.Items.Count);

			logger = CreateListLogger(LogLevel.Information);
			options = new FusionCacheOptions()
			{
				PluginsInfoLogLevel = LogLevel.Debug
			};
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.AddPlugin(new NullPlugin());
			}

			Assert.Empty(logger.Items);
		}

		[Fact]
		public async Task EventsErrorsLogLevelsWork()
		{
			var logger = CreateListLogger(LogLevel.Information);
			var options = new FusionCacheOptions
			{
				EnableSyncEventHandlersExecution = true
			};
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.Events.FactorySuccess += (sender, e) => throw new Exception("Sloths!");
				cache.GetOrSet<int>("qux", _ => 123);
			}

			Assert.Single(logger.Items);
			Assert.Single(logger.Items.Where(x => x.LogLevel == LogLevel.Warning));

			logger = CreateListLogger(LogLevel.Information);
			options = new FusionCacheOptions
			{
				EnableSyncEventHandlersExecution = true,
				EventHandlingErrorsLogLevel = LogLevel.Debug
			};
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.Events.FactorySuccess += (sender, e) => throw new Exception("Sloths!");
				cache.GetOrSet<int>("qux", _ => 123);
			}

			Assert.Empty(logger.Items);
		}

		[Fact]
		public async Task CacheNameIsAlwaysThere()
		{
			var cacheName = Guid.NewGuid().ToString("N");
			var logger = CreateListLogger(LogLevel.Trace);
			var options = new FusionCacheOptions
			{
				CacheName = cacheName,
				EnableSyncEventHandlersExecution = true
			};
			using (var cache = new FusionCache(options, logger: logger))
			{
				// PLUGINS
				cache.AddPlugin(new NullPlugin());

				// DISTRIBUTED CACHE
				cache.SetupDistributedCache(
					new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
					new FusionCacheNewtonsoftJsonSerializer()
				);

				// BACKPLANE
				cache.SetupBackplane(
					new MemoryBackplane(new MemoryBackplaneOptions())
				);

				// BASIC OPERATIONS
				cache.Set<int>("foo", 123);
				var foo = cache.GetOrDefault<int>("foo");
				var maybeFoo = cache.TryGet<int>("foo");
				cache.Remove("foo");
			}

			var itemsCountWithoutCacheName = logger.Items.Count(x => x.Message.Contains(cacheName) == false);

			Assert.True(logger.Items.Count > 0);
			Assert.Equal(0, itemsCountWithoutCacheName);
		}
	}
}
