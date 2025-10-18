using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// Represents a message on a backplane.
/// </summary>
public readonly struct BackplaneMessage
{
	private static readonly Encoding _encoding = Encoding.UTF8;

	/// <summary>
	/// Creates a new instance of a backplane message.
	/// </summary>
	/// <param name="action">The action to broadcast to the backplane.</param>
	/// <param name="timestamp">The timestamp (in ticks) related to the operation being notified.</param>
	/// <param name="sourceId">The cache InstanceId of the source.</param>
	/// <param name="cacheKey">The cache key related to the action, if any.</param>
	private BackplaneMessage(BackplaneMessageAction action, long timestamp, string sourceId, string cacheKey)
	{
		Action = action;
		Timestamp = timestamp;
		SourceId = sourceId;
		CacheKey = cacheKey;
	}

	/// <summary>
	/// The InstanceId of the source cache.
	/// </summary>
	public readonly string SourceId;

	/// <summary>
	/// The timestamp (in ticks) related to the operation being notified.
	/// </summary>
	public readonly long Timestamp;

	/// <summary>
	/// The action to broadcast to the backplane.
	/// </summary>
	public readonly BackplaneMessageAction Action;

	/// <summary>
	/// The cache key related to the action, if any.
	/// </summary>
	public readonly string CacheKey;

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

		return new BackplaneMessage(BackplaneMessageAction.EntrySet, timestamp, sourceId, cacheKey);
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

		return new BackplaneMessage(BackplaneMessageAction.EntryRemove, timestamp, sourceId, cacheKey);
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

		return new BackplaneMessage(BackplaneMessageAction.EntryExpire, timestamp, sourceId, cacheKey);
	}

	/// <summary>
	/// Writes the backplane message to a buffer writer.
	/// </summary>
	/// <typeparam name="T">The type of the buffer writer.</typeparam>
	/// <param name="writer">The buffer writer to write to.</param>
	/// <exception cref="ArgumentNullException"></exception>
	public void WriteTo<T>(T writer) where T : IBufferWriter<byte>
	{
		var sourceIdByteCount = _encoding.GetByteCount(SourceId);
		var cacheKeyByteCount = _encoding.GetByteCount(CacheKey);

		var size =
			1 // VERSION
			+ 4 + sourceIdByteCount // SOURCE ID
			+ 8 // INSTANCE TICKS
			+ 1 // ACTION
			+ 4 + cacheKeyByteCount // CACHE KEY
		;

		var span = writer.GetSpan(size);

		// VERSION
		span[0] = 0;

		// SOURCE ID
		BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1, 4), sourceIdByteCount);
		_encoding.GetBytes(SourceId, span.Slice(5));

		// TIMESTAMP
		BinaryPrimitives.WriteInt64LittleEndian(span.Slice(5 + sourceIdByteCount, 8), Timestamp);

		// ACTION
		span[13 + sourceIdByteCount] = (byte)Action;

		// CACHE KEY
		BinaryPrimitives.WriteInt32LittleEndian(span.Slice(14 + sourceIdByteCount, 4), cacheKeyByteCount);
		_encoding.GetBytes(CacheKey, span.Slice(18 + sourceIdByteCount));
		writer.Advance(size);
	}

	/// <summary>
	/// Tries to parse a byte array into a backplane message.
	/// </summary>
	/// <param name="data">The byte array to parse.</param>
	/// <param name="message">When successful, the parsed backplane message.</param>
	/// <returns>True if the parsing was successful, false otherwise.</returns>
	public static bool TryParse(ReadOnlySpan<byte> data, out BackplaneMessage message)
	{
		if (data.IsEmpty)
		{
			message = default;
			return false;
		}

		// Check the version
		if (data[0] != 0)
		{
			message = default;
			return false;
		}

		try
		{
			// SOURCE ID
			int sourceIdLength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
			string sourceId = _encoding.GetString(data.Slice(5, sourceIdLength));

			// TIMESTAMP
			long timestamp = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(5 + sourceIdLength));

			// ACTION
			BackplaneMessageAction backplaneMessageAction = (BackplaneMessageAction)data[13 + sourceIdLength];

			// CACHE KEY
			int cacheKeyLength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(14 + sourceIdLength));
			string cacheKey = _encoding.GetString(data.Slice(18 + sourceIdLength, cacheKeyLength));
			message = new BackplaneMessage(backplaneMessageAction, timestamp, sourceId, cacheKey);
			return true;
		}
		catch (Exception)
		{
			message = default;
			return false;
		}
	}
}
