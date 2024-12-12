using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	/// <summary>
	/// The <see cref="ArrayPoolBufferWriter"/> class is an implementation of <see cref="T:IBufferWriter{byte}"/> that uses an <see cref="T:ArrayPool{byte}"/> to rent and return buffers.
	/// </summary>
	public class ArrayPoolBufferWriter : IBufferWriter<byte>
	{
		private static readonly ConcurrentStack<ArrayPoolBufferWriter> PooledWriters = new();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ArrayPoolBufferWriter Rent()
		{
			return PooledWriters.TryPop(out var writer) ? writer : new ArrayPoolBufferWriter();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Return(ArrayPoolBufferWriter writer)
		{
			writer.Reset();
			PooledWriters.Push(writer);
		}

		private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create();
		private byte[] _buffer;
		private int _bytesWritten = 0;
		/// <summary>
		/// Gets the number of bytes written to the buffer.
		/// </summary>
		public int BytesWritten => _bytesWritten;
		
		/// <summary>
		/// Gets the size of the buffer.
		/// </summary>
		public int BufferSize => _buffer.Length;

		/// <summary>
		/// Creates a new instance of the <see cref="ArrayPoolBufferWriter"/> class.
		/// </summary>
		private ArrayPoolBufferWriter()
		{
			_buffer = _arrayPool.Rent(4096);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Advance(int count)
		{
			if (_bytesWritten + count > _buffer.Length)
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
			var requiredCapacity = _bytesWritten + sizeHint;
			var currentBufferLength = _buffer.Length;
			if (requiredCapacity >= currentBufferLength)
			{
				var newSize = Math.Max(currentBufferLength * 2, requiredCapacity);
				var newBuffer = _arrayPool.Rent(newSize);
				var bufferSpan = _buffer.AsSpan();
				var newBufferSpan = newBuffer.AsSpan();
				Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(newBufferSpan), ref MemoryMarshal.GetReference(bufferSpan), (uint)_bytesWritten);
				_arrayPool.Return(_buffer);
				_buffer = newBuffer;
			}

			return _buffer.AsMemory(_bytesWritten);
		}

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
	}
}
