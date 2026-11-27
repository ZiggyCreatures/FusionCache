using System.Buffers;
using System.Collections.Concurrent;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="ArrayPool{T}"/> wrapper that keeps track of the arrays currently rented, to verify that every rented buffer is returned exactly once.
/// </summary>
internal sealed class TrackingArrayPool
	: ArrayPool<byte>
{
	private readonly ArrayPool<byte> _innerPool;
	private readonly ConcurrentDictionary<byte[], byte> _rented = new(ReferenceEqualityComparer.Instance);
	private int _invalidReturns;
	private int _totalRents;

	public TrackingArrayPool(ArrayPool<byte>? innerPool = null)
	{
		_innerPool = innerPool ?? Shared;
	}

	/// <summary>
	/// The number of arrays rented and not yet returned.
	/// </summary>
	public int Outstanding => _rented.Count;

	/// <summary>
	/// The total number of arrays rented.
	/// </summary>
	public int TotalRents => Volatile.Read(ref _totalRents);

	/// <summary>
	/// The number of returns of arrays that were not rented from this pool, or that had already been returned.
	/// </summary>
	public int InvalidReturns => Volatile.Read(ref _invalidReturns);

	public override byte[] Rent(int minimumLength)
	{
		Interlocked.Increment(ref _totalRents);
		var array = _innerPool.Rent(minimumLength);
		if (_rented.TryAdd(array, 0) == false)
			throw new InvalidOperationException("The inner pool handed out an array which is still rented.");

		return array;
	}

	public override void Return(byte[] array, bool clearArray = false)
	{
		if (_rented.TryRemove(array, out _) == false)
		{
			Interlocked.Increment(ref _invalidReturns);
			return;
		}

		_innerPool.Return(array, clearArray);
	}

	public async Task<bool> WaitForNoOutstandingAsync(TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (Outstanding > 0)
		{
			if (DateTime.UtcNow > deadline)
				return false;

			await Task.Delay(10);
		}

		return true;
	}

	public bool WaitForNoOutstanding(TimeSpan timeout)
	{
		return WaitForNoOutstandingAsync(timeout).GetAwaiter().GetResult();
	}
}
