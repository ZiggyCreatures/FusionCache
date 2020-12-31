using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace ZiggyCreatures.FusionCaching.Serialization.NewtonsoftJson
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the Newtonsoft Json.NET serializer.
	/// </summary>
	public class FusionCacheNewtonsoftJsonSerializer
		: IFusionCacheSerializer
	{

		/// <summary>
		/// Create a new instance of a <see cref="FusionCacheNewtonsoftJsonSerializer"/> object.
		/// </summary>
		/// <param name="settings">The optional <see cref="JsonSerializerSettings"/> object to use.</param>
		public FusionCacheNewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
		{
			Settings = settings;
		}

		private JsonSerializerSettings? Settings;

		/// <inheritdoc />
		public byte[] Serialize<T>(T obj)
		{
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
			return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Settings));
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning restore CS8604 // Possible null reference argument.
		}

		/// <inheritdoc />
		public T Deserialize<T>(byte[] data)
		{
#pragma warning disable CS8603 // Possible null reference return.
			return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data), Settings);
#pragma warning restore CS8603 // Possible null reference return.
		}

		/// <inheritdoc />
		public Task<byte[]> SerializeAsync<T>(T obj)
		{
			// TODO: DO BETTER HERE
			return Task.FromResult(Serialize<T>(obj));
		}

		/// <inheritdoc />
		public Task<T> DeserializeAsync<T>(byte[] data)
		{
			// TODO: DO BETTER HERE
			return Task.FromResult(Deserialize<T>(data));
		}
	}

}