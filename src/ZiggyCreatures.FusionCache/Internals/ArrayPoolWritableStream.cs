using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	/// <summary>
	/// A writable stream that uses an <see cref="ArrayPoolBufferWriter"/> to manage its buffer.
	/// </summary>
	public class ArrayPoolWritableStream : Stream
	{
		private readonly ArrayPoolBufferWriter _buffer;

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayPoolWritableStream"/> class.
		/// </summary>
		public ArrayPoolWritableStream()
		{
			_buffer = ArrayPoolBufferWriter.Rent();
		}

		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => true;

		/// <inheritdoc/>
		public override long Length => _buffer.BytesWritten;

		/// <summary>
		/// Gets the written bytes as a byte array.
		/// </summary>
		/// <returns>The written bytes as a byte array.</returns>
		public byte[] GetBytes() => _buffer.ToArray();

		/// <inheritdoc/>
		public override long Position
		{
			get => Length;
			set
			{
				throw new NotSupportedException("Cannot set the position of a writable stream.");
			}
		}

		/// <inheritdoc/>
		public override void Flush()
		{
			// no-op
			return;
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
			var memory = _buffer.GetSpan(count);
			buffer.AsSpan(offset, count).CopyTo(memory);
			_buffer.Advance(count);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ArrayPoolWritableStream"/> and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ArrayPoolBufferWriter.Return(_buffer);
			}
		}
	}
}
