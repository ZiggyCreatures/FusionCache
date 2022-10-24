using System.IO;
using System.Threading.Tasks;
using ProtoBuf.Meta;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the most used .NET ProtoBuf serializer, by Marc Gravell.
	/// </summary>
	public class FusionCacheProtoBufNetSerializer
		: IFusionCacheSerializer
	{
		/// <summary>
		/// Create a new instance of a <see cref="FusionCacheProtoBufNetSerializer"/> object.
		/// </summary>
		public FusionCacheProtoBufNetSerializer(RuntimeTypeModel? model = null)
		{
			_model = model ?? RuntimeTypeModel.Default;

			// ENSURE MODEL REGISTRATION FOR FusionCacheEntryMetadata
			//if (_model.CanSerialize(typeof(FusionCacheEntryMetadata)) == false)
			//{
			try
			{
				_model.Add(typeof(FusionCacheEntryMetadata), false)
					.SetSurrogate(typeof(FusionCacheEntryMetadataSurrogate))
				;
			}
			catch
			{
				// EMPTY
			}
			//}
		}

		private readonly RuntimeTypeModel _model;
		private readonly object _modelLock = new object();

		private void EnsureDistributedEntryModelIsRegistered<T>()
		{
			// TODO: OPTIMIZE THIS

			var _t = typeof(T);
			if (_t.IsGenericType == false || _t.GetGenericTypeDefinition() != typeof(FusionCacheDistributedEntry<>))
				return;

			//if (_model.CanSerialize(_t))
			//	return;

			lock (_modelLock)
			{
				//if (_model.CanSerialize(_t))
				//	return;

				// ENSURE MODEL REGISTRATION FOR FusionCacheDistributedEntry<T>
				try
				{
					_model.Add(typeof(T), false)
						.Add(1, nameof(FusionCacheDistributedEntry<T>.Value))
						.Add(2, nameof(FusionCacheDistributedEntry<T>.Metadata))
					;
				}
				catch
				{
					// EMPTY
				}
			}
		}

		/// <inheritdoc />
		public byte[] Serialize<T>(T? obj)
		{
			EnsureDistributedEntryModelIsRegistered<T>();

			using (var stream = new MemoryStream())
			{
				_model.Serialize(stream, obj);
				return stream.ToArray();
			}
		}

		/// <inheritdoc />
		public T? Deserialize<T>(byte[] data)
		{
			if (data.Length == 0)
				return default(T);

			EnsureDistributedEntryModelIsRegistered<T>();

			using (var stream = new MemoryStream(data))
			{
				return _model.Deserialize<T?>(stream);
			}
		}

		/// <inheritdoc />
		public ValueTask<byte[]> SerializeAsync<T>(T? obj)
		{
			return new ValueTask<byte[]>(Serialize(obj));
		}

		/// <inheritdoc />
		public ValueTask<T?> DeserializeAsync<T>(byte[] data)
		{
			return new ValueTask<T?>(Deserialize<T>(data));
		}
	}
}
