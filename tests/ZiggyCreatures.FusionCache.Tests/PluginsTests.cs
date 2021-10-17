using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.Tests
{
	public class PluginsTests
	{
		private class SamplePlugin
			: IFusionCachePlugin
		{
			private readonly bool _throwOnStart = false;
			private int _missCount = 0;

			public SamplePlugin(bool throwOnStart = false)
			{
				_throwOnStart = throwOnStart;
			}

			public void Start(IFusionCache cache)
			{
				IsStarted = true;

				if (_throwOnStart)
					throw new Exception("Uooops ¯\\_(ツ)_/¯");

				cache.Events.Miss += OnMiss;
			}

			public void Stop(IFusionCache cache)
			{
				IsStopped = true;
				cache.Events.Miss -= OnMiss;
			}

			private void OnMiss(object sender, FusionCacheEntryEventArgs e)
			{
				Interlocked.Increment(ref _missCount);
			}

			public bool IsStarted { get; private set; }
			public bool IsStopped { get; private set; }

			public int MissCount
			{
				get { return _missCount; }
			}
		}

		[Fact]
		public async Task PluginBasicsWorkAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				var plugin = new SamplePlugin();

				// ADD PLUGIN AND START IT
				cache.AddPlugin(plugin);

				// MISS: +1
				await cache.TryGetAsync<int>("foo");

				// MISS: +1
				await cache.GetOrDefaultAsync<int>("bar");

				// STOP PLUGIN AND REMOVE IT
				cache.RemovePlugin(plugin);

				// MISS: NO CHANGE (BECAUSE IN THEORY THE EVENT HANDLERS SHOULD HAVE BEEN REMOVED)
				await cache.TryGetAsync<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not started");
				Assert.True(plugin.IsStopped, "Plugin has not stopped");
				Assert.Equal(2, plugin.MissCount);
			}
		}

		[Fact]
		public void PluginBasicsWork()
		{
			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				var plugin = new SamplePlugin();

				// ADD PLUGIN AND START IT
				cache.AddPlugin(plugin);

				// MISS: +1
				cache.TryGet<int>("foo");

				// MISS: +1
				cache.GetOrDefault<int>("bar");

				// STOP PLUGIN AND REMOVE IT
				cache.RemovePlugin(plugin);

				// MISS: NO CHANGE (BECAUSE IN THEORY THE EVENT HANDLERS SHOULD HAVE BEEN REMOVED)
				cache.TryGet<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not started");
				Assert.True(plugin.IsStopped, "Plugin has not stopped");
				Assert.Equal(2, plugin.MissCount);
			}
		}

		[Fact]
		public async Task ThrowingDuringStartDoesNotAddPluginAsync()
		{
			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				var plugin = new SamplePlugin(true);

				// ADD PLUGIN AND START IT (AND THROW)
				Assert.Throws<InvalidOperationException>(() =>
				{
					cache.AddPlugin(plugin);
				});

				// MISS: NO CHANGE (BECAUSE IN THEORY THE PLUGIN HASN'T BEEN ADDED, BECAUSE EXCEPTION DURING Start())
				await cache.TryGetAsync<int>("foo");

				// STOP PLUGIN AND REMOVE IT
				var isRemoved = cache.RemovePlugin(plugin);

				// MISS: NO CHANGE (BECAUSE IN THEORY THE EVENT HANDLERS SHOULD HAVE BEEN REMOVED)
				await cache.TryGetAsync<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not been started");
				Assert.False(plugin.IsStopped, "Plugin has been stopped");
				Assert.False(isRemoved, "Plugin has been removed");
				Assert.Equal(0, plugin.MissCount);
			}
		}

		[Fact]
		public void ThrowingDuringStartDoesNotAddPlugin()
		{
			using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
			{
				var plugin = new SamplePlugin(true);

				// ADD PLUGIN AND START IT (AND THROW)
				Assert.Throws<InvalidOperationException>(() =>
				{
					cache.AddPlugin(plugin);
				});

				// MISS: NO CHANGE (BECAUSE IN THEORY THE PLUGIN HASN'T BEEN ADDED, BECAUSE EXCEPTION DURING Start())
				cache.TryGet<int>("foo");

				// STOP PLUGIN AND REMOVE IT
				var isRemoved = cache.RemovePlugin(plugin);

				// MISS: NO CHANGE (BECAUSE IN THEORY THE EVENT HANDLERS SHOULD HAVE BEEN REMOVED)
				cache.TryGet<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not been started");
				Assert.False(plugin.IsStopped, "Plugin has been stopped");
				Assert.False(isRemoved, "Plugin has been removed");
				Assert.Equal(0, plugin.MissCount);
			}
		}

		[Fact]
		public async Task DependencyInjectionAutoDiscoveryWorksAsync()
		{
			var services = new ServiceCollection();
			services.AddSingleton<IFusionCachePlugin, SamplePlugin>();
			services.AddFusionCache(options =>
			{
				options.EnableSyncEventHandlersExecution = true;
			});
			using (var serviceProvider = services.BuildServiceProvider())
			{
				var cache = serviceProvider.GetRequiredService<IFusionCache>();

				// GET THE PLUGIN (SHOULD RETURN THE SME INSTANCE, BECAUSE SINGLETON)
				var plugin = serviceProvider.GetRequiredService<IFusionCachePlugin>() as SamplePlugin;

				// MISS: +1
				await cache.TryGetAsync<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not been started");
				Assert.Equal(1, plugin.MissCount);
			}
		}

		[Fact]
		public void DependencyInjectionAutoDiscoveryWorks()
		{
			var services = new ServiceCollection();
			services.AddSingleton<IFusionCachePlugin, SamplePlugin>();
			services.AddFusionCache(options =>
			{
				options.EnableSyncEventHandlersExecution = true;
			});
			using (var serviceProvider = services.BuildServiceProvider())
			{
				var cache = serviceProvider.GetRequiredService<IFusionCache>();

				// GET THE PLUGIN (SHOULD RETURN THE SME INSTANCE, BECAUSE SINGLETON)
				var plugin = serviceProvider.GetRequiredService<IFusionCachePlugin>() as SamplePlugin;

				// MISS: +1
				cache.TryGet<int>("foo");

				Assert.True(plugin.IsStarted, "Plugin has not been started");
				Assert.Equal(1, plugin.MissCount);
			}
		}
	}
}
