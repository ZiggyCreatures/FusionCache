using System;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public partial class SerializationTests
	: AbstractTests
{
	private static T? LoopDeLoop<T>(IFusionCacheSerializer serializer, T? obj)
	{
		var data = serializer.Serialize(obj);
		return serializer.Deserialize<T>(data);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithSimpleTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, SampleString);
		Assert.Equal(SampleString, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithComplexTypes(SerializerType serializerType)
	{
		var data = ComplexType.CreateSample();
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, data);
		Assert.Equal(data, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithComplexTypesArray(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, BigData);
		Assert.Equal(BigData, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopDoesNotFailWithNull(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop<string>(serializer, null);
		Assert.Null(looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndSimpleTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, now.UtcTicks, now.AddSeconds(10).UtcTicks, [], new FusionCacheEntryMetadata(true, now.AddSeconds(9).UtcTicks, "abc123", now.UtcTicks, 123, 1));

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.LogicalExpirationTimestamp, looped.LogicalExpirationTimestamp);
		Assert.Equal(obj.Metadata!.IsStale, looped.Metadata!.IsStale);
		Assert.Equal(obj.Metadata!.EagerExpirationTimestamp, looped.Metadata!.EagerExpirationTimestamp);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModifiedTimestamp, looped.Metadata!.LastModifiedTimestamp);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
		Assert.Equal(obj.Metadata!.Priority, looped.Metadata!.Priority);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndNoMetadata(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, now.UtcTicks, now.AddSeconds(10).UtcTicks, [], null);

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.LogicalExpirationTimestamp, looped.LogicalExpirationTimestamp);
		Assert.Null(looped!.Metadata);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndComplexTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<ComplexType>(ComplexType.CreateSample(), now.UtcTicks, now.AddSeconds(10).AddMicroseconds(now.Nanosecond * -1).UtcTicks, [], new FusionCacheEntryMetadata(true, now.AddSeconds(9).AddMicroseconds(now.Microsecond * -1).UtcTicks, "abc123", now.AddMicroseconds(now.Microsecond * -1).UtcTicks, 123, 1));

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<ComplexType>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.LogicalExpirationTimestamp, looped.LogicalExpirationTimestamp);
		Assert.Equal(obj.Metadata!.IsStale, looped.Metadata!.IsStale);
		Assert.Equal(obj.Metadata!.EagerExpirationTimestamp, looped.Metadata!.EagerExpirationTimestamp);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModifiedTimestamp, looped.Metadata!.LastModifiedTimestamp);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
		Assert.Equal(obj.Metadata!.Priority, looped.Metadata!.Priority);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanWorkWithByteArrays(SerializerType serializerType)
	{
		var logger = CreateXUnitLogger<bool>();
		var serializer = TestsUtils.GetSerializer(serializerType);

		var random = new Random(123456);

		var sourceData = new byte[100_000];
		random.NextBytes(sourceData);

		logger.LogInformation("SOURCE DATA: {bytes} bytes", sourceData.Length);

		var sourceEntry = new FusionCacheDistributedEntry<byte[]>(
			sourceData,
			DateTimeOffset.UtcNow.UtcTicks,
			DateTimeOffset.UtcNow.AddSeconds(10).UtcTicks,
			null,
			null
		);

		var serializedData = serializer.Serialize(sourceEntry);
		logger.LogInformation("SERIALIZED DATA: {bytes} bytes (+{delta} bytes)", serializedData.Length, serializedData.Length - sourceData.Length);

		var targetEntry = serializer.Deserialize<FusionCacheDistributedEntry<byte[]>>(serializedData);
		logger.LogInformation("TARGET DATA: {bytes} bytes", targetEntry!.Value.Length);

		Assert.Equal(sourceData, targetEntry.Value);
	}
}
