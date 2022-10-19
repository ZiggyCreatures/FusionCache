using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using ZiggyCreatures.FusionCache.Serialization.NeueccMessagePack;

namespace FusionCacheTests
{
	public enum SerializerType
	{
		NewtonsoftJson = 0,
		SystemTextJson = 1,
		NeueccMessagePack = 2
	}

	public static class TestsUtils
	{
		public static IFusionCacheSerializer GetSerializer(SerializerType serializerType)
		{
			switch (serializerType)
			{
				case SerializerType.NewtonsoftJson:
					return new FusionCacheNewtonsoftJsonSerializer();
				case SerializerType.SystemTextJson:
					return new FusionCacheSystemTextJsonSerializer();
				case SerializerType.NeueccMessagePack:
					return new FusionCacheNeueccMessagePackSerializer(MessagePack.Resolvers.ContractlessStandardResolver.Options);
				default:
					throw new ArgumentException("Invalid serializer specified", nameof(serializerType));
			}
		}
	}

	public class SerializerTypesClassData : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			yield return new object[] { SerializerType.NewtonsoftJson };
			yield return new object[] { SerializerType.SystemTextJson };
			yield return new object[] { SerializerType.NeueccMessagePack };
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
