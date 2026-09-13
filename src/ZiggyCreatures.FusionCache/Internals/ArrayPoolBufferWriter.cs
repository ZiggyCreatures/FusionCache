using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// The <see cref="ArrayPoolBufferWriter"/> class is an implementation of <see cref="T:IBufferWriter{byte}"/> that uses an <see cref="T:ArrayPool{byte}"/> to rent and return buffers.
/// <br/><br/>
/// The buffer is contiguous (grown by doubling), so the written data is always available as a single-segment <see cref="ReadOnlySequence{T}"/>: this enables single-span fast paths in serializers and avoids linearization in consumers like the Redis implementation of IBufferDistributedCache.
/// </summary>
public sealed class ArrayPoolBufferWriter : IBufferWriter<byte>, IDisposable
{
	// LEGACY DEFAULT POOL: ISOLATED, WITH A 1MB MAX ARRAY LENGTH AND NO TRIMMING.
	// NOTE: FOR BUFFERS ABOVE ITS MAX ARRAY LENGTH AN ISOLATED POOL FALLS BACK TO PLAIN
	// ALLOCATIONS, WHICH IS VERY COSTLY WITH LARGE PAYLOADS: PREFER PASSING ArrayPool<byte>.Shared
	private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create();

	// NOTE: SAME VALUE AS Array.MaxLength, WHICH IS NOT AVAILABLE ON ALL THE TARGET FRAMEWORKS
	private const int MaxBufferLength = 0X7FFFFFC7;

	private readonly ArrayPool<byte> _pool;
	private byte[] _buffer;
	private int _bytesWritten = 0;
	private bool disposedValue;

	/// <summary>
	/// Gets the number of bytes written to the buffer.
	/// </summary>
	public int BytesWritten => _bytesWritten;

	/// <summary>
	/// Gets the size of the buffer.
	/// </summary>
	public int BufferSize => _buffer.Length;

	/// <summary>
	/// Creates a new instance of the <see cref="ArrayPoolBufferWriter"/> class, using an isolated <see cref="T:ArrayPool{byte}"/> with a 1MB max array length.
	/// </summary>
	public ArrayPoolBufferWriter()
		: this(null)
	{
		// EMPTY
	}

	/// <summary>
	/// Creates a new instance of the <see cref="ArrayPoolBufferWriter"/> class using the specified pool.
	/// </summary>
	/// <param name="pool">The <see cref="T:ArrayPool{byte}"/> to rent buffers from (eg: <see cref="ArrayPool{T}.Shared"/>, which supports buffers of any size and releases unused ones under memory pressure), or <see langword="null"/> to use the default isolated pool.</param>
	/// <param name="initialCapacity">The initial buffer capacity.</param>
	public ArrayPoolBufferWriter(ArrayPool<byte>? pool, int initialCapacity = 4096)
	{
		_pool = pool ?? _arrayPool;
		_buffer = _pool.Rent(initialCapacity);
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Advance(int count)
	{
		// NOTE: THE BOUNDS CHECK IS SUBTRACTION-BASED (INSTEAD OF _bytesWritten + count) SO THAT
		// IT CANNOT OVERFLOW: SINCE _bytesWritten IS ALWAYS <= _buffer.Length, THE RIGHT-HAND SIDE
		// IS ALWAYS A NON-NEGATIVE int
		if (count < 0 || count > _buffer.Length - _bytesWritten)
		{
			ThrowInvalidOperationException();
		}

		_bytesWritten += count;
	}

	/// <summary>
	/// Resets the buffer writer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Reset()
	{
		_bytesWritten = 0;
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		if (sizeHint < 1)
		{
			sizeHint = 1;
		}

		// NOTE: ALL THE SIZE MATH IS DONE IN long TO AVOID OVERFLOWS WITH VERY LARGE PAYLOADS
		// (EG: A sizeHint NEAR int.MaxValue, OR DOUBLING A BUFFER ALREADY ABOVE 1GB)
		var currentBufferLength = _buffer.Length;
		if (sizeHint > currentBufferLength - _bytesWritten)
		{
			var requiredCapacity = (long)_bytesWritten + sizeHint;
			if (requiredCapacity > MaxBufferLength)
			{
				ThrowOutOfMemoryException();
			}

			var newSize = (int)Math.Min(MaxBufferLength, Math.Max((long)currentBufferLength * 2, requiredCapacity));
			var newBuffer = _pool.Rent(newSize);
			var bufferSpan = _buffer.AsSpan();
			var newBufferSpan = newBuffer.AsSpan();
			Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(newBufferSpan), ref MemoryMarshal.GetReference(bufferSpan), (uint)_bytesWritten);
			_pool.Return(_buffer);
			_buffer = newBuffer;
		}

		return _buffer.AsMemory(_bytesWritten);
	}

	/// <summary>
	/// Returns the written data as a (single-segment) <see cref="ReadOnlySequence{T}"/>, without copying.
	/// <br/><br/>
	/// <strong>IMPORTANT:</strong> the sequence is backed by the pooled buffer, so it is only valid until the writer is disposed or written to again.
	/// </summary>
	public ReadOnlySequence<byte> WrittenSequence => new(_buffer, 0, _bytesWritten);

	/// <summary>
	/// Returns the buffer as an array of <see cref="T:byte[]" />
	/// </summary>
	/// <returns>The buffer as a byte array.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] ToArray()
	{
		var bufferSpan = _buffer.AsSpan(0, _bytesWritten);
		byte[] result = new byte[_bytesWritten];
		var resultSpan = result.AsSpan();
		Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(resultSpan), ref MemoryMarshal.GetReference(bufferSpan), (uint)_bytesWritten);
		return result;
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<byte> GetSpan(int sizeHint = 0)
	{
		return GetMemory(sizeHint).Span;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowInvalidOperationException()
	{
		throw new InvalidOperationException("Cannot advance past the end of the buffer.");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowOutOfMemoryException()
	{
		throw new OutOfMemoryException("Cannot grow the buffer past the maximum array length.");
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (disposedValue)
		{
			return;
		}

		_pool.Return(_buffer);
		disposedValue = true;
	}
}
