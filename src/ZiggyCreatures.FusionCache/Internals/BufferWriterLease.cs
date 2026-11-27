using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// Shares ownership of a pooled <see cref="ArrayPoolBufferWriter"/> between a caller and a (distributed cache) operation that may outlive it, for example because of a timeout or a cancellation.
/// <br/><br/>
/// The buffer is returned to the pool exactly once, and never while the operation may still be using it: whoever finishes last (the caller via <see cref="Dispose"/>, or the operation via <see cref="EndUse"/>) releases it.
/// </summary>
internal sealed class BufferWriterLease
	: IDisposable
{
	// THE CALLER OWNS THE WRITER (NO OPERATION IS USING IT)
	private const int StateOwned = 0;
	// AN OPERATION IS USING THE WRITER
	private const int StateInUse = 1;
	// THE CALLER IS DONE WITH THE WRITER: WHOEVER SEES THIS STATE LAST RELEASES IT
	private const int StateReleased = 2;

	private int _state = StateOwned;

	/// <summary>
	/// Creates a new instance of the <see cref="BufferWriterLease"/> class, renting a writer from the specified pool.
	/// </summary>
	/// <param name="pool">The <see cref="ArrayPool{T}"/> to rent the buffer from.</param>
	public BufferWriterLease(ArrayPool<byte> pool)
	{
		Writer = new ArrayPoolBufferWriter(pool);
	}

	/// <summary>
	/// The leased writer: the caller can use it freely before starting an operation (see <see cref="TryBeginUse"/>) and after it ended (see <see cref="EndUse"/>).
	/// </summary>
	public ArrayPoolBufferWriter Writer { get; }

	/// <summary>
	/// Called by the operation before using the writer.
	/// </summary>
	/// <returns><see langword="true"/> if the operation can use the writer, or <see langword="false"/> if the caller already gave up on it (in which case the writer must not be touched).</returns>
	public bool TryBeginUse()
	{
		return Interlocked.CompareExchange(ref _state, StateInUse, StateOwned) == StateOwned;
	}

	/// <summary>
	/// Called by the operation when it will not use the writer anymore (both on success and on failure): if the caller already gave up on it, the writer is released here.
	/// </summary>
	public void EndUse()
	{
		if (Interlocked.CompareExchange(ref _state, StateOwned, StateInUse) == StateInUse)
			return;

		// THE CALLER ALREADY GAVE UP (EG: TIMEOUT OR CANCELLATION), SO THE OPERATION IS THE LAST USER
		Writer.Dispose();
	}

	/// <summary>
	/// Called by the caller when it does not need the writer anymore: if no operation is using it, the writer is released immediately, otherwise it will be released by the operation when it ends.
	/// </summary>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _state, StateReleased) == StateOwned)
			Writer.Dispose();
	}
}
