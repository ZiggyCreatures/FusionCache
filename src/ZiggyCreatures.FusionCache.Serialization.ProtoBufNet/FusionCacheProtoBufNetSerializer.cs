using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ProtoBuf;
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
	}

	/// <summary>
	/// Creates a new instance of the <see cref="FusionCacheProtoBufNetSerializer"/> class.
	/// </summary>
	/// <param name="model">The runtime type model to use for serialization. If null, the default model will be used.</param>
	public FusionCacheProtoBufNetSerializer(RuntimeTypeModel? model = null)
	{
		_model = model ?? RuntimeTypeModel.Default;
		_modelsCache.GetOrAdd(_model, static (model) =>
		{
			lock (model)
			{
				model.Add(typeof(FusionCacheEntryMetadata), false).SetSurrogate(typeof(FusionCacheEntryMetadataSurrogate));
				return [_metadataType];
			}
		});
	}

	/// <summary>
	/// Create a new instance of a <see cref="FusionCacheProtoBufNetSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheProtoBufNetSerializer(Options options)
		: this(options.Model)
	{
	}

	private static readonly ConcurrentDictionary<RuntimeTypeModel, HashSet<Type>> _modelsCache = [];
	private static readonly ConcurrentDictionary<Type, bool> _suitableTypeCache = [];
	private static readonly Type _metadataType = typeof(FusionCacheEntryMetadata);
	private static readonly Type _distributedEntryOpenGenericType = typeof(FusionCacheDistributedEntry<>);
	private readonly RuntimeTypeModel _model;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MaybeRegisterDistributedEntryModel<T>()
	{
		var t = typeof(T);

		var isSuitableType = _suitableTypeCache.GetOrAdd(t, static (type) => type.IsGenericType && type.GetGenericTypeDefinition() == _distributedEntryOpenGenericType);

		if (!isSuitableType)
			return;

		var tmp = _modelsCache[_model];

		// ENSURE MODEL REGISTRATION FOR FusionCacheDistributedEntry<T>
		if (tmp.Contains(t))
			return;

		lock (tmp)
		{
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
		using var stream = new ArrayPoolWritableStream();
		_model.Serialize(stream, obj);
		return stream.GetBytes();
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		if (data.Length == 0)
			return default;

		MaybeRegisterDistributedEntryModel<T>();

		return _model.Deserialize<T?>((ReadOnlySpan<byte>)data);
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

	/// <inheritdoc />
	public override string ToString() => GetType().Name;
}
