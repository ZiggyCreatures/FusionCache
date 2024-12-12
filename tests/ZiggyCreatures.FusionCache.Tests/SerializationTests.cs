using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FusionCacheTests.Stuff;
using Xunit;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public partial class SerializationTests
	: AbstractTests
{
	public SerializationTests(ITestOutputHelper output)
			: base(output, null)
	{
	}

	private static readonly Regex __re_VersionExtractor = VersionExtractorRegEx();

	private const string SampleString = "Supercalifragilisticexpialidocious";

	private static T? LoopDeLoop<T>(IFusionCacheSerializer serializer, T? obj)
	{
		var data = serializer.Serialize(obj);
		return serializer.Deserialize<T>(data);
	}

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
	public void LoopSucceedsWithSimpleTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, SampleString);
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
	public void LoopSucceedsWithComplexTypes(SerializerType serializerType)
	{
		var data = ComplexType.CreateSample();
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, data);
		Assert.Equal(data, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWitComplexTypesArrayAsync(SerializerType serializerType)
	{
		var data = new ComplexType[1024 * 1024];
		for(int i = 0; i < data.Length; i++)
		{
			data[i] = ComplexType.CreateSample();
		}

		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = await LoopDeLoopAsync(serializer, data);
		Assert.Equal(data, looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithComplexTypesArray(SerializerType serializerType)
	{
		var data = new ComplexType[1024 * 1024];
		for (int i = 0; i < data.Length; i++)
		{
			data[i] = ComplexType.CreateSample();
		}

		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop(serializer, data);
		Assert.Equal(data, looped);
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
	public void LoopDoesNotFailWithNull(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var looped = LoopDeLoop<string>(serializer, null);
		Assert.Null(looped);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithDistributedEntryAndSimpleTypesAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, [], new FusionCacheEntryMetadata(now.AddSeconds(10), true, now.AddSeconds(9), "abc123", now, 123), FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = await serializer.SerializeAsync(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.Metadata!.IsFromFailSafe, looped.Metadata!.IsFromFailSafe);
		Assert.Equal(obj.Metadata!.LogicalExpiration, looped.Metadata!.LogicalExpiration);
		Assert.Equal(obj.Metadata!.EagerExpiration, looped.Metadata!.EagerExpiration);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModified, looped.Metadata!.LastModified);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndSimpleTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<string>(SampleString, [], new FusionCacheEntryMetadata(now.AddSeconds(10), true, now.AddSeconds(9), "abc123", now, 123), FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.Metadata!.IsFromFailSafe, looped.Metadata!.IsFromFailSafe);
		Assert.Equal(obj.Metadata!.LogicalExpiration, looped.Metadata!.LogicalExpiration);
		Assert.Equal(obj.Metadata!.EagerExpiration, looped.Metadata!.EagerExpiration);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModified, looped.Metadata!.LastModified);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithDistributedEntryAndNoMetadataAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var obj = new FusionCacheDistributedEntry<string>(SampleString, [], null, FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = await serializer.SerializeAsync(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Null(looped!.Metadata);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndNoMetadata(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var obj = new FusionCacheDistributedEntry<string>(SampleString, [], null, FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Null(looped!.Metadata);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task LoopSucceedsWithDistributedEntryAndComplexTypesAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<ComplexType>(ComplexType.CreateSample(), [], new FusionCacheEntryMetadata(now.AddSeconds(10), true, now.AddSeconds(9), "abc123", now, 123), FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = await serializer.SerializeAsync(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<ComplexType>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.Metadata!.IsFromFailSafe, looped.Metadata!.IsFromFailSafe);
		Assert.Equal(obj.Metadata!.LogicalExpiration, looped.Metadata!.LogicalExpiration);
		Assert.Equal(obj.Metadata!.EagerExpiration, looped.Metadata!.EagerExpiration);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModified, looped.Metadata!.LastModified);
		Assert.Equal(obj.Metadata!.Size, looped.Metadata!.Size);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void LoopSucceedsWithDistributedEntryAndComplexTypes(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);
		var now = DateTimeOffset.UtcNow;
		var obj = new FusionCacheDistributedEntry<ComplexType>(ComplexType.CreateSample(), [], new FusionCacheEntryMetadata(now.AddSeconds(10).AddMicroseconds(now.Nanosecond * -1), true, now.AddSeconds(9).AddMicroseconds(now.Microsecond * -1), "abc123", now.AddMicroseconds(now.Microsecond * -1), 123), FusionCacheInternalUtils.GetCurrentTimestamp());

		var data = serializer.Serialize(obj);

		Assert.NotNull(data);
		Assert.NotEmpty(data);

		var looped = serializer.Deserialize<FusionCacheDistributedEntry<ComplexType>>(data);
		Assert.NotNull(looped);
		Assert.Equal(obj.Value, looped.Value);
		Assert.Equal(obj.Timestamp, looped.Timestamp);
		Assert.Equal(obj.Metadata!.IsFromFailSafe, looped.Metadata!.IsFromFailSafe);
		Assert.Equal(obj.Metadata!.LogicalExpiration, looped.Metadata!.LogicalExpiration);
		Assert.Equal(obj.Metadata!.EagerExpiration, looped.Metadata!.EagerExpiration);
		Assert.Equal(obj.Metadata!.ETag, looped.Metadata!.ETag);
		Assert.Equal(obj.Metadata!.LastModified, looped.Metadata!.LastModified);
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public async Task CanDeserializeOldSnapshotsAsync(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);

		var assembly = serializer.GetType().Assembly;
		var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
		string? currentVersion = fvi.FileVersion![..fvi.FileVersion!.LastIndexOf('.')];

		var filePrefix = $"{serializer.GetType().Name}__";

		var files = Directory.GetFiles("Snapshots", filePrefix + "*.bin");

		TestOutput.WriteLine($"Found {files.Length} snapshots for {serializer.GetType().Name}");

		foreach (var file in files)
		{
			var payloadVersion = __re_VersionExtractor.Match(file).Groups[1].Value.Replace('_', '.');

			var payload = File.ReadAllBytes(file);
			var deserialized = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(payload);
			Assert.False(deserialized is null, $"Failed deserializing payload from v{payloadVersion}");

			TestOutput.WriteLine($"Correctly deserialized payload from v{payloadVersion} to v{currentVersion} (current) using {serializer.GetType().Name}");
		}
	}

	[Theory]
	[ClassData(typeof(SerializerTypesClassData))]
	public void CanDeserializeOldSnapshots(SerializerType serializerType)
	{
		var serializer = TestsUtils.GetSerializer(serializerType);

		var assembly = serializer.GetType().Assembly;
		var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
		string? currentVersion = fvi.FileVersion![..fvi.FileVersion!.LastIndexOf('.')];

		var filePrefix = $"{serializer.GetType().Name}__";

		var files = Directory.GetFiles("Snapshots", filePrefix + "*.bin");

		TestOutput.WriteLine($"Found {files.Length} snapshots for {serializer.GetType().Name}");

		foreach (var file in files)
		{
			var payloadVersion = __re_VersionExtractor.Match(file).Groups[1].Value.Replace('_', '.');

			var payload = File.ReadAllBytes(file);
			var deserialized = serializer.Deserialize<FusionCacheDistributedEntry<string>>(payload);
			Assert.False(deserialized is null, $"Failed deserializing payload from v{payloadVersion}");

			TestOutput.WriteLine($"Correctly deserialized payload from v{payloadVersion} to v{currentVersion} (current) using {serializer.GetType().Name}");
		}
	}

	[GeneratedRegex(@"\w+__v(\d+_\d+_\d+)_\d+\.bin", RegexOptions.Compiled)]
	private static partial Regex VersionExtractorRegEx();
}
