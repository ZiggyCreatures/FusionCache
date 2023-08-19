using System;
using System.Threading;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;

namespace FusionCacheTests
{
	public class BackplaneTests
	{
		private readonly ITestOutputHelper _output;
		private static readonly string? _cacheKeyPrefix = "Foo:";

		public BackplaneTests(ITestOutputHelper output)
		{
			_output = output;
		}

		private XUnitLogger<T> CreateLogger<T>(LogLevel minLevel = LogLevel.Trace)
		{
			return new XUnitLogger<T>(minLevel, _output);
		}

		private static FusionCacheOptions CreateFusionCacheOptions()
		{
			var res = new FusionCacheOptions();

			res.CacheKeyPrefix = _cacheKeyPrefix;

			return res;
		}

		private static readonly string? RedisConnection = null;
		//private static readonly string? RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False";

		private IFusionCacheBackplane CreateBackplane(string connectionId)
		{
			if (string.IsNullOrWhiteSpace(RedisConnection))
				return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = connectionId }, logger: CreateLogger<MemoryBackplane>());

			return new RedisBackplane(new RedisBackplaneOptions { Configuration = RedisConnection }, logger: CreateLogger<RedisBackplane>());
		}

		private ChaosBackplane CreateChaosBackplane(string connectionId)
		{
			return new ChaosBackplane(CreateBackplane(connectionId));
		}

		private static IDistributedCache CreateDistributedCache()
		{
			if (string.IsNullOrWhiteSpace(RedisConnection))
				return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			return new RedisCache(new RedisCacheOptions { Configuration = RedisConnection });
		}

		private IFusionCache CreateFusionCache(string? cacheName, SerializerType? serializerType, IDistributedCache? distributedCache, IFusionCacheBackplane? backplane, Action<FusionCacheOptions>? setupAction = null)
		{
			var options = CreateFusionCacheOptions();

			options.CacheName = cacheName!;
			options.EnableSyncEventHandlersExecution = true;

			setupAction?.Invoke(options);
			var fusionCache = new FusionCache(options, logger: CreateLogger<FusionCache>());
			fusionCache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			fusionCache.DefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
			if (distributedCache is not null && serializerType.HasValue)
				fusionCache.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType.Value));
			if (backplane is not null)
				fusionCache.SetupBackplane(backplane);

			return fusionCache;
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
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

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

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

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
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

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			cache1.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cache2.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cache3.SetupBackplane(CreateBackplane(backplaneConnectionId));

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

		[Fact]
		public async Task WorksWithDifferentCachesAsync()
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId));
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));

			await Task.Delay(1_000);

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

		[Fact]
		public void WorksWithDifferentCaches()
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache("C1", null, null, CreateBackplane(backplaneConnectionId));
			using var cache2 = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));
			using var cache2bis = CreateFusionCache("C2", null, null, CreateBackplane(backplaneConnectionId));

			Thread.Sleep(1_000);

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

		[Fact]
		public async Task CanSkipNotificationsAsync()
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));

			cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;
			cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;
			cache3.DefaultEntryOptions.SkipBackplaneNotifications = true;

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			await Task.Delay(1_000);

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

		[Fact]
		public void CanSkipNotifications()
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var key = Guid.NewGuid().ToString("N");
			using var cache1 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
			using var cache2 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));
			using var cache3 = CreateFusionCache(null, null, null, CreateBackplane(backplaneConnectionId));

			cache1.DefaultEntryOptions.SkipBackplaneNotifications = true;
			cache2.DefaultEntryOptions.SkipBackplaneNotifications = true;
			cache3.DefaultEntryOptions.SkipBackplaneNotifications = true;

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			Thread.Sleep(1_000);

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

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task AutoRecoveryWorksAsync(SerializerType serializerType)
		{
			var defaultOptions = new FusionCacheOptions();

			var _value = 0;

			var key = "foo";
			var otherKey = "bar";

			var distributedCache = CreateDistributedCache();

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var backplane1 = CreateChaosBackplane(backplaneConnectionId);
			var backplane2 = CreateChaosBackplane(backplaneConnectionId);
			var backplane3 = CreateChaosBackplane(backplaneConnectionId);

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableBackplaneAutoRecovery = true; });
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableBackplaneAutoRecovery = true; });
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableBackplaneAutoRecovery = true; });

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// DISABLE THE BACKPLANE
			backplane1.SetAlwaysThrow();
			backplane2.SetAlwaysThrow();
			backplane3.SetAlwaysThrow();

			await Task.Delay(1_000);

			// 1
			_value = 1;
			await cache1.SetAsync(key, _value, TimeSpan.FromMinutes(10));
			await Task.Delay(200);

			// 2
			_value = 2;
			await cache2.SetAsync(key, _value, TimeSpan.FromMinutes(10));
			await Task.Delay(200);

			// 3
			_value = 3;
			await cache3.SetAsync(key, _value, TimeSpan.FromMinutes(10));
			await Task.Delay(200);

			Assert.Equal(1, await cache1.GetOrSetAsync<int>(key, async _ => _value));
			Assert.Equal(2, await cache2.GetOrSetAsync<int>(key, async _ => _value));
			Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));

			// RE-ENABLE THE BACKPLANE
			backplane1.SetNeverThrow();
			backplane2.SetNeverThrow();
			backplane3.SetNeverThrow();

			await Task.Delay(defaultOptions.BackplaneAutoRecoveryBackpressureDelay.PlusALittleBit());

			// CHANGE ANOTHER KEY (TO RUN AUTO-RECOVERY OPERATIONS)
			await cache1.SetAsync(otherKey, 42, TimeSpan.FromMinutes(10));

			await Task.Delay(1_000);

			Assert.Equal(3, await cache1.GetOrSetAsync<int>(key, async _ => _value));
			Assert.Equal(3, await cache2.GetOrSetAsync<int>(key, async _ => _value));
			Assert.Equal(3, await cache3.GetOrSetAsync<int>(key, async _ => _value));
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void AutoRecoveryWorks(SerializerType serializerType)
		{
			var defaultOptions = new FusionCacheOptions();

			var _value = 0;

			var key = "foo";
			var otherKey = "bar";

			var distributedCache = CreateDistributedCache();

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var backplane1 = CreateChaosBackplane(backplaneConnectionId);
			var backplane2 = CreateChaosBackplane(backplaneConnectionId);
			var backplane3 = CreateChaosBackplane(backplaneConnectionId);

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableBackplaneAutoRecovery = true; });
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableBackplaneAutoRecovery = true; });
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableBackplaneAutoRecovery = true; });

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// DISABLE THE BACKPLANE
			backplane1.SetAlwaysThrow();
			backplane2.SetAlwaysThrow();
			backplane3.SetAlwaysThrow();

			Thread.Sleep(1_000);

			// 1
			_value = 1;
			cache1.Set(key, _value, TimeSpan.FromMinutes(10));
			Thread.Sleep(200);

			// 2
			_value = 2;
			cache2.Set(key, _value, TimeSpan.FromMinutes(10));
			Thread.Sleep(200);

			// 3
			_value = 3;
			cache3.Set(key, _value, TimeSpan.FromMinutes(10));
			Thread.Sleep(200);

			Assert.Equal(1, cache1.GetOrSet<int>(key, _ => _value));
			Assert.Equal(2, cache2.GetOrSet<int>(key, _ => _value));
			Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));

			// RE-ENABLE THE BACKPLANE
			backplane1.SetNeverThrow();
			backplane2.SetNeverThrow();
			backplane3.SetNeverThrow();

			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryBackpressureDelay.PlusALittleBit());

			// CHANGE ANOTHER KEY (TO RUN AUTO-RECOVERY OPERATIONS)
			cache1.Set(otherKey, 42, TimeSpan.FromMinutes(10));

			Thread.Sleep(1_000);

			Assert.Equal(3, cache1.GetOrSet<int>(key, _ => _value));
			Assert.Equal(3, cache2.GetOrSet<int>(key, _ => _value));
			Assert.Equal(3, cache3.GetOrSet<int>(key, _ => _value));
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task AutoRecoveryRespectsMaxItemsAsync(SerializerType serializerType)
		{
			var _value = 0;

			var key1 = "foo";
			var key2 = "bar";

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = CreateDistributedCache();

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var backplane1 = CreateChaosBackplane(backplaneConnectionId);
			var backplane2 = CreateChaosBackplane(backplaneConnectionId);
			var backplane3 = CreateChaosBackplane(backplaneConnectionId);

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// DISABLE THE BACKPLANE
			backplane1.SetAlwaysThrow();
			backplane2.SetAlwaysThrow();
			backplane3.SetAlwaysThrow();

			await Task.Delay(1_000);

			// 1
			_value = 1;
			await cache1.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
			await cache1.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
			await Task.Delay(200);

			// 2
			_value = 2;
			await cache2.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
			await cache2.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
			await Task.Delay(200);

			// 3
			_value = 3;
			await cache3.SetAsync(key1, _value, TimeSpan.FromMinutes(10));
			await cache3.SetAsync(key2, _value, TimeSpan.FromMinutes(5));
			await Task.Delay(200);

			_value = 21;

			Assert.Equal(1, await cache1.GetOrSetAsync<int>(key1, async _ => _value));
			Assert.Equal(2, await cache2.GetOrSetAsync<int>(key1, async _ => _value));
			Assert.Equal(3, await cache3.GetOrSetAsync<int>(key1, async _ => _value));

			Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => _value));
			Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => _value));
			Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => _value));

			// RE-ENABLE THE BACKPLANE
			backplane1.SetNeverThrow();
			backplane2.SetNeverThrow();
			backplane3.SetNeverThrow();

			// CHANGE ANOTHER KEY (TO RUN AUTO-RECOVERY OPERATIONS)
			//await cache1.SetAsync(otherKey, 42, TimeSpan.FromMinutes(10));

			await Task.Delay(defaultOptions.BackplaneAutoRecoveryBackpressureDelay.PlusALittleBit());

			_value = 42;

			Assert.Equal(_value, await cache1.GetOrSetAsync<int>(key1, async _ => _value));
			Assert.Equal(_value, await cache2.GetOrSetAsync<int>(key1, async _ => _value));
			Assert.Equal(_value, await cache3.GetOrSetAsync<int>(key1, async _ => _value));

			Assert.Equal(1, await cache1.GetOrSetAsync<int>(key2, async _ => _value));
			Assert.Equal(2, await cache2.GetOrSetAsync<int>(key2, async _ => _value));
			Assert.Equal(3, await cache3.GetOrSetAsync<int>(key2, async _ => _value));
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void AutoRecoveryRespectsMaxItems(SerializerType serializerType)
		{
			var _value = 0;

			var key1 = "foo";
			var key2 = "bar";

			var defaultOptions = new FusionCacheOptions();

			var distributedCache = CreateDistributedCache();

			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var backplane1 = CreateChaosBackplane(backplaneConnectionId);
			var backplane2 = CreateChaosBackplane(backplaneConnectionId);
			var backplane3 = CreateChaosBackplane(backplaneConnectionId);

			using var cache1 = CreateFusionCache(null, serializerType, distributedCache, backplane1, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });
			using var cache2 = CreateFusionCache(null, serializerType, distributedCache, backplane2, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });
			using var cache3 = CreateFusionCache(null, serializerType, distributedCache, backplane3, opt => { opt.EnableBackplaneAutoRecovery = true; opt.BackplaneAutoRecoveryMaxItems = 1; });

			cache1.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache2.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
			cache3.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// DISABLE THE BACKPLANE
			backplane1.SetAlwaysThrow();
			backplane2.SetAlwaysThrow();
			backplane3.SetAlwaysThrow();

			Thread.Sleep(1_000);

			// 1
			_value = 1;
			cache1.Set(key1, _value, TimeSpan.FromMinutes(10));
			cache1.Set(key2, _value, TimeSpan.FromMinutes(5));
			Thread.Sleep(200);

			// 2
			_value = 2;
			cache2.Set(key1, _value, TimeSpan.FromMinutes(10));
			cache2.Set(key2, _value, TimeSpan.FromMinutes(5));
			Thread.Sleep(200);

			// 3
			_value = 3;
			cache3.Set(key1, _value, TimeSpan.FromMinutes(10));
			cache3.Set(key2, _value, TimeSpan.FromMinutes(5));
			Thread.Sleep(200);

			_value = 21;

			Assert.Equal(1, cache1.GetOrSet<int>(key1, _ => _value));
			Assert.Equal(2, cache2.GetOrSet<int>(key1, _ => _value));
			Assert.Equal(3, cache3.GetOrSet<int>(key1, _ => _value));

			Assert.Equal(1, cache1.GetOrSet<int>(key2, _ => _value));
			Assert.Equal(2, cache2.GetOrSet<int>(key2, _ => _value));
			Assert.Equal(3, cache3.GetOrSet<int>(key2, _ => _value));

			// RE-ENABLE THE BACKPLANE
			backplane1.SetNeverThrow();
			backplane2.SetNeverThrow();
			backplane3.SetNeverThrow();

			// CHANGE ANOTHER KEY (TO RUN AUTO-RECOVERY OPERATIONS)
			//await cache1.SetAsync(otherKey, 42, TimeSpan.FromMinutes(10));

			Thread.Sleep(defaultOptions.BackplaneAutoRecoveryBackpressureDelay.PlusALittleBit());

			_value = 42;

			Assert.Equal(_value, cache1.GetOrSet<int>(key1, _ => _value));
			Assert.Equal(_value, cache2.GetOrSet<int>(key1, _ => _value));
			Assert.Equal(_value, cache3.GetOrSet<int>(key1, _ => _value));

			Assert.Equal(1, cache1.GetOrSet<int>(key2, _ => _value));
			Assert.Equal(2, cache2.GetOrSet<int>(key2, _ => _value));
			Assert.Equal(3, cache3.GetOrSet<int>(key2, _ => _value));
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task CanHandleExpireOnMultiNodesAsync(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var duration = TimeSpan.FromMinutes(10);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheA.DefaultEntryOptions.Duration = duration;
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheB.DefaultEntryOptions.Duration = duration;
			cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			using var cacheC = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheC.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheC.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheC.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheC.DefaultEntryOptions.Duration = duration;
			cacheC.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// SET ON CACHE A
			await cacheA.SetAsync<int>("foo", 42);

			// GET ON CACHE A
			var maybeFooA1 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

			// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
			var maybeFooB1 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

			// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

			// EXPIRE ON CACHE A, WHIS WILL:
			// - EXPIRE ON CACHE A
			// - REMOVE ON DISTRIBUTED CACHE
			// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
			//   - EXPIRE ON CACHE B
			//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
			await cacheA.ExpireAsync("foo");

			await Task.Delay(TimeSpan.FromMilliseconds(100));

			// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
			var maybeFooA2 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

			// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
			var maybeFooB2 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

			// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
			var maybeFooC2 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetFailSafe(false));

			_output.WriteLine($"BEFORE");

			// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
			var maybeFooA3 = await cacheA.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

			_output.WriteLine($"AFTER");

			// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
			var maybeFooB3 = await cacheB.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

			// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
			var maybeFooC3 = await cacheC.TryGetAsync<int>("foo", opt => opt.SetFailSafe(true));

			Assert.True(maybeFooA1.HasValue);
			Assert.Equal(42, maybeFooA1.Value);

			Assert.True(maybeFooB1.HasValue);
			Assert.Equal(42, maybeFooB1.Value);

			Assert.False(maybeFooA2.HasValue);
			Assert.False(maybeFooB2.HasValue);
			Assert.False(maybeFooC2.HasValue);

			Assert.True(maybeFooA3.HasValue);
			Assert.Equal(42, maybeFooA3.Value);

			Assert.True(maybeFooB3.HasValue);
			Assert.Equal(42, maybeFooB3.Value);

			Assert.False(maybeFooC3.HasValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void CanHandleExpireOnMultiNodes(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var duration = TimeSpan.FromMinutes(10);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheA.DefaultEntryOptions.Duration = duration;
			cacheA.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheB.DefaultEntryOptions.Duration = duration;
			cacheB.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			using var cacheC = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheC.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheC.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheC.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheC.DefaultEntryOptions.Duration = duration;
			cacheC.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;

			// SET ON CACHE A
			cacheA.Set<int>("foo", 42);

			// GET ON CACHE A
			var maybeFooA1 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(true));

			// GET ON CACHE B (WILL GET FROM DISTRIBUTED CACHE AND SAVE ON LOCAL MEMORY CACHE)
			var maybeFooB1 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(true));

			// NOW CACHE A + B HAVE THE VALUE CACHED IN THEIR LOCAL MEMORY CACHE, WHILE CACHE C DOES NOT

			// EXPIRE ON CACHE A, WHIS WILL:
			// - EXPIRE ON CACHE A
			// - REMOVE ON DISTRIBUTED CACHE
			// - NOTIFY CACHE B AND CACHE C OF THE EXPIRATION AND THAT, IN TURN, WILL:
			//   - EXPIRE ON CACHE B
			//   - DO NOTHING ON CACHE C (IT WAS NOT IN ITS MEMORY CACHE)
			cacheA.Expire("foo");

			Thread.Sleep(TimeSpan.FromMilliseconds(100));

			// GET ON CACHE A: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
			var maybeFooA2 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(false));

			// GET ON CACHE B: SINCE IT'S EXPIRED AND FAIL-SAFE IS DISABLED, NOTHING WILL BE RETURNED
			var maybeFooB2 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(false));

			// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
			var maybeFooC2 = cacheC.TryGet<int>("foo", opt => opt.SetFailSafe(false));

			// GET ON CACHE A: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
			var maybeFooA3 = cacheA.TryGet<int>("foo", opt => opt.SetFailSafe(true));

			// GET ON CACHE B: SINCE IT'S EXPIRED BUT FAIL-SAFE IS ENABLED, THE STALE VALUE WILL BE RETURNED
			var maybeFooB3 = cacheB.TryGet<int>("foo", opt => opt.SetFailSafe(true));

			// GET ON CACHE C: SINCE NOTHING IS THERE, NOTHING WILL BE RETURNED
			var maybeFooC3 = cacheC.TryGet<int>("foo", opt => opt.SetFailSafe(true));

			Assert.True(maybeFooA1.HasValue);
			Assert.Equal(42, maybeFooA1.Value);

			Assert.True(maybeFooB1.HasValue);
			Assert.Equal(42, maybeFooB1.Value);

			Assert.False(maybeFooA2.HasValue);
			Assert.False(maybeFooB2.HasValue);
			Assert.False(maybeFooC2.HasValue);

			Assert.True(maybeFooA3.HasValue);
			Assert.Equal(42, maybeFooA3.Value);

			Assert.True(maybeFooB3.HasValue);
			Assert.Equal(42, maybeFooB3.Value);

			Assert.False(maybeFooC3.HasValue);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task BackgroundFactoryCompleteNotifyOtherNodesAsync(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var duration1 = TimeSpan.FromSeconds(1);
			var duration2 = TimeSpan.FromSeconds(10);
			var factorySoftTimeout = TimeSpan.FromMilliseconds(50);
			var simulatedFactoryDuration = TimeSpan.FromSeconds(3);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheA.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;

			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheB.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;

			// SET 10 ON CACHE-A AND DIST CACHE
			var fooA1 = await cacheA.GetOrSetAsync("foo", async _ => 10, duration1);

			// GET 10 FROM DIST CACHE AND SET ON CACHE-B
			var fooB1 = await cacheB.GetOrSetAsync("foo", async _ => 20, duration1);

			Assert.Equal(10, fooA1);
			Assert.Equal(10, fooB1);

			// WAIT FOR THE CACHE ENTRIES TO EXPIRE
			await Task.Delay(duration1.PlusALittleBit());

			// EXECUTE THE FACTORY ON CACHE-A, WHICH WILL TAKE 3 SECONDS, BUT
			// THE FACTORY SOFT TIMEOUT IS 50 MILLISECONDS, SO IT WILL FAIL
			// AND THE STALE VALUE WILL BE RETURNED
			// THE FACTORY WILL BE KEPT RUNNING IN THE BACKGROUND, AND WHEN
			// IT WILL COMPLETE SUCCESSFULLY UPDATE CACHE-A, THE DIST
			// CACHE AND NOTIFY THE OTHER NODES
			// SUCESSFULLY UPDATE CACHE-A, THE DIST CACHE AND NOTIFY THE OTHER NODES
			var fooA2 = await cacheA.GetOrSetAsync(
				"foo",
				async _ =>
				{
					await Task.Delay(simulatedFactoryDuration);
					return 30;
				},
				duration2
			);

			// IMMEDIATELY GET OR SET FROM CACHE-B: THE VALUE THERE IS
			// EXPIRED, SO THE NEW VALUE WILL BE SAVED AND RETURNED
			var fooB2 = await cacheB.GetOrSetAsync(
				"foo",
				40,
				duration2
			);

			Assert.Equal(10, fooA2);
			Assert.Equal(40, fooB2);

			// WAIT FOR THE SIMULATED FACTORY TO COMPLETE: A NOTIFICATION
			// WILL BE SENT TO THE OTHER NODES, WHICH IN TURN WILL UPDATE
			// THEIR CACHE ENTRIES
			await Task.Delay(simulatedFactoryDuration.PlusALittleBit());

			// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
			var fooA3 = await cacheA.GetOrDefaultAsync<int>("foo");
			var fooB3 = await cacheB.GetOrDefaultAsync<int>("foo");

			Assert.Equal(30, fooA3);
			Assert.Equal(30, fooB3);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void BackgroundFactoryCompleteNotifyOtherNodes(SerializerType serializerType)
		{
			var backplaneConnectionId = Guid.NewGuid().ToString("N");

			var duration1 = TimeSpan.FromSeconds(1);
			var duration2 = TimeSpan.FromSeconds(10);
			var factorySoftTimeout = TimeSpan.FromMilliseconds(50);
			var simulatedFactoryDuration = TimeSpan.FromSeconds(3);

			var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

			using var cacheA = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheA.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheA.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheA.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheA.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;

			using var cacheB = new FusionCache(CreateFusionCacheOptions(), logger: CreateLogger<FusionCache>());
			cacheB.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));
			cacheB.SetupBackplane(CreateBackplane(backplaneConnectionId));
			cacheB.DefaultEntryOptions.IsFailSafeEnabled = true;
			cacheB.DefaultEntryOptions.FactorySoftTimeout = factorySoftTimeout;

			// SET 10 ON CACHE-A AND DIST CACHE
			var fooA1 = cacheA.GetOrSet("foo", _ => 10, duration1);

			// GET 10 FROM DIST CACHE AND SET ON CACHE-B
			var fooB1 = cacheB.GetOrSet("foo", _ => 20, duration1);

			Assert.Equal(10, fooA1);
			Assert.Equal(10, fooB1);

			// WAIT FOR THE CACHE ENTRIES TO EXPIRE
			Thread.Sleep(duration1.PlusALittleBit());

			// EXECUTE THE FACTORY ON CACHE-A, WHICH WILL TAKE 3 SECONDS, BUT
			// THE FACTORY SOFT TIMEOUT IS 50 MILLISECONDS, SO IT WILL FAIL
			// AND THE STALE VALUE WILL BE RETURNED
			// THE FACTORY WILL BE KEPT RUNNING IN THE BACKGROUND, AND WHEN
			// IT WILL COMPLETE SUCCESSFULLY UPDATE CACHE-A, THE DIST
			// CACHE AND NOTIFY THE OTHER NODES
			// SUCESSFULLY UPDATE CACHE-A, THE DIST CACHE AND NOTIFY THE OTHER NODES
			var fooA2 = cacheA.GetOrSet(
				"foo",
				_ =>
				{
					Thread.Sleep(simulatedFactoryDuration);
					return 30;
				},
				duration2
			);

			// IMMEDIATELY GET OR SET FROM CACHE-B: THE VALUE THERE IS
			// EXPIRED, SO THE NEW VALUE WILL BE SAVED AND RETURNED
			var fooB2 = cacheB.GetOrSet(
				"foo",
				40,
				duration2
			);

			Assert.Equal(10, fooA2);
			Assert.Equal(40, fooB2);

			// WAIT FOR THE SIMULATED FACTORY TO COMPLETE: A NOTIFICATION
			// WILL BE SENT TO THE OTHER NODES, WHICH IN TURN WILL UPDATE
			// THEIR CACHE ENTRIES
			Thread.Sleep(simulatedFactoryDuration.PlusALittleBit());

			// GET THE UPDATED VALUES FROM CACHE-A AND CACHE-B
			var fooA3 = cacheA.GetOrDefault<int>("foo");
			var fooB3 = cacheB.GetOrDefault<int>("foo");

			Assert.Equal(30, fooA3);
			Assert.Equal(30, fooB3);
		}
	}
}
