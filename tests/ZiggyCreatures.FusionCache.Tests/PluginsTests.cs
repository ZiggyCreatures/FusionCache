﻿using System;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace FusionCacheTests;

public class PluginsTests
{
	[Fact]
	public async Task PluginBasicsWorkAsync()
	{
		using (var cache = new FusionCache(new FusionCacheOptions() { EnableSyncEventHandlersExecution = true }))
		{
			var plugin = new SimpleEventsPlugin();

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
			var plugin = new SimpleEventsPlugin();

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
			var plugin = new SimpleEventsPlugin(true);

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
			var plugin = new SimpleEventsPlugin(true);

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

		services.AddSingleton<IFusionCachePlugin, SimpleEventsPlugin>();
		services.AddFusionCache()
			.TryWithAutoSetup()
			.WithOptions(options =>
			{
				options.EnableSyncEventHandlersExecution = true;
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		// GET THE PLUGIN (SHOULD RETURN THE SAME INSTANCE, BECAUSE SINGLETON)
		var plugin = serviceProvider.GetRequiredService<IFusionCachePlugin>() as SimpleEventsPlugin;

		// MISS: +1
		await cache.TryGetAsync<int>("foo");

		Assert.True(plugin!.IsStarted, "Plugin has not been started");
		Assert.Equal(1, plugin.MissCount);
	}

	[Fact]
	public void DependencyInjectionAutoDiscoveryWorks()
	{
		var services = new ServiceCollection();

		services.AddSingleton<IFusionCachePlugin, SimpleEventsPlugin>();
		services.AddFusionCache()
			.TryWithAutoSetup()
			.WithOptions(options =>
			{
				options.EnableSyncEventHandlersExecution = true;
			})
		;

		using var serviceProvider = services.BuildServiceProvider();

		var cache = serviceProvider.GetRequiredService<IFusionCache>();

		// GET THE PLUGIN (SHOULD RETURN THE SAME INSTANCE, BECAUSE SINGLETON)
		var plugin = serviceProvider.GetRequiredService<IFusionCachePlugin>() as SimpleEventsPlugin;

		// MISS: +1
		cache.TryGet<int>("foo");

		Assert.True(plugin!.IsStarted, "Plugin has not been started");
		Assert.Equal(1, plugin.MissCount);
	}
}
