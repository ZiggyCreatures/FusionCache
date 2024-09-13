using System;
using System.Collections;
using System.Collections.Generic;

namespace FusionCacheTests.Stuff;

public class CacheStampedeClassData : IEnumerable<object[]>
{
	private static readonly int[] AccessorsCounts = [
		10,
		100,
		1_000
	];

	public IEnumerator<object[]> GetEnumerator()
	{
		foreach (var memoryLockerType in Enum.GetValues<MemoryLockerType>())
		{
			foreach (var accessorsCount in AccessorsCounts)
			{
				yield return [memoryLockerType, accessorsCount];
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
