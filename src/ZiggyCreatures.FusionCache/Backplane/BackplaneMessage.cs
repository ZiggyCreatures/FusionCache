using System;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents a message on a backplane.
/// </summary>
public class BackplaneMessage
{
	/// <summary>
	/// Creates a new instance of a backplane message.
	/// </summary>
	public BackplaneMessage()
	{
		Timestamp = FusionCacheInternalUtils.GetCurrentTimestamp();
	}

	/// <summary>
	/// Creates a new instance of a backplane message.
	/// </summary>
	/// <param name="timestamp">The timestamp, or <see langword="null"/> to set it automatically to the current timestamp.</param>
	public BackplaneMessage(long? timestamp)
	{
		Timestamp = timestamp ?? FusionCacheInternalUtils.GetCurrentTimestamp();
	}

	/// <summary>
	/// The InstanceId of the source cache.
	/// </summary>
	public string? SourceId { get; set; }

	/// <summary>
	/// The timestamp (in ticks) at a message has been created.
	/// </summary>
	public long Timestamp { get; set; }

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

		if (Timestamp <= 0)
			return false;

		switch (Action)
		{
			case BackplaneMessageAction.EntrySet:
			case BackplaneMessageAction.EntryRemove:
			case BackplaneMessageAction.EntryExpire:
				if (string.IsNullOrEmpty(CacheKey))
					return false;
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Creates a message for a single cache entry set operation (via either a Set() or a GetOrSet() method call).
	/// </summary>
	/// <param name="sourceId">The cache InstanceId of the source.</param>
	/// <param name="cacheKey">The cache key.</param>
	/// <param name="timestamp">The timestamp.</param>
	/// <returns>The message.</returns>
	public static BackplaneMessage CreateForEntrySet(string sourceId, string cacheKey, long? timestamp)
	{
		if (string.IsNullOrEmpty(cacheKey))
			throw new ArgumentException("The cache key cannot be null or empty", nameof(cacheKey));

		return new BackplaneMessage(timestamp)
		{
			SourceId = sourceId,
			Action = BackplaneMessageAction.EntrySet,
			CacheKey = cacheKey
		};
	}

	/// <summary>
	/// Creates a message for a single cache entry remove (via a Remove() method call).
	/// </summary>
	/// <param name="sourceId">The cache InstanceId of the source.</param>
	/// <param name="cacheKey">The cache key.</param>
	/// <param name="timestamp">The timestamp.</param>
	/// <returns>The message.</returns>
	public static BackplaneMessage CreateForEntryRemove(string sourceId, string cacheKey, long? timestamp)
	{
		if (string.IsNullOrEmpty(cacheKey))
			throw new ArgumentException("The cache key cannot be null or empty", nameof(cacheKey));

		return new BackplaneMessage(timestamp)
		{
			SourceId = sourceId,
			Action = BackplaneMessageAction.EntryRemove,
			CacheKey = cacheKey
		};
	}

	/// <summary>
	/// Creates a message for a single cache entry expire operation (via an Expire() method call).
	/// </summary>
	/// <param name="sourceId">The cache InstanceId of the source.</param>
	/// <param name="cacheKey">The cache key.</param>
	/// <param name="timestamp">The timestamp.</param>
	/// <returns>The message.</returns>
	public static BackplaneMessage CreateForEntryExpire(string sourceId, string cacheKey, long? timestamp)
	{
		if (string.IsNullOrEmpty(cacheKey))
			throw new ArgumentException("The cache key cannot be null or empty", nameof(cacheKey));

		return new BackplaneMessage(timestamp)
		{
			SourceId = sourceId,
			Action = BackplaneMessageAction.EntryExpire,
			CacheKey = cacheKey
		};
	}
}
