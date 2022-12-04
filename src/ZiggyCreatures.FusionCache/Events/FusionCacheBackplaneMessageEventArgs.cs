using System;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to cache entries (eg: with a cache key).
/// </summary>
public class FusionCacheBackplaneMessageEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheBackplaneMessageEventArgs"/> class.
	/// </summary>
	/// <param name="message">The backplane message received</param>
	public FusionCacheBackplaneMessageEventArgs(BackplaneMessage message)
	{
		Message = message;
	}

	/// <summary>
	/// The backplane message received.
	/// </summary>
	public BackplaneMessage Message { get; }
}
