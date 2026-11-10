using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// A read-only <see cref="Stream"/> over a <see cref="ReadOnlySequence{T}"/>: used to adapt stream-based serializers to the buffered serialization path.
/// <br/><br/>
/// <strong>IMPORTANT:</strong> the underlying sequence may be backed by pooled memory, so the stream must not be used after the memory is returned to the pool.
/// </summary>
public sealed class ReadOnlySequenceStream : Stream
{
	private readonly ReadOnlySequence<byte> _sequence;
	private ReadOnlySequence<byte> _remaining;

	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlySequenceStream"/> class.
	/// </summary>
	/// <param name="sequence">The sequence to read from.</param>
	public ReadOnlySequenceStream(in ReadOnlySequence<byte> sequence)
	{
		_sequence = sequence;
		_remaining = sequence;
	}

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanSeek => true;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => _sequence.Length;

	/// <inheritdoc/>
	public override long Position
	{
		get => _sequence.Length - _remaining.Length;
		set => _remaining = _sequence.Slice(value);
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));

	/// <inheritdoc/>
	public override int ReadByte()
	{
		if (_remaining.IsEmpty)
			return -1;

		var value = _remaining.First.Span[0];
		_remaining = _remaining.Slice(1);
		return value;
	}

#if !NETSTANDARD2_0
	/// <inheritdoc/>
	public override int Read(Span<byte> buffer) => ReadCore(buffer);
#endif

	private int ReadCore(Span<byte> buffer)
	{
		var bytesToRead = (int)Math.Min(buffer.Length, _remaining.Length);
		if (bytesToRead == 0)
			return 0;

		_remaining.Slice(0, bytesToRead).CopyTo(buffer.Slice(0, bytesToRead));
		_remaining = _remaining.Slice(bytesToRead);
		return bytesToRead;
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin)
	{
		var position = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => Position + offset,
			SeekOrigin.End => _sequence.Length + offset,
			_ => throw new ArgumentOutOfRangeException(nameof(origin)),
		};

		Position = position;
		return position;
	}

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException("Cannot set the length of a read-only stream.");

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Cannot write to a read-only stream.");
}
