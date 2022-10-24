﻿using System;
using System.Collections;
using System.Collections.Generic;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests
{
	public enum SerializerType
	{
		// JSON
		NewtonsoftJson = 0,
		SystemTextJson = 1,
		// MESSAGEPACK
		NeueccMessagePack = 10,
		// PROTOBUF
		ProtoBufNet = 20
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
					return new FusionCacheNeueccMessagePackSerializer();
				case SerializerType.ProtoBufNet:
					return new FusionCacheProtoBufNetSerializer();
				default:
					throw new ArgumentException("Invalid serializer specified", nameof(serializerType));
			}
		}
	}

	public class SerializerTypesClassData : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			foreach (var x in Enum.GetValues<SerializerType>())
			{
				yield return new object[] { x };
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
