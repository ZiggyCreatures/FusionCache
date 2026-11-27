using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public partial class BufferedL2Tests
{
	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferedPathIsUsedWhenBothSupportBuffers(SerializerType serializerType)
	{
		var innerCache = CreateInnerDistributedCache();
		var distributedCache = new BufferCallTrackingDistributedCache(innerCache);

		using var cache1 = new FusionCache(CreateFusionCacheOptions());
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache2 = new FusionCache(CreateFusionCacheOptions());
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var key = CreateRandomCacheKey("buffered");
		var value = ComplexType.CreateSample();

		cache1.Set(key, value, token: TestContext.Current.CancellationToken);
		// A SECOND CACHE INSTANCE, SO THE READ CANNOT BE SERVED BY L1
		var looped = cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken);

		Assert.Equal(value, looped);

		Assert.Equal(1, distributedCache.BufferSetCount(key));
		Assert.Equal(1, distributedCache.BufferGetCount(key));
		Assert.Equal(0, distributedCache.ClassicSetCount(key));
		Assert.Equal(0, distributedCache.ClassicGetCount(key));

		// THE WRITER MUST STAY CONTIGUOUS, OTHERWISE CONSUMERS LIKE REDIS HAVE TO LINEARIZE IT
		Assert.True(distributedCache.AllSetSequencesWereSingleSegment);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ClassicPathIsUsedWhenSerializerDoesNotSupportBuffers(SerializerType serializerType)
	{
		var innerCache = CreateInnerDistributedCache();
		var distributedCache = new BufferCallTrackingDistributedCache(innerCache);

		using var cache1 = new FusionCache(CreateFusionCacheOptions());
		cache1.SetupDistributedCache(distributedCache, new ClassicOnlySerializer(TestsUtils.GetSerializer(serializerType)));

		using var cache2 = new FusionCache(CreateFusionCacheOptions());
		cache2.SetupDistributedCache(distributedCache, new ClassicOnlySerializer(TestsUtils.GetSerializer(serializerType)));

		var key = CreateRandomCacheKey("classic-serializer");
		var value = ComplexType.CreateSample();

		cache1.Set(key, value, token: TestContext.Current.CancellationToken);
		var looped = cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken);

		Assert.Equal(value, looped);

		Assert.Equal(0, distributedCache.BufferSetCount(key));
		Assert.Equal(0, distributedCache.BufferGetCount(key));
		Assert.Equal(1, distributedCache.ClassicSetCount(key));
		Assert.Equal(1, distributedCache.ClassicGetCount(key));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ClassicPathIsUsedWhenCacheDoesNotSupportBuffers(SerializerType serializerType)
	{
		var innerCache = CreateInnerDistributedCache();
		var distributedCache = new CallTrackingDistributedCache(innerCache);

		using var cache1 = new FusionCache(CreateFusionCacheOptions());
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache2 = new FusionCache(CreateFusionCacheOptions());
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		var key = CreateRandomCacheKey("classic-cache");
		var value = ComplexType.CreateSample();

		cache1.Set(key, value, token: TestContext.Current.CancellationToken);
		var looped = cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken);

		Assert.Equal(value, looped);

		Assert.Equal(1, distributedCache.ClassicSetCount(key));
		Assert.Equal(1, distributedCache.ClassicGetCount(key));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferedAndClassicPathsAreWireCompatible(SerializerType serializerType)
	{
		// THE SAME UNDERLYING STORAGE, SEEN ONCE THROUGH THE BUFFERED PATH AND ONCE THROUGH THE CLASSIC ONE:
		// THIS IS WHAT A MIXED FLEET (OR A ROLLBACK) LOOKS LIKE
		var innerCache = CreateInnerDistributedCache();
		var bufferedDistributedCache = new BufferCallTrackingDistributedCache(innerCache);
		var classicDistributedCache = new CallTrackingDistributedCache(innerCache);

		using var bufferedCache = new FusionCache(CreateFusionCacheOptions());
		bufferedCache.SetupDistributedCache(bufferedDistributedCache, TestsUtils.GetSerializer(serializerType));

		using var classicCache = new FusionCache(CreateFusionCacheOptions());
		classicCache.SetupDistributedCache(classicDistributedCache, new ClassicOnlySerializer(TestsUtils.GetSerializer(serializerType)));

		var value = ComplexType.CreateSample();

		// BUFFERED -> CLASSIC
		var key1 = CreateRandomCacheKey("buffered-to-classic");
		bufferedCache.Set(key1, value, token: TestContext.Current.CancellationToken);
		Assert.Equal(value, classicCache.GetOrDefault<ComplexType>(key1, token: TestContext.Current.CancellationToken));

		// CLASSIC -> BUFFERED
		var key2 = CreateRandomCacheKey("classic-to-buffered");
		classicCache.Set(key2, value, token: TestContext.Current.CancellationToken);
		Assert.Equal(value, bufferedCache.GetOrDefault<ComplexType>(key2, token: TestContext.Current.CancellationToken));

		Assert.Equal(1, bufferedDistributedCache.BufferSetCount(key1));
		Assert.Equal(1, bufferedDistributedCache.BufferGetCount(key2));
		Assert.Equal(1, classicDistributedCache.ClassicSetCount(key2));
		Assert.Equal(1, classicDistributedCache.ClassicGetCount(key1));
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void BufferedPathHandlesMissesAndNullValues(SerializerType serializerType)
	{
		var innerCache = CreateInnerDistributedCache();
		var distributedCache = new BufferCallTrackingDistributedCache(innerCache);

		using var cache1 = new FusionCache(CreateFusionCacheOptions());
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		using var cache2 = new FusionCache(CreateFusionCacheOptions());
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(serializerType));

		// MISS: THE BUFFER MUST BE RELEASED WITHOUT BEING HANDED TO THE SERIALIZER
		var missKey = CreateRandomCacheKey("miss");
		Assert.Null(cache2.GetOrDefault<string>(missKey, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, distributedCache.BufferGetCount(missKey));

		// NULL VALUE
		var nullKey = CreateRandomCacheKey("null");
		cache1.Set<string?>(nullKey, null, token: TestContext.Current.CancellationToken);
		Assert.Null(cache2.GetOrDefault<string?>(nullKey, token: TestContext.Current.CancellationToken));
		Assert.Equal(1, distributedCache.BufferSetCount(nullKey));
		Assert.Equal(1, distributedCache.BufferGetCount(nullKey));

		// NON-NULL VALUE, FOR GOOD MEASURE
		var key = CreateRandomCacheKey("value");
		cache1.Set(key, SampleString, token: TestContext.Current.CancellationToken);
		Assert.Equal(SampleString, cache2.GetOrDefault<string>(key, token: TestContext.Current.CancellationToken));

		foreach (var k in new[] { missKey, nullKey, key })
		{
			Assert.Equal(0, distributedCache.ClassicSetCount(k));
			Assert.Equal(0, distributedCache.ClassicGetCount(k));
		}
	}

	[Fact]
	public void BufferedPathReturnsAllBuffersToThePool()
	{
		var pool = new TrackingArrayPool();
		var distributedCache = new BufferCallTrackingDistributedCache(CreateInnerDistributedCache());

		using var cache1 = new FusionCache(CreateFusionCacheOptions(pool));
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(SerializerType.SystemTextJson));

		using var cache2 = new FusionCache(CreateFusionCacheOptions(pool));
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(SerializerType.SystemTextJson));

		var key = CreateRandomCacheKey("pool");
		var value = ComplexType.CreateSample();

		// SET + GET (HIT)
		cache1.Set(key, value, token: TestContext.Current.CancellationToken);
		Assert.Equal(value, cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken));

		// GET (MISS)
		Assert.Null(cache2.GetOrDefault<ComplexType>(CreateRandomCacheKey("pool-miss"), token: TestContext.Current.CancellationToken));

		Assert.Equal(1, distributedCache.BufferSetCount(key));
		Assert.Equal(1, distributedCache.BufferGetCount(key));
		Assert.Equal(0, pool.Outstanding);
		Assert.Equal(0, pool.InvalidReturns);
	}

	[Fact]
	public void BufferedPathReturnsBuffersToThePoolWhenDistributedCacheThrows()
	{
		var pool = new TrackingArrayPool();
		var chaosDistributedCache = new ChaosBufferDistributedCache(new BufferCallTrackingDistributedCache(CreateInnerDistributedCache()));

		var options = CreateFusionCacheOptions(pool);
		options.DefaultEntryOptions.ReThrowDistributedCacheExceptions = false;

		using var cache = new FusionCache(options);
		cache.SetupDistributedCache(chaosDistributedCache, new ChaosBufferSerializer((IBufferFusionCacheSerializer)TestsUtils.GetSerializer(SerializerType.SystemTextJson)));

		chaosDistributedCache.SetAlwaysThrow();

		var key = CreateRandomCacheKey("pool-throw");

		cache.Set(key, ComplexType.CreateSample(), token: TestContext.Current.CancellationToken);
		cache.GetOrDefault<ComplexType>(CreateRandomCacheKey("pool-throw-get"), token: TestContext.Current.CancellationToken);

		// ONE BUFFER FOR THE SET, ONE FOR THE GET
		Assert.Equal(2, pool.TotalRents);
		Assert.Equal(0, pool.Outstanding);
		Assert.Equal(0, pool.InvalidReturns);
	}

	[Fact]
	public void BufferedPathReturnsBufferToThePoolOnlyAfterTimedOutGetCompletes()
	{
		var pool = new TrackingArrayPool();
		var key = CreateRandomCacheKey("pool-timeout");
		var distributedCache = new GatedBufferDistributedCache(CreateInnerDistributedCache(), x => x.Contains(key, StringComparison.Ordinal));

		using var cache1 = new FusionCache(CreateFusionCacheOptions(pool));
		cache1.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(SerializerType.SystemTextJson));

		var options2 = CreateFusionCacheOptions(pool);
		options2.DefaultEntryOptions.DistributedCacheHardTimeout = TimeSpan.FromMilliseconds(100);
		using var cache2 = new FusionCache(options2);
		cache2.SetupDistributedCache(distributedCache, TestsUtils.GetSerializer(SerializerType.SystemTextJson));

		cache1.Set(key, ComplexType.CreateSample(), token: TestContext.Current.CancellationToken);
		Assert.Equal(0, pool.Outstanding);

		// THE READ TIMES OUT, BUT THE OPERATION IS STILL IN FLIGHT AND WILL WRITE INTO THE BUFFER LATER
		distributedCache.Enable();
		Assert.Null(cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken));

		// SO THE BUFFER MUST NOT HAVE BEEN RETURNED TO THE POOL YET...
		Assert.Equal(1, pool.Outstanding);

		// ...BUT IT MUST BE ONCE THE OPERATION COMPLETES
		distributedCache.OpenGate();
		Assert.True(distributedCache.ReadCompleted.Wait(PoolWaitTimeout, TestContext.Current.CancellationToken));
		Assert.True(pool.WaitForNoOutstanding(PoolWaitTimeout));
		Assert.Equal(0, pool.InvalidReturns);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void ChaosWrappersPreserveTheBufferedPath(SerializerType serializerType)
	{
		var distributedCache = new BufferCallTrackingDistributedCache(CreateInnerDistributedCache());
		var chaosDistributedCache = new ChaosBufferDistributedCache(distributedCache);

		using var cache1 = new FusionCache(CreateFusionCacheOptions());
		cache1.SetupDistributedCache(chaosDistributedCache, new ChaosBufferSerializer((IBufferFusionCacheSerializer)TestsUtils.GetSerializer(serializerType)));

		using var cache2 = new FusionCache(CreateFusionCacheOptions());
		cache2.SetupDistributedCache(chaosDistributedCache, new ChaosBufferSerializer((IBufferFusionCacheSerializer)TestsUtils.GetSerializer(serializerType)));

		var key = CreateRandomCacheKey("chaos");
		var value = ComplexType.CreateSample();

		cache1.Set(key, value, token: TestContext.Current.CancellationToken);
		Assert.Equal(value, cache2.GetOrDefault<ComplexType>(key, token: TestContext.Current.CancellationToken));

		Assert.Equal(1, distributedCache.BufferSetCount(key));
		Assert.Equal(1, distributedCache.BufferGetCount(key));
		Assert.Equal(0, distributedCache.ClassicSetCount(key));
		Assert.Equal(0, distributedCache.ClassicGetCount(key));
	}
}
