using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests
{
	public enum SerializerType
	{
		NewtonsoftJson = 0,
		SystemTextJson = 1
	}

	public class BackplaneTests
	{
		private static string? RedisConnection = null;
		//private static string? RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False";

		private static IFusionCacheSerializer GetSerializer(SerializerType serializerType)
		{
			switch (serializerType)
			{
				case SerializerType.NewtonsoftJson:
					return new FusionCacheNewtonsoftJsonSerializer();
				case SerializerType.SystemTextJson:
					return new FusionCacheSystemTextJsonSerializer();
			}

			throw new ArgumentException("Invalid serializer specified", nameof(serializerType));
		}

		private static IFusionCachePlugin CreateBackplane()
		{
			if (string.IsNullOrWhiteSpace(RedisConnection))
				return new MemoryBackplanePlugin(new MemoryBackplaneOptions());

			return new RedisBackplanePlugin(new RedisBackplaneOptions { Configuration = RedisConnection });
		}

		private static IFusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCachePlugin? backplane)
		{
			var fusionCache = new FusionCache(new FusionCacheOptions() { CacheName = cacheName, EnableSyncEventHandlersExecution = true });
			if (distributedCache is object && serializerType.HasValue)
				fusionCache.SetupDistributedCache(distributedCache, GetSerializer(serializerType.Value));
			if (backplane is object)
				fusionCache.AddPlugin(backplane);

			return fusionCache;
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task BackplaneWorksAsync(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

			cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

			try
			{
				await cache1.GetOrSetAsync("foo", async _ => 1, TimeSpan.FromMinutes(10));
				await cache2.GetOrSetAsync("foo", async _ => 2, TimeSpan.FromMinutes(10));
				await cache3.GetOrSetAsync("foo", async _ => 3, TimeSpan.FromMinutes(10));

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(1, await cache2.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(1, await cache3.GetOrDefaultAsync<int>("foo"));

				await cache1.SetAsync("foo", 21);

				await Task.Delay(1_000);

				Assert.Equal(21, await cache1.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(1, await cache2.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(1, await cache3.GetOrDefaultAsync<int>("foo"));

				cache1.AddPlugin(CreateBackplane());
				cache2.AddPlugin(CreateBackplane());
				cache3.AddPlugin(CreateBackplane());

				await cache1.SetAsync("foo", 42);

				await Task.Delay(1_000);

				Assert.Equal(42, await cache1.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(42, await cache2.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(42, await cache3.GetOrDefaultAsync<int>("foo"));

				await cache1.RemoveAsync("foo");

				await Task.Delay(1_000);

				Assert.Equal(0, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(0, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(0, cache3.GetOrDefault<int>("foo"));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache3?.Dispose();
			}
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public void BackplaneWorks(SerializerType serializerType)
		{
			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

			cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

			try
			{
				cache1.GetOrSet("foo", _ => 1, TimeSpan.FromMinutes(10));
				cache2.GetOrSet("foo", _ => 2, TimeSpan.FromMinutes(10));
				cache3.GetOrSet("foo", _ => 3, TimeSpan.FromMinutes(10));

				Assert.Equal(1, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(1, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(1, cache3.GetOrDefault<int>("foo"));

				cache1.Set("foo", 21, TimeSpan.FromMinutes(10));

				Thread.Sleep(1_000);

				Assert.Equal(21, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(1, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(1, cache3.GetOrDefault<int>("foo"));

				cache1.AddPlugin(CreateBackplane());
				cache2.AddPlugin(CreateBackplane());
				cache3.AddPlugin(CreateBackplane());

				cache1.Set("foo", 42, TimeSpan.FromMinutes(10));

				Thread.Sleep(1_000);

				Assert.Equal(42, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(42, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(42, cache3.GetOrDefault<int>("foo"));

				cache1.Remove("foo");

				Thread.Sleep(1_000);

				Assert.Equal(0, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(0, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(0, cache3.GetOrDefault<int>("foo"));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache3?.Dispose();
			}
		}

		[Fact]
		public async Task WorksWithDifferentCachesAsync()
		{
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane());
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane());
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane());

			try
			{
				await cache1.GetOrSetAsync("foo", async _ => 1, TimeSpan.FromMinutes(10));
				await cache2.GetOrSetAsync("foo", async _ => 2, TimeSpan.FromMinutes(10));
				await Task.Delay(1_000);
				await cache2bis.GetOrSetAsync("foo", async _ => 2, TimeSpan.FromMinutes(10));
				await Task.Delay(1_000);

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(0, await cache2.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(2, await cache2bis.GetOrDefaultAsync<int>("foo"));

				await cache1.SetAsync("foo", 21);
				await cache2.SetAsync("foo", 42);

				await Task.Delay(1_000);

				Assert.Equal(21, await cache1.GetOrSetAsync("foo", async _ => 78, TimeSpan.FromMinutes(10)));
				Assert.Equal(42, await cache2.GetOrSetAsync("foo", async _ => 78, TimeSpan.FromMinutes(10)));
				await Task.Delay(1_000);
				Assert.Equal(78, await cache2bis.GetOrSetAsync("foo", async _ => 78, TimeSpan.FromMinutes(10)));
				await Task.Delay(1_000);
				Assert.Equal(88, await cache2.GetOrSetAsync("foo", async _ => 88, TimeSpan.FromMinutes(10)));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache2bis?.Dispose();
			}
		}

		[Fact]
		public void WorksWithDifferentCaches()
		{
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane());
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane());
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane());

			try
			{
				cache1.GetOrSet("foo", _ => 1, TimeSpan.FromMinutes(10));
				cache2.GetOrSet("foo", _ => 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(1_000);
				cache2bis.GetOrSet("foo", _ => 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(1_000);

				Assert.Equal(1, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(0, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(2, cache2bis.GetOrDefault<int>("foo"));

				cache1.Set("foo", 21);
				cache2.Set("foo", 42);

				Thread.Sleep(1_000);

				Assert.Equal(21, cache1.GetOrSet("foo", _ => 78, TimeSpan.FromMinutes(10)));
				Assert.Equal(42, cache2.GetOrSet("foo", _ => 78, TimeSpan.FromMinutes(10)));
				Thread.Sleep(1_000);
				Assert.Equal(78, cache2bis.GetOrSet("foo", _ => 78, TimeSpan.FromMinutes(10)));
				Thread.Sleep(1_000);
				Assert.Equal(88, cache2.GetOrSet("foo", _ => 88, TimeSpan.FromMinutes(10)));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache2bis?.Dispose();
			}
		}

		[Fact]
		public async Task NoAutomaticNotificationsAsync()
		{
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane());

			cache1.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache2.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache3.DefaultEntryOptions.EnableBackplaneNotifications = false;

			try
			{
				await cache1.SetAsync("foo", 1, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				await cache2.SetAsync("foo", 2, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				await cache3.SetAsync("foo", 3, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(2, await cache2.GetOrDefaultAsync<int>("foo"));
				Assert.Equal(3, await cache3.GetOrDefaultAsync<int>("foo"));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache3?.Dispose();
			}
		}

		[Fact]
		public void NoAutomaticNotifications()
		{
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane());

			cache1.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache2.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache3.DefaultEntryOptions.EnableBackplaneNotifications = false;

			try
			{
				cache1.Set("foo", 1, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				cache2.Set("foo", 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				cache3.Set("foo", 3, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				Assert.Equal(1, cache1.GetOrDefault<int>("foo"));
				Assert.Equal(2, cache2.GetOrDefault<int>("foo"));
				Assert.Equal(3, cache3.GetOrDefault<int>("foo"));
			}
			finally
			{
				cache1?.Dispose();
				cache2?.Dispose();
				cache3?.Dispose();
			}
		}
	}
}
