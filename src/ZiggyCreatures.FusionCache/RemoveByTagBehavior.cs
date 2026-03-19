namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The behavior to apply to individual entries when they have a tag that matches a previous RemoveByTag() call.
/// </summary>
public enum RemoveByTagBehavior
{
	/// <summary>
	/// Individual entries will be removed (like calling Remove()) when they have a tag that matches a previous RemoveByTag() call.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	Expire = 0,
	/// <summary>
	/// Individual entries will be removed (like calling Remove()) when they have a tag that matches a previous RemoveByTag() call.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	Remove = 1
}
