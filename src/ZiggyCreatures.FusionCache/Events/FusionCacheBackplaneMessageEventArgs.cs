using System;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to backplane messages, either published or received.
/// </summary>
public class FusionCacheBackplaneMessageEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheBackplaneMessageEventArgs"/> class.
	/// </summary>
	/// <param name="message">The backplane message.</param>
	public FusionCacheBackplaneMessageEventArgs(BackplaneMessage message)
	{
		Message = message;
	}

	/// <summary>
	/// The backplane message.
	/// </summary>
	public BackplaneMessage Message { get; }
}
