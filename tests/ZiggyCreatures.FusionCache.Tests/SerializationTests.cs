using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests
{
	public class SerializationTests
	{
		private static readonly string SampleString = "Supercalifragilisticexpialidocious";
		private static readonly SampleComplexObject SampleObject = SampleComplexObject.CreateRandom();

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
		public async Task LoopSucceedsAsync(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			var looped = await LoopDeLoopAsync(serializer, SampleString);
			Assert.Equal(SampleString, looped);
		}

		[Theory]
		[ClassData(typeof(SerializerTypesClassData))]
		public void LoopSucceeds(SerializerType serializerType)
		{
			var serializer = TestsUtils.GetSerializer(serializerType);
			var looped = LoopDeLoop(serializer, SampleString);
			Assert.Equal(SampleString, looped);
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
