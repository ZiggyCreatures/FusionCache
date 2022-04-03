using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
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

		private static IFusionCacheBackplane CreateBackplane()
		{
			if (string.IsNullOrWhiteSpace(RedisConnection))
				return new MemoryBackplane(new MemoryBackplaneOptions());

			return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection });
		}

		private static IDistributedCache CreateDistributedCache()
		{
			if (string.IsNullOrWhiteSpace(RedisConnection))
				return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });
		}

		private static IFusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCacheBackplane? backplane)
		{
			var fusionCache = new FusionCache(new FusionCacheOptions()
			{
				CacheName = cacheName!,
				EnableSyncEventHandlersExecution = true
			});
			fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
			if (distributedCache is object && serializerType.HasValue)
				fusionCache.SetupDistributedCache(distributedCache, GetSerializer(serializerType.Value));
			if (backplane is object)
				fusionCache.SetupBackplane(backplane);

			return fusionCache;
		}

		[Theory]
		[InlineData(SerializerType.NewtonsoftJson)]
		[InlineData(SerializerType.SystemTextJson)]
		public async Task BackplaneWorksAsync(SerializerType serializerType)
		{
			var key = Guid.NewGuid().ToString("N");
			var distributedCache = CreateDistributedCache();
			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

			cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

			try
			{
				await cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10));
				await cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
				await cache3.GetOrSetAsync(key, async _ => 3, TimeSpan.FromMinutes(10));

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
				Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key));
				Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key));

				await cache1.SetAsync(key, 21);

				await Task.Delay(1_000);

				Assert.Equal(21, await cache1.GetOrDefaultAsync<int>(key));
				Assert.Equal(1, await cache2.GetOrDefaultAsync<int>(key));
				Assert.Equal(1, await cache3.GetOrDefaultAsync<int>(key));

				cache1.SetupBackplane(CreateBackplane());
				cache2.SetupBackplane(CreateBackplane());
				cache3.SetupBackplane(CreateBackplane());

				await Task.Delay(1_000);

				await cache1.SetAsync(key, 42);

				await Task.Delay(1_000);

				Assert.Equal(42, await cache1.GetOrDefaultAsync<int>(key));
				Assert.Equal(42, await cache2.GetOrDefaultAsync<int>(key));
				Assert.Equal(42, await cache3.GetOrDefaultAsync<int>(key));

				await cache1.RemoveAsync(key);

				await Task.Delay(1_000);

				Assert.Equal(0, cache1.GetOrDefault<int>(key));
				Assert.Equal(0, cache2.GetOrDefault<int>(key));
				Assert.Equal(0, cache3.GetOrDefault<int>(key));
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
			var key = Guid.NewGuid().ToString("N");
			var distributedCache = CreateDistributedCache();

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, null);
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, null);

			cache1.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache2.DefaultEntryOptions.IsFailSafeEnabled = true;
			cache3.DefaultEntryOptions.IsFailSafeEnabled = true;

			try
			{
				cache1.GetOrSet(key, _ => 1, TimeSpan.FromMinutes(10));
				cache2.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
				cache3.GetOrSet(key, _ => 3, TimeSpan.FromMinutes(10));

				Assert.Equal(1, cache1.GetOrDefault<int>(key));
				Assert.Equal(1, cache2.GetOrDefault<int>(key));
				Assert.Equal(1, cache3.GetOrDefault<int>(key));

				cache1.Set(key, 21, TimeSpan.FromMinutes(10));

				Thread.Sleep(1_000);

				Assert.Equal(21, cache1.GetOrDefault<int>(key));
				Assert.Equal(1, cache2.GetOrDefault<int>(key));
				Assert.Equal(1, cache3.GetOrDefault<int>(key));

				cache1.SetupBackplane(CreateBackplane());
				cache2.SetupBackplane(CreateBackplane());
				cache3.SetupBackplane(CreateBackplane());

				Thread.Sleep(1_000);

				cache1.Set(key, 42, TimeSpan.FromMinutes(10));

				Thread.Sleep(1_000);

				Assert.Equal(42, cache1.GetOrDefault<int>(key));
				Assert.Equal(42, cache2.GetOrDefault<int>(key));
				Assert.Equal(42, cache3.GetOrDefault<int>(key));

				cache1.Remove(key);

				Thread.Sleep(1_000);

				Assert.Equal(0, cache1.GetOrDefault<int>(key));
				Assert.Equal(0, cache2.GetOrDefault<int>(key));
				Assert.Equal(0, cache3.GetOrDefault<int>(key));
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
			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane());
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane());
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane());

			await Task.Delay(1_000);

			try
			{
				await cache1.GetOrSetAsync(key, async _ => 1, TimeSpan.FromMinutes(10));
				await cache2.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
				await Task.Delay(1_000);
				await cache2bis.GetOrSetAsync(key, async _ => 2, TimeSpan.FromMinutes(10));
				await Task.Delay(1_000);

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
				Assert.Equal(0, await cache2.GetOrDefaultAsync<int>(key));
				Assert.Equal(2, await cache2bis.GetOrDefaultAsync<int>(key));

				await cache1.SetAsync(key, 21);
				await cache2.SetAsync(key, 42);

				await Task.Delay(1_000);

				Assert.Equal(21, await cache1.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
				Assert.Equal(42, await cache2.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
				await Task.Delay(1_000);
				Assert.Equal(78, await cache2bis.GetOrSetAsync(key, async _ => 78, TimeSpan.FromMinutes(10)));
				await Task.Delay(1_000);
				Assert.Equal(88, await cache2.GetOrSetAsync(key, async _ => 88, TimeSpan.FromMinutes(10)));
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
			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane());
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane());
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane());

			Thread.Sleep(1_000);

			try
			{
				cache1.GetOrSet(key, _ => 1, TimeSpan.FromMinutes(10));
				cache2.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(1_000);
				cache2bis.GetOrSet(key, _ => 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(1_000);

				Assert.Equal(1, cache1.GetOrDefault<int>(key));
				Assert.Equal(0, cache2.GetOrDefault<int>(key));
				Assert.Equal(2, cache2bis.GetOrDefault<int>(key));

				cache1.Set(key, 21);
				cache2.Set(key, 42);

				Thread.Sleep(1_000);

				Assert.Equal(21, cache1.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
				Assert.Equal(42, cache2.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
				Thread.Sleep(1_000);
				Assert.Equal(78, cache2bis.GetOrSet(key, _ => 78, TimeSpan.FromMinutes(10)));
				Thread.Sleep(1_000);
				Assert.Equal(88, cache2.GetOrSet(key, _ => 88, TimeSpan.FromMinutes(10)));
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
			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane());

			cache1.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache2.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache3.DefaultEntryOptions.EnableBackplaneNotifications = false;

			await Task.Delay(1_000);

			try
			{
				await cache1.SetAsync(key, 1, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				await cache2.SetAsync(key, 2, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				await cache3.SetAsync(key, 3, TimeSpan.FromMinutes(10));
				await Task.Delay(200);

				Assert.Equal(1, await cache1.GetOrDefaultAsync<int>(key));
				Assert.Equal(2, await cache2.GetOrDefaultAsync<int>(key));
				Assert.Equal(3, await cache3.GetOrDefaultAsync<int>(key));
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
			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane());
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane());

			cache1.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache2.DefaultEntryOptions.EnableBackplaneNotifications = false;
			cache3.DefaultEntryOptions.EnableBackplaneNotifications = false;

			Thread.Sleep(1_000);

			try
			{
				cache1.Set(key, 1, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				cache2.Set(key, 2, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				cache3.Set(key, 3, TimeSpan.FromMinutes(10));
				Thread.Sleep(200);

				Assert.Equal(1, cache1.GetOrDefault<int>(key));
				Assert.Equal(2, cache2.GetOrDefault<int>(key));
				Assert.Equal(3, cache3.GetOrDefault<int>(key));
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
