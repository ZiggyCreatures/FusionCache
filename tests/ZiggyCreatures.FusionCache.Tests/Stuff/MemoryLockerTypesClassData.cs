using System.Collections;

namespace FusionCacheTests.Stuff;

public class MemoryLockerTypesClassData : IEnumerable<object[]>
{
	public IEnumerator<object[]> GetEnumerator()
	{
		foreach (var x in Enum.GetValues<MemoryLockerType>())
		{
			yield return [x];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
