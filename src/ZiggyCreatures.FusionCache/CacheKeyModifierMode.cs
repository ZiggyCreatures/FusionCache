namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The mode in which a cache modifier should be used to produce a cache key.
/// </summary>
public enum CacheKeyModifierMode
{
	/// <summary>
	/// The cache modifier will be prepended, plus a separator.
	/// </summary>
	Prefix = 0,
	/// <summary>
	/// The cache modifier will be appended, plus a separator.
	/// </summary>
	Suffix = 1,
	/// <summary>
	/// The cache modifier will not be prepended.
	/// </summary>
	None = 2
}
