using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IO;
using ProtoBuf.Meta;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses protobuf-net, one of the most used .NET Protobuf serializer, by Marc Gravell.
/// </summary>
public class FusionCacheProtoBufNetSerializer
	: IFusionCacheSerializer
{
	/// <summary>
	/// The options class for the <see cref="FusionCacheProtoBufNetSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="RuntimeTypeModel"/> object to use.
		/// </summary>
		public RuntimeTypeModel? Model { get; set; }

		/// <summary>
		/// The optional <see cref="RecyclableMemoryStreamManager"/> object to use.
		/// </summary>
		public RecyclableMemoryStreamManager? StreamManager { get; set; }
	}

	/// <summary>
	/// Creates a new instance of the <see cref="FusionCacheProtoBufNetSerializer"/> class.
	/// </summary>
	/// <param name="model">The runtime type model to use for serialization. If null, the default model will be used.</param>
	public FusionCacheProtoBufNetSerializer(RuntimeTypeModel? model = null)
	{
		_model = model ?? RuntimeTypeModel.Default;

		RegisterMetadataModel();
	}

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheProtoBufNetSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheProtoBufNetSerializer(Options? options)
		: this(options?.Model)
	{
		_streamManager = options?.StreamManager;
	}

	private static readonly ConcurrentDictionary<RuntimeTypeModel, HashSet<Type>> _modelsCache = [];
	private static readonly Type _metadataType = typeof(FusionCacheEntryMetadata);
	private static readonly Type _distributedEntryOpenGenericType = typeof(FusionCacheDistributedEntry<>);

	private readonly RuntimeTypeModel _model;
	private readonly RecyclableMemoryStreamManager? _streamManager;

	private MemoryStream GetMemoryStream()
	{
		return _streamManager?.GetStream() ?? new MemoryStream();
	}

	private MemoryStream GetMemoryStream(byte[] buffer)
	{
		return _streamManager?.GetStream(buffer) ?? new MemoryStream(buffer);
	}

	private void RegisterMetadataModel()
	{
		if (_modelsCache.TryGetValue(_model, out var tmp) == false)
		{
			lock (_modelsCache)
			{
				tmp = _modelsCache.GetOrAdd(_model, _ => []);
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

				tmp.Add(_metadataType);
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
				tmp = _modelsCache.GetOrAdd(_model, _ => []);
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
					.Add(3, nameof(FusionCacheDistributedEntry<bool>.Timestamp))
					.Add(4, nameof(FusionCacheDistributedEntry<bool>.Tags))
				;

				tmp.Add(t);
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

		using var stream = GetMemoryStream();

		_model.Serialize(stream, obj);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		if (data.Length == 0)
			return default;

		MaybeRegisterDistributedEntryModel<T>();

		using var stream = GetMemoryStream(data);

		return _model.Deserialize<T?>(stream);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}
}
