using System;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents the options available for subscribing to a backplane.
/// </summary>
public class BackplaneSubscriptionOptions
{
	/// <summary>
	/// The channel name to be used.
	/// </summary>
	public string? ChannelName { get; set; }

	/// <summary>
	/// The backplane message handler that will be used to process incoming messages.
	/// </summary>
	public Action<BackplaneMessage>? Handler { get; set; }
}
