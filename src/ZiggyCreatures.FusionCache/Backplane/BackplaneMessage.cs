using System;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents a message on a backplane.
/// </summary>
public class BackplaneMessage
{
	/// <summary>
	/// Creates a new instance of a backplane message.
	/// </summary>
	///// <param name="sourceId">The InstanceId of the source cache.</param>
	///// <param name="instantTicks">The instant this message is related to, expressed as ticks amount. If null, DateTimeOffset.UtcNow.Ticks will be used.</param>
	public BackplaneMessage()
	{
		InstantTicks = DateTimeOffset.UtcNow.Ticks;
	}

	/// <summary>
	/// The InstanceId of the source cache.
	/// </summary>
	public string? SourceId { get; set; }

	/// <summary>
	/// The instant a message was related to, expressed as ticks amount.
	/// </summary>
	public long InstantTicks { get; set; }

	/// <summary>
	/// The action to broadcast to the backplane.
	/// </summary>
	public BackplaneMessageAction Action { get; set; }

	/// <summary>
	/// The cache key related to the action, if any.
	/// </summary>
	public string? CacheKey { get; set; }

	/// <summary>
	/// Checks if a message is valid.
	/// </summary>
	/// <returns><see langword="true"/> if it seems valid, <see langword="false"/> otherwise.</returns>
	public bool IsValid()
	{
		if (string.IsNullOrEmpty(SourceId))
			return false;

		if (InstantTicks <= 0)
			return false;

		switch (Action)
		{
			case BackplaneMessageAction.EntrySet:
			case BackplaneMessageAction.EntryRemove:
				if (string.IsNullOrEmpty(CacheKey))
					return false;
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Creates a message for a single cache entry set operation (via either a Set or a GetOrSet method call).
	/// </summary>
	/// <param name="cacheKey">The cache key.</param>
	/// <returns>The message.</returns>
	public static BackplaneMessage CreateForEntrySet(string cacheKey)
	{
		if (string.IsNullOrEmpty(cacheKey))
			throw new ArgumentException("The cache key cannot be null or empty", nameof(cacheKey));

		return new BackplaneMessage()
		{
			Action = BackplaneMessageAction.EntrySet,
			CacheKey = cacheKey
		};
	}

	/// <summary>
	/// Creates a message for a single cache entry remove (via a Remove method call).
	/// </summary>
	/// <param name="cacheKey">The cache key.</param>
	/// <returns>The message.</returns>
	public static BackplaneMessage CreateForEntryRemove(string cacheKey)
	{
		if (string.IsNullOrEmpty(cacheKey))
			throw new ArgumentException("The cache key cannot be null or empty", nameof(cacheKey));

		return new BackplaneMessage()
		{
			Action = BackplaneMessageAction.EntryRemove,
			CacheKey = cacheKey
		};
	}
}
