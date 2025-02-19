using System.Collections.Concurrent;

namespace FusionCacheTests.Stuff;

public class EntryActionsStats
{
	public EntryActionsStats()
	{
		Data = new ConcurrentDictionary<EntryActionKind, int>();
		foreach (EntryActionKind kind in Enum.GetValues(typeof(EntryActionKind)))
		{
			Data[kind] = 0;
		}
	}

	public ConcurrentDictionary<EntryActionKind, int> Data { get; }
	public void RecordAction(EntryActionKind kind)
	{
		Data.AddOrUpdate(kind, 1, (_, x) => x + 1);
	}
}
