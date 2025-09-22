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
	private static async Task<T?> LoopDeLoopAsync<T>(IFusionCacheSerializer serializer, T? obj)
	{
		var data = await serializer.SerializeAsync(obj);
		return await serializer.DeserializeAsync<T>(data);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithSimpleTypesAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = await LoopDeLoopAsync(serializer, SampleString);
		Assert.Equal(SampleString, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithComplexTypesAsync(SerializerType serializerType)
	{
		var data = ComplexType.CreateSample();
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = await LoopDeLoopAsync(serializer, data);
		Assert.Equal(data, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithComplexTypesArrayAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = await LoopDeLoopAsync(serializer, BigData);
		Assert.Equal(BigData, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopDoesNotFailWithNullAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = await LoopDeLoopAsync<string>(serializer, null);
		Assert.Null(looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithDistributedEntryAndSimpleTypesAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, now.UtcTicks, now.AddSeconds(10).UtcTicks, [], new FusionCacheEntryMetadata(true, now.AddSeconds(9).UtcTicks, "abc123", now.UtcTicks, 123, 1));

		var data = await serializer.SerializeAsync(obj, TestContext.Current.CancellationToken);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data, TestContext.Current.CancellationToken);
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
	public async Task LoopSucceedsWithDistributedEntryAndNoMetadataAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, now.UtcTicks, now.AddSeconds(10).UtcTicks, [], null);

		var data = await serializer.SerializeAsync(obj, TestContext.Current.CancellationToken);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data, TestContext.Current.CancellationToken);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.LogicalExpirationTimestamp, looped.LogicalExpirationTimestamp);
		Assert.Null(looped.Metadata);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithDistributedEntryAndComplexTypesAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<ComplexType>(ComplexType.CreateSample(), now.UtcTicks, now.AddSeconds(10).AddMicroseconds(now.Nanosecond * -1).UtcTicks, [], new FusionCacheEntryMetadata(true, now.AddSeconds(9).AddMicroseconds(now.Microsecond * -1).UtcTicks, "abc123", now.AddMicroseconds(now.Microsecond * -1).UtcTicks, 123, 1));

		var data = await serializer.SerializeAsync(obj, TestContext.Current.CancellationToken);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<ComplexType>>(data, TestContext.Current.CancellationToken);
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
	public async Task CanWorkWithByteArraysAsync(SerializerType serializerType)
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

		var serializedData = await serializer.SerializeAsync(sourceEntry, TestContext.Current.CancellationToken);
		logger.LogInformation("SERIALIZED DATA: {bytes} bytes (+{delta} bytes)", serializedData.Length, serializedData.Length - sourceData.Length);

		var targetEntry = await serializer.DeserializeAsync<FusionCacheDistributedEntry<byte[]>>(serializedData, TestContext.Current.CancellationToken);
		logger.LogInformation("TARGET DATA: {bytes} bytes", targetEntry!.Value.Length);

		Assert.Equal(sourceData, targetEntry.Value);
	}

	//[Theory]
	//[ClassData(typeof(SerializerTypesClassData))]
	//public async Task CanDeserializeOldSnapshotsAsync(SerializerType serializerType)
	//{
	//	var serializer = TestsUtils.GetSerializer(serializerType);

	//	var assembly = serializer.GetType().Assembly;
	//	var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
	//	string? currentVersion = fvi.FileVersion![..fvi.FileVersion!.LastIndexOf('.')];

	//	var filePrefix = $"{serializer.GetType().Name}__";

	//	var files = Directory.GetFiles("Snapshots", filePrefix + "*.bin");

	//	TestOutput.WriteLine($"Found {files.Length} snapshots for {serializer.GetType().Name}");

	//	foreach (var file in files)
	//	{
	//		var payloadVersion = __re_VersionExtractor.Match(file).Groups[1].Value.Replace('_', '.');

	//		var payload = File.ReadAllBytes(file);
	//		var deserialized = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(payload);
	//		Assert.False(deserialized is null, $"Failed deserializing payload from v{payloadVersion}");

	//		TestOutput.WriteLine($"Correctly deserialized payload from v{payloadVersion} to v{currentVersion} (current) using {serializer.GetType().Name}");
	//	}
	//}

	//[Theory]
	//[ClassData(typeof(SerializerTypesClassData))]
	//public void CanDeserializeOldSnapshots(SerializerType serializerType)
	//{
	//	var serializer = TestsUtils.GetSerializer(serializerType);

	//	var assembly = serializer.GetType().Assembly;
	//	var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
	//	string? currentVersion = fvi.FileVersion![..fvi.FileVersion!.LastIndexOf('.')];

	//	var filePrefix = $"{serializer.GetType().Name}__";

	//	var files = Directory.GetFiles("Snapshots", filePrefix + "*.bin");

	//	TestOutput.WriteLine($"Found {files.Length} snapshots for {serializer.GetType().Name}");

	//	foreach (var file in files)
	//	{
	//		var payloadVersion = __re_VersionExtractor.Match(file).Groups[1].Value.Replace('_', '.');

	//		var payload = File.ReadAllBytes(file);
	//		var deserialized = serializer.Deserialize<FusionCacheDistributedEntry<string>>(payload);
	//		Assert.False(deserialized is null, $"Failed deserializing payload from v{payloadVersion}");

	//		TestOutput.WriteLine($"Correctly deserialized payload from v{payloadVersion} to v{currentVersion} (current) using {serializer.GetType().Name}");
	//	}
	//}

	//[GeneratedRegex(@"\w+__v(\d+_\d+_\d+)_\d+\.bin", RegexOptions.Compiled)]
	//private static partial Regex VersionExtractorRegEx();
}
