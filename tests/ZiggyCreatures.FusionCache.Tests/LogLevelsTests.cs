﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace FusionCacheTests
{
	public class LogLevelsTests
	{
		private class SamplePlugin
			: IFusionCachePlugin
		{
			public void Start(IFusionCache cache)
			{
				// EMPTY
			}

			public void Stop(IFusionCache cache)
			{
				// EMPTY
			}
		}

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
				cache.AddPlugin(new SamplePlugin());

				cache.TryGet<int>("foo");
				cache.TryGet<int>("bar");
				cache.Set<int>("foo", 123);
				cache.TryGet<int>("foo");
				cache.GetOrSet<int>("qux", _ => throw new Exception("Sloths!"), 123, opt => opt.SetFailSafe(true));
			}

			Assert.Equal(20, logger.Items.Count);
			Assert.Single(logger.Items.Where(x => x.LogLevel == LogLevel.Warning));
		}

		[Fact]
		public async Task PluginsInfoWork()
		{
			var logger = CreateListLogger(LogLevel.Information);
			var options = new FusionCacheOptions();
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.AddPlugin(new SamplePlugin());
			}

			Assert.Equal(2, logger.Items.Count);

			logger = CreateListLogger(LogLevel.Information);
			options = new FusionCacheOptions()
			{
				PluginsInfoLogLevel = LogLevel.Debug
			};
			using (var cache = new FusionCache(options, logger: logger))
			{
				cache.AddPlugin(new SamplePlugin());
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
	}
}
