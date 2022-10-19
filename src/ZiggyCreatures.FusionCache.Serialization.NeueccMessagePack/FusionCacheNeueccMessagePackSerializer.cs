using System.IO;
using System.Threading.Tasks;
using MessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses Neuecc's famous MessagePack serializer.
	/// </summary>
	public class FusionCacheNeueccMessagePackSerializer
		: IFusionCacheSerializer
	{
		/// <summary>
		/// Create a new instance of a <see cref="FusionCacheNeueccMessagePackSerializer"/> object.
		/// </summary>
		/// <param name="options">The optional <see cref="MessagePackSerializerOptions"/> object to use.</param>
		public FusionCacheNeueccMessagePackSerializer(MessagePackSerializerOptions? options = null)
		{
			//Options = options ?? MessagePack.Resolvers.ContractlessStandardResolver.Options;
			Options = options;
		}

		private readonly MessagePackSerializerOptions? Options;

		/// <inheritdoc />
		public byte[] Serialize<T>(T? obj)
		{
			return MessagePackSerializer.Serialize<T?>(obj, Options);
		}

		/// <inheritdoc />
		public T? Deserialize<T>(byte[] data)
		{
			return MessagePackSerializer.Deserialize<T?>(data, Options);
		}

		/// <inheritdoc />
		public async ValueTask<byte[]> SerializeAsync<T>(T? obj)
		{
			using (var stream = new MemoryStream())
			{
				await MessagePackSerializer.SerializeAsync<T?>(stream, obj, Options);
				return stream.ToArray();
			}
		}

		/// <inheritdoc />
		public async ValueTask<T?> DeserializeAsync<T>(byte[] data)
		{
			using (var stream = new MemoryStream(data))
			{
				return await MessagePackSerializer.DeserializeAsync<T?>(stream, Options);
			}
		}
	}
}
