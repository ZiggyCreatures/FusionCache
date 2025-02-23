using System.Collections;

namespace FusionCacheTests.Stuff;

public class CacheStampedeClassData : IEnumerable<object?[]>
{
	private static readonly int[] AccessorsCounts = [
		10,
		100,
		1_000
	];

	public IEnumerator<object?[]> GetEnumerator()
	{
		var serializerTypes = new List<SerializerType?> { null };
		foreach (var serializerType in Enum.GetValues<SerializerType>())
		{
			serializerTypes.Add(serializerType);
		}

		foreach (var serializerType in serializerTypes)
		{
			foreach (var memoryLockerType in Enum.GetValues<MemoryLockerType>())
			{
				foreach (var accessorsCount in AccessorsCounts)
				{
					yield return [serializerType, memoryLockerType, accessorsCount];
				}
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
