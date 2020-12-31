using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ZiggyCreatures.FusionCaching.Serialization;

namespace FusionCaching.Serialization.SystemTextJson
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the System.Text.Json serializer.
	/// </summary>
	public class FusionCacheSystemTextJsonSerializer
		: IFusionCacheSerializer
	{

		/// <summary>
		/// Create a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
		/// </summary>
		/// <param name="options">The optional <see cref="JsonSerializerOptions"/> object to use.</param>
		public FusionCacheSystemTextJsonSerializer(JsonSerializerOptions? options = null)
		{
			Options = options;
		}

		JsonSerializerOptions? Options;

		/// <inheritdoc />
		public byte[] Serialize<T>(T obj)
		{
			return JsonSerializer.SerializeToUtf8Bytes<T>(obj, Options);
		}

		/// <inheritdoc />
		public T Deserialize<T>(byte[] data)
		{
			return JsonSerializer.Deserialize<T>(data, Options);
		}

		/// <inheritdoc />
		public async Task<byte[]> SerializeAsync<T>(T obj)
		{
			using (var stream = new MemoryStream())
			{
				await JsonSerializer.SerializeAsync<T>(stream, obj, Options);
				return stream.ToArray();
			}
		}

		/// <inheritdoc />
		public async Task<T> DeserializeAsync<T>(byte[] data)
		{
			using (var stream = new MemoryStream(data))
			{
				return await JsonSerializer.DeserializeAsync<T>(stream, Options);
			}
		}

	}

}