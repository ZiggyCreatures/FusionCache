using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// A write-only <see cref="Stream"/> that forwards all writes to the provided <see cref="IBufferWriter{T}"/>: used to adapt stream-based serializers to the buffered serialization path.
/// </summary>
public sealed class BufferWriterStream : Stream
{
	private readonly IBufferWriter<byte> _writer;
	private long _written;

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferWriterStream"/> class.
	/// </summary>
	/// <param name="writer">The buffer writer to forward writes to.</param>
	public BufferWriterStream(IBufferWriter<byte> writer)
	{
		_writer = writer ?? throw new ArgumentNullException(nameof(writer));
	}

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => true;

	/// <inheritdoc/>
	public override long Length => _written;

	/// <inheritdoc/>
	public override long Position
	{
		get => _written;
		set => throw new NotSupportedException("Cannot set the position of a write-only stream.");
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		// EMPTY: WRITES GO DIRECTLY TO THE BUFFER WRITER
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Cannot read from a write-only stream.");

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Cannot seek a write-only stream.");

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException("Cannot set the length of a write-only stream.");

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		_writer.Write(buffer.AsSpan(offset, count));
		_written += count;
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		var span = _writer.GetSpan(1);
		span[0] = value;
		_writer.Advance(1);
		_written++;
	}

#if !NETSTANDARD2_0
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		_writer.Write(buffer);
		_written += buffer.Length;
	}
#endif
}
