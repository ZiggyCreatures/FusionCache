using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// An implementation of <see cref="Stream"/> that wraps a <see cref="ReadOnlySequence{Byte}"/> and enables reading from it.
/// </summary>
public sealed class ReadOnlySequenceStream : Stream
{
	private readonly ReadOnlySequence<byte> _source;
	private ReadOnlySequence<byte> _tail;
	private bool _disposed;

	/// <summary>
	/// Creates a <see cref="ReadOnlySequenceStream"/> from the provided <paramref name="source"/>.
	/// </summary>
	/// <param name="source">The sequence to read from.</param>
	public ReadOnlySequenceStream(in ReadOnlySequence<byte> source)
	{
		_source = source;
		_tail = source;
	}

	/// <inheritdoc/>
    public override bool CanRead => !_disposed;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
    public override bool CanSeek => !_disposed;

	/// <inheritdoc/>
	public override long Length
	{
		get
		{
			ThrowIfDisposed();
			return _source.Length;
		}
	}

	/// <inheritdoc/>
	public override long Position
	{
		get
		{
			ThrowIfDisposed();
			return _source.Length - _tail.Length;
		}

		set
		{
			ThrowIfDisposed();
			_tail = _source.Slice(value);
		}
	}

    /// <inheritdoc/>
    public override void Flush()
    {
	    ThrowIfDisposed();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
	    ThrowIfDisposed();

	    var position = origin switch
	    {
		    SeekOrigin.Begin => offset,
		    SeekOrigin.Current => _source.Length - _tail.Length + offset,
		    SeekOrigin.End => _source.Length + offset,
		    _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, message: null)
	    };

	    _tail = _source.Slice(position);

	    return position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
	    ThrowIfDisposed();
	    throw new NotSupportedException("Cannot set the length of a read-only stream.");
    }

    /// <inheritdoc/>
    public override int Read(byte[]? buffer, int offset, int count)
    {
	    var destination = buffer.AsSpan(offset, count);
	    return Read(destination);
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
	    Span<byte> buffer = stackalloc byte[1];

	    if (Read(buffer) == 0)
	    {
		    return -1;
	    }

	    return buffer[0];
    }

#if !NETSTANDARD2_0
	/// <inheritdoc />
	public override int Read(Span<byte> buffer)
#else
    private int Read(Span<byte> buffer)
#endif
    {
	    ThrowIfDisposed();

	    var remainingLength = _tail.Length;
	    var bytesToRead = remainingLength >= buffer.Length ? buffer.Length : (int)remainingLength;

	    if (bytesToRead == 0)
	    {
		    return 0;
	    }

	    var endPosition = _tail.GetPosition(bytesToRead);
	    _tail.Slice(0, endPosition).CopyTo(buffer);
	    _tail = _tail.Slice(endPosition);
	    
	    return bytesToRead;
    }

    /// <inheritdoc/>
    public override void Write(byte[]? buffer, int offset, int count)
    {
	    throw new NotSupportedException("Cannot write to a read-only stream.");
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        throw new NotSupportedException("Cannot write to a read-only stream.");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
	    _disposed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
#if NETSTANDARD2_0
	    if (_disposed) ThrowObjectDisposedSlow();

	    static void ThrowObjectDisposedSlow()
	    {
		    throw new ObjectDisposedException(nameof(ReadOnlySequenceStream));
	    }
#else
	    ObjectDisposedException.ThrowIf(_disposed, typeof(ReadOnlySequenceStream));
#endif
    }

}
