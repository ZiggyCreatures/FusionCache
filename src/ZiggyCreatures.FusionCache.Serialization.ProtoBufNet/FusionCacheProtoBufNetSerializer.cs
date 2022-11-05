using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf.Meta;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses protobuf-net, one of the most used .NET Protobuf serializer, by Marc Gravell.
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

			RegisterMetadataModel();
		}

		private static readonly ConcurrentDictionary<RuntimeTypeModel, HashSet<Type>> _modelsCache = new ConcurrentDictionary<RuntimeTypeModel, HashSet<Type>>();
		private static readonly Type _metadataType = typeof(FusionCacheEntryMetadata);
		private static readonly Type _distributedEntryOpenGenericType = typeof(FusionCacheDistributedEntry<>);

		private readonly RuntimeTypeModel _model;

		private void RegisterMetadataModel()
		{
			if (_modelsCache.TryGetValue(_model, out var tmp) == false)
			{
				lock (_modelsCache)
				{
					tmp = _modelsCache.GetOrAdd(_model, _ => new HashSet<Type>());
				}
			}

			// ENSURE MODEL REGISTRATION FOR FusionCacheEntryMetadata
			if (tmp.Contains(_metadataType))
				return;

			lock (tmp)
			{
				if (tmp.Contains(_metadataType))
					return;

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
			}
		}

		private void MaybeRegisterDistributedEntryModel<T>()
		{
			var t = typeof(T);

			if (t.IsGenericType == false || t.GetGenericTypeDefinition() != _distributedEntryOpenGenericType)
				return;

			if (_modelsCache.TryGetValue(_model, out var tmp) == false)
			{
				lock (_modelsCache)
				{
					tmp = _modelsCache.GetOrAdd(_model, _ => new HashSet<Type>());
				}
			}

			// ENSURE MODEL REGISTRATION FOR FusionCacheDistributedEntry<T>
			if (tmp.Contains(t))
				return;

			lock (tmp)
			{
				if (tmp.Contains(t))
					return;

				try
				{
					// NOTE: USING FusionCacheDistributedEntry<bool> HERE SINCE IT WILL
					// BE EVALUATED AT COMPILE TIME AND SO IT DOESN'T ACTUALLY MATTER
					_model.Add(t, false)
						.Add(1, nameof(FusionCacheDistributedEntry<bool>.Value))
						.Add(2, nameof(FusionCacheDistributedEntry<bool>.Metadata))
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
			MaybeRegisterDistributedEntryModel<T>();

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

			MaybeRegisterDistributedEntryModel<T>();

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
