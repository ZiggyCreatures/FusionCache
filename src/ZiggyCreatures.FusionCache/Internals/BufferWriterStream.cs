using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// A writable stream that proxies the data to the provided <see cref="IBufferWriter{Byte}"/>.
/// </summary>
public sealed class BufferWriterStream : Stream
{
	private readonly IBufferWriter<byte> _writer;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferWriterStream"/> class.
	/// </summary>
	public BufferWriterStream(IBufferWriter<byte> writer)
	{
		_writer = writer ?? throw new ArgumentNullException(nameof(writer));
	}

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => !_disposed;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException("Cannot get the length of a writable stream.");

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException("Cannot get the position of a writable stream.");
		set => throw new NotSupportedException("Cannot set the position of a writable stream.");
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		ThrowIfDisposed();
		// no-op
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException("Cannot read from a writable stream.");
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotSupportedException("Cannot seek a writable stream.");
	}

	/// <inheritdoc/>
	public override void SetLength(long value)
	{
		throw new NotSupportedException("Cannot set the length of a writable stream.");
	}

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		Write(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		Write([value]);
	}

#if !NETSTANDARD2_0
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
#else
	private void Write(ReadOnlySpan<byte> buffer)
#endif
	{
		ThrowIfDisposed();
		
		var memory = _writer.GetSpan(buffer.Length);
		buffer.CopyTo(memory);
		_writer.Advance(buffer.Length);
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
