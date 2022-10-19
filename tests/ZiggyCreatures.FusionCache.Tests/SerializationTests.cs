using System;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests
{
	public class SerializationTests
	{
		private static readonly string SampleString = "Supercalifragilisticexpialidocious";

		private static T? LoopDeLoop<T>(IFusionCacheSerializer serializer, T? obj)
		{
			return serializer.Deserialize<T>(serializer.Serialize(obj));
		}

		private static async Task<T?> LoopDeLoopAsync<T>(IFusionCacheSerializer serializer, T? obj)
		{
			return await serializer.DeserializeAsync<T>(await serializer.SerializeAsync(obj));
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
		public async Task LoopSucceedsWithNonSimpleTypesAsync(SerializerType serializerType)
		{
			var data = ComplexType.CreateSample();
			var serializer = TestsUtils.GetSerializer(serializerType);
			var looped = await LoopDeLoopAsync(serializer, data);
			Assert.Equal(data, looped);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void LoopSucceedsWithNonSimpleTypes(SerializerType serializerType)
		{
			var data = ComplexType.CreateSample();
			var serializer = TestsUtils.GetSerializer(serializerType);
			var looped = LoopDeLoop(serializer, data);
			Assert.Equal(data, looped);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public async Task LoopFailsWithIncompatibleTypesAsync(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			await Assert.ThrowsAnyAsync<Exception>(async () =>
			{
				await serializer.DeserializeAsync<int>(await serializer.SerializeAsync("sloths, sloths everywhere"));
			});
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void LoopFailsWithIncompatibleTypes(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			Assert.ThrowsAny<Exception>(() =>
			{
				serializer.Deserialize<int>(serializer.Serialize("sloths, sloths everywhere"));
			});
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
	}
}
