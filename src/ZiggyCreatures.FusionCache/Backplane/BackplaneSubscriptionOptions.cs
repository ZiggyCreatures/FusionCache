using System;
using System.ComponentModel;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents the options available for subscribing to a backplane.
/// </summary>
public class BackplaneSubscriptionOptions
{
	/// <summary>
	/// Creates a new instance of a <see cref="BackplaneSubscriptionOptions"/>.
	/// </summary>
	/// <param name="channelName">The channel name to be used.</param>
	/// <param name="connectHandler">The backplane connection handler that will be used when there's a connection (or reconnection).</param>
	/// <param name="incomingMessageHandler">The backplane message handler that will be used to process incoming messages.</param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use the newer constructor with more parameters.", true)]
	public BackplaneSubscriptionOptions(string? channelName, Action<BackplaneConnectionInfo>? connectHandler, Action<BackplaneMessage>? incomingMessageHandler)
	{
		CacheName = "";
		CacheInstanceId = "";
		ChannelName = channelName;
		ConnectHandler = connectHandler;
		IncomingMessageHandler = incomingMessageHandler;
	}

	/// <summary>
	/// Creates a new instance of a <see cref="BackplaneSubscriptionOptions"/>.
	/// </summary>
	/// <param name="cacheName">The cache name.</param>
	/// <param name="cacheInstanceId">The unique cache instance id.</param>
	/// <param name="channelName">The channel name to be used.</param>
	/// <param name="connectHandler">The backplane connection handler that will be used when there's a connection (or reconnection).</param>
	/// <param name="incomingMessageHandler">The backplane message handler that will be used to process incoming messages.</param>
	public BackplaneSubscriptionOptions(string cacheName, string cacheInstanceId, string? channelName, Action<BackplaneConnectionInfo>? connectHandler, Action<BackplaneMessage>? incomingMessageHandler)
	{
		CacheName = cacheName;
		CacheInstanceId = cacheInstanceId;
		ChannelName = channelName;
		ConnectHandler = connectHandler;
		IncomingMessageHandler = incomingMessageHandler;
	}

	/// <summary>
	/// The cache name.
	/// </summary>
	public string CacheName { get; }

	/// <summary>
	/// The cache instance id.
	/// </summary>
	public string CacheInstanceId { get; }

	/// <summary>
	/// The channel name to be used.
	/// </summary>
	public string? ChannelName { get; }

	/// <summary>
	/// The backplane message handler that will be used to process incoming messages.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Please use MessageHandler instead.", true)]
	public Action<BackplaneMessage>? Handler
	{
		get { return IncomingMessageHandler; }
	}

	/// <summary>
	/// The backplane connection handler that will be used when there's a connection (or reconnection).
	/// </summary>
	public Action<BackplaneConnectionInfo>? ConnectHandler { get; }

	/// <summary>
	/// The backplane message handler that will be used to process incoming messages.
	/// </summary>
	public Action<BackplaneMessage>? IncomingMessageHandler { get; }
}
