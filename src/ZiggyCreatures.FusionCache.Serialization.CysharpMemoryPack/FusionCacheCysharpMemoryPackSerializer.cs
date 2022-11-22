using System;
using System.Reflection;
using System.Threading.Tasks;
using MemoryPack;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack.Internals;

namespace ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack
{
	/// <summary>
	/// An implementation of <see cref="IFusionCacheSerializer"/> which uses Cysharp's MemoryPack serializer.
	/// </summary>
	public class FusionCacheCysharpMemoryPackSerializer
		: IFusionCacheSerializer
	{

		private static class Check<TGenericDistributedEntry>
		{
			private static readonly Type FusionCacheDistributedEntrySurrogateType = typeof(FusionCacheDistributedEntrySurrogate<>);
			static Check()
			{
				try
				{
					var tvalueType = typeof(TGenericDistributedEntry).GetGenericArguments()[0];
					var b = FusionCacheDistributedEntrySurrogateType.MakeGenericType(tvalueType).GetMethod(nameof(FusionCacheDistributedEntrySurrogate<bool>.RegisterFormatter), BindingFlags.Static | BindingFlags.Public);
					b?.Invoke(null, null);
				}
				catch
				{
					// EMPTY
				}
			}

			public static void Ensure()
			{
				// EMPTY
			}
		}

		/// <summary>
		/// Create a new instance of a <see cref="FusionCacheCysharpMemoryPackSerializer"/> object.
		/// </summary>
		/// <param name="options">The <see cref="MemoryPackSerializerOptions"/> to use, or <see langword="null"/></param>
		public FusionCacheCysharpMemoryPackSerializer(MemoryPackSerializerOptions? options = null)
		{
			Options = options;

			FusionCacheEntryMetadataSurrogate.RegisterFormatter();
		}

		private static readonly Type _distributedEntryOpenGenericType = typeof(FusionCacheDistributedEntry<>);

		private readonly MemoryPackSerializerOptions? Options;

		private void MaybeRegisterDistributedEntryFormatter<T>()
		{
			var t = typeof(T);

			if (t.IsGenericType == false || t.GetGenericTypeDefinition() != _distributedEntryOpenGenericType)
				return;

			Check<T>.Ensure();
		}

		//private static void TryRegisterDistributedEntryFormatter<TValue>()
		//{
		//	try
		//	{
		//		if (MemoryPackFormatterProvider.IsRegistered<FusionCacheDistributedEntry<TValue>>() == false)
		//		{
		//			var formatter = new FusionCacheDistributedEntryFormatter<TValue>();
		//			MemoryPackFormatterProvider.Register(formatter);
		//		}

		//		if (MemoryPackFormatterProvider.IsRegistered<FusionCacheDistributedEntry<TValue>[]>() == false)
		//		{
		//			MemoryPackFormatterProvider.Register(new ArrayFormatter<FusionCacheDistributedEntry<TValue>>());
		//		}
		//	}
		//	catch
		//	{
		//		// EMPTY
		//	}
		//}

		/// <inheritdoc />
		public byte[] Serialize<T>(T? obj)
		{
			MaybeRegisterDistributedEntryFormatter<T>();

			return MemoryPackSerializer.Serialize<T>(obj, Options);
		}

		/// <inheritdoc />
		public T? Deserialize<T>(byte[] data)
		{
			MaybeRegisterDistributedEntryFormatter<T>();

			return MemoryPackSerializer.Deserialize<T?>(data, Options);
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
