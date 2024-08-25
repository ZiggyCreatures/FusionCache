using System;
using System.Collections;
using System.Collections.Generic;

namespace FusionCacheTests.Stuff;

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
