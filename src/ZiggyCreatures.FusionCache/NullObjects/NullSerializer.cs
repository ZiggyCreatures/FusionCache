using System;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.NullObjects
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> that implements the null object pattern, meaning that it does nothing.
	/// </summary>
	public class NullSerializer
		: IFusionCacheSerializer
	{
		/// <inheritdoc/>
		public T? Deserialize<T>(byte[] data)
		{
			return default(T?);
		}

		/// <inheritdoc/>
		public ValueTask<T?> DeserializeAsync<T>(byte[] data)
		{
			return new ValueTask<T?>(default(T?));
		}

		/// <inheritdoc/>
		public byte[] Serialize<T>(T? obj)
		{
			return Array.Empty<byte>();
		}

		/// <inheritdoc/>
		public ValueTask<byte[]> SerializeAsync<T>(T? obj)
		{
			return new ValueTask<byte[]>(Array.Empty<byte>());
		}
	}
}
