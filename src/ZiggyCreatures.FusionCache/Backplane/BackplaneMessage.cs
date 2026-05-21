using System.Buffers.Binary;
using System.Text;

using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents a message on a backplane.
/// </summary>
public class BackplaneMessage
{
	private static readonly Encoding _encoding = Encoding.UTF8;

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
	/// <param name="timestamp">The timestamp (in ticks) related to the operation being notified.</param>
	public BackplaneMessage(long timestamp)
	{
		Timestamp = timestamp;
	}

	/// <summary>
	/// The InstanceId of the source cache.
	/// </summary>
	public string? SourceId { get; set; }

	/// <summary>
	/// The timestamp (in ticks) related to the operation being notified.
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
	public static BackplaneMessage CreateForEntrySet(string sourceId, string cacheKey, long timestamp)
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
	public static BackplaneMessage CreateForEntryRemove(string sourceId, string cacheKey, long timestamp)
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
	public static BackplaneMessage CreateForEntryExpire(string sourceId, string cacheKey, long timestamp)
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

	/// <summary>
	/// Serializes a backplane message to a byte array.
	/// </summary>
	/// <param name="message">The backplane message to serialize.</param>
	/// <returns>The message as a byte[].</returns>
	public static byte[] ToByteArray(BackplaneMessage? message)
	{
		if (message is null)
			throw new ArgumentNullException(nameof(message));
		if (message.SourceId is null)
			throw new InvalidOperationException($"{nameof(message.SourceId)} cannot be null.");
		if (message.CacheKey is null)
			throw new InvalidOperationException($"{nameof(message.CacheKey)} cannot be null.");

		var sourceIdByteCount = _encoding.GetByteCount(message.SourceId);
		var cacheKeyByteCount = _encoding.GetByteCount(message.CacheKey);

		var size =
			1 // VERSION
			+ 4 + sourceIdByteCount // SOURCE ID
			+ 8 // INSTANCE TICKS
			+ 1 // ACTION
			+ 4 + cacheKeyByteCount // CACHE KEY
		;

		var res = new byte[size];
		var pos = 0;

		// VERSION
		res[pos] = 0;
		pos++;

		// SOURCE ID
		BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(res, pos, 4), sourceIdByteCount);
		pos += 4;
		_encoding.GetBytes(message.SourceId!, 0, message.SourceId!.Length, res, pos);
		pos += sourceIdByteCount;

		// TIMESTAMP
		BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(res, pos, 8), message.Timestamp);
		pos += 8;

		// ACTION
		res[pos] = (byte)message.Action;
		pos++;

		// CACHE KEY
		BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(res, pos, 4), cacheKeyByteCount);
		pos += 4;
		_encoding.GetBytes(message.CacheKey, 0, message.CacheKey!.Length, res, pos);
		//pos += cacheKeyByteCount;

		return res;
	}

	/// <summary>
	/// Deserializes a byte array into a backplane message.
	/// </summary>
	/// <param name="data">The byte array to deserialize.</param>
	/// <returns>An instance of a backplane message, or <see langword="null"/></returns>
	/// <exception cref="FormatException"></exception>
	public static BackplaneMessage FromByteArray(byte[]? data)
	{
		if (data is null)
			throw new ArgumentNullException(nameof(data));

		if (data.Length == 0)
			throw new InvalidOperationException("The byte array is empty.");

		var res = new BackplaneMessage();
		var pos = 0;

		// VERSION
		var version = data[pos];
		if (version != 0)
			throw new FormatException($"The backplane message version ({version}) is not supported.");
		pos++;

		// SOURCE ID
		var tmp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(data, pos, 4));
		pos += 4;
		res.SourceId = _encoding.GetString(data, pos, tmp);
		pos += tmp;

		// TIMESTAMP
		res.Timestamp = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(data, pos, 8));
		pos += 8;

		// ACTION
		res.Action = (BackplaneMessageAction)data[pos];
		pos++;

		// CACHE KEY
		tmp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(data, pos, 4));
		pos += 4;
		res.CacheKey = _encoding.GetString(data, pos, tmp);
		//pos += tmp;

		return res;
	}
}
