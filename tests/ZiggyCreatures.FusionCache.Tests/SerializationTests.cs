using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests
{
	public class SerializationTests
	{
		private static readonly string SampleString = "Supercalifragilisticexpialidocious";

		private T LoopDeLoop<T>(IFusionCacheSerializer serializer, T obj)
		{
			return serializer.Deserialize<T>(serializer.Serialize(obj));
		}

		private async Task<T> LoopDeLoopAsync<T>(IFusionCacheSerializer serializer, T obj)
		{
			return await serializer.DeserializeAsync<T>(await serializer.SerializeAsync(obj));
		}

		[Fact]
		public async Task NewtonsoftJsonSerializationLoopSucceedsAsync()
		{
			var serializer = new FusionCacheNewtonsoftJsonSerializer();
			var looped = await LoopDeLoopAsync(serializer, SampleString);
			Assert.Equal(SampleString, looped);
		}

		[Fact]
		public void NewtonsoftJsonSerializationLoopSucceeds()
		{
			var serializer = new FusionCacheNewtonsoftJsonSerializer();
			var looped = LoopDeLoop(serializer, SampleString);
			Assert.Equal(SampleString, looped);
		}

		[Fact]
		public async Task SystemTextJsonSerializationLoopSucceedsAsync()
		{
			var serializer = new FusionCacheSystemTextJsonSerializer();
			var looped = await LoopDeLoopAsync(serializer, SampleString);
			Assert.Equal(SampleString, looped);
		}

		[Fact]
		public void SystemTextJsonSerializationLoopSucceeds()
		{
			var serializer = new FusionCacheSystemTextJsonSerializer();
			var looped = LoopDeLoop(serializer, SampleString);
			Assert.Equal(SampleString, looped);
		}

		[Fact]
		public async Task NewtonsoftJsonSerializationLoopDoesNotFailWithNullAsync()
		{
			var serializer = new FusionCacheNewtonsoftJsonSerializer();
			var looped = await LoopDeLoopAsync<string>(serializer, null);
			Assert.Null(looped);
		}

		[Fact]
		public void NewtonsoftJsonSerializationLoopDoesNotFailWithNull()
		{
			var serializer = new FusionCacheNewtonsoftJsonSerializer();
			var looped = LoopDeLoop<string>(serializer, null);
			Assert.Null(looped);
		}

		[Fact]
		public async Task SystemTextJsonSerializationLoopDoesNotFailWithNullAsync()
		{
			var serializer = new FusionCacheSystemTextJsonSerializer();
			var looped = await LoopDeLoopAsync<string>(serializer, null);
			Assert.Null(looped);
		}

		[Fact]
		public void SystemTextJsonSerializationLoopDoesNotFailWithNull()
		{
			var serializer = new FusionCacheSystemTextJsonSerializer();
			var looped = LoopDeLoop<string>(serializer, null);
			Assert.Null(looped);
		}
	}
}
