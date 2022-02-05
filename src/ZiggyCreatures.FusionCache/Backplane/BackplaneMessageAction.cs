namespace ZiggyCreatures.Caching.Fusion.Backplane
{
	/// <summary>
	/// The type of action for a backplane message.
	/// </summary>
	public enum BackplaneMessageAction
	{
		/// <summary>
		/// A cache entry has been set (via either a Set or a GetOrSet method call).
		/// </summary>
		EntrySet = 0,
		/// <summary>
		/// A cache entry has been removed (via a Remove method call).
		/// </summary>
		EntryRemove = 1
	}
}
