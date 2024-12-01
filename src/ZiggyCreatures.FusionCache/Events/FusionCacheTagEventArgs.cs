using System;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to tag operations (eg: RemoveByTag).
/// </summary>
public class FusionCacheTagEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheTagEventArgs"/> class.
	/// </summary>
	/// <param name="tag">The cache key related to the event.</param>
	public FusionCacheTagEventArgs(string tag)
	{
		Tag = tag;
	}

	/// <summary>
	/// The cache key related to the event.
	/// </summary>
	public string Tag { get; }
}
