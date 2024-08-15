using System;
using System.Collections;
using System.Collections.Generic;

namespace FusionCacheTests.Stuff;

public enum SerializerType
{
	// JSON
	NewtonsoftJson = 0,
	SystemTextJson = 1,
	ServiceStackJson = 2,
	// MESSAGEPACK
	NeueccMessagePack = 10,
	// PROTOBUF
	ProtoBufNet = 20,
	// MEMORYPACK
	CysharpMemoryPack = 30,
}

public class SerializerTypesClassData : IEnumerable<object[]>
{
	public IEnumerator<object[]> GetEnumerator()
	{
		foreach (var x in Enum.GetValues<SerializerType>())
		{
			yield return [x];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
