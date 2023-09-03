namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// The type of action for a backplane message.
/// </summary>
public enum BackplaneMessageAction : byte
{
	/// <summary>
	/// Unknown action.
	/// </summary>
	Unknown = 0,
	/// <summary>
	/// A cache entry has been set (via either a Set() or a GetOrSet() method call).
	/// </summary>
	EntrySet = 1,
	/// <summary>
	/// A cache entry has been removed (via a Remove() method call).
	/// </summary>
	EntryRemove = 2,
	/// <summary>
	/// A cache entry has been manually expired (via an Expire() method call).
	/// </summary>
	EntryExpire = 3,
	/// <summary>
	/// A cache entry has been manually expired (via an Expire() method call).
	/// </summary>
	EntrySentinel = 4
}
