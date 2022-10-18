using System;
using System.Collections;
using System.Collections.Generic;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests
{
	public enum SerializerType
	{
		NewtonsoftJson = 0,
		SystemTextJson = 1
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
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
