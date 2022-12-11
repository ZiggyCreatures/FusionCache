using System;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests
{
	public class SerializationTests
	{
		private static readonly string SampleString = "Supercalifragilisticexpialidocious";

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

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public async Task LoopFailsWithIncompatibleTypesAsync(SerializerType serializerType)
		//{
		//	var serializer = TestsUtils.GetSerializer(serializerType);
		//	await Assert.ThrowsAnyAsync<Exception>(async () =>
		//	{
		//		var data = await serializer.SerializeAsync("sloths, sloths everywhere");
		//		var res = await serializer.DeserializeAsync<int>(data);
		//	});
		//}

		//[Theory]
		//[ClassData(typeof(SerializerTypesClassData))]
		//public void LoopFailsWithIncompatibleTypes(SerializerType serializerType)
		//{
		//	var serializer = TestsUtils.GetSerializer(serializerType);
		//	Assert.ThrowsAny<Exception>(() =>
		//	{
		//		var data = serializer.Serialize("sloths, sloths everywhere");
		//		var res = serializer.Deserialize<int>(data);
		//	});
		//}

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
			var obj = new FusionCacheDistributedEntry<string>(SampleString, new FusionCacheEntryMetadata(DateTimeOffset.UtcNow.AddSeconds(10), true));

			var data = await serializer.SerializeAsync(obj);

			Assert.NotNull(data);
			Assert.NotEmpty(data);

			var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data);
			Assert.NotNull(looped);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void LoopSucceedsWithDistributedEntryAndSimpleTypes(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			var obj = new FusionCacheDistributedEntry<string>(SampleString, new FusionCacheEntryMetadata(DateTimeOffset.UtcNow.AddSeconds(10), true));

			var data = serializer.Serialize(obj);

			Assert.NotNull(data);
			Assert.NotEmpty(data);

			var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
			Assert.NotNull(looped);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task LoopSucceedsWithDistributedEntryAndNoMetadataAsync(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			var obj = new FusionCacheDistributedEntry<string>(SampleString, null);

			var data = await serializer.SerializeAsync(obj);

			Assert.NotNull(data);
			Assert.NotEmpty(data);

			var looped = await serializer.DeserializeAsync<FusionCacheDistributedEntry<string>>(data);
			Assert.NotNull(looped);
			Assert.Equal(SampleString, looped!.Value);
			Assert.Null(looped!.Metadata);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void LoopSucceedsWithDistributedEntryAndNoMetadata(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			var obj = new FusionCacheDistributedEntry<string>(SampleString, null);

			var data = serializer.Serialize(obj);

			Assert.NotNull(data);
			Assert.NotEmpty(data);

			var looped = serializer.Deserialize<FusionCacheDistributedEntry<string>>(data);
			Assert.NotNull(looped);
			Assert.Equal(SampleString, looped!.Value);
			Assert.Null(looped!.Metadata);
		}
	}
}
