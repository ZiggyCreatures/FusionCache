using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals;

/// <summary>
/// A simple, reusable circuit-breaker.
/// </summary>
internal sealed class SimpleCircuitBreaker
{
	private const int CircuitStateClosed = 0;
	private const int CircuitStateOpen = 1;

	private const long NoAnchor = long.MinValue;

	private int _circuitState;
	private long _gatewayTimestamp = NoAnchor;

	/// <summary>
	/// Creates a new <see cref="SimpleCircuitBreaker"/> instance.
	/// </summary>
	/// <param name="breakDuration">The amount of time the circuit will remain open, when told to.</param>
	public SimpleCircuitBreaker(TimeSpan breakDuration)
	{
		BreakDuration = breakDuration;
	}

	/// <summary>
	/// The amount of time the circuit will remain open, when told to.
	/// </summary>
	public TimeSpan BreakDuration { get; private set; }

	/// <summary>
	/// Tries to open the circuit.
	/// </summary>
	/// <param name="isStateChanged">Indicates if the circuit has been opened with this operation.</param>
	/// <returns><see langword="true"/> if the circuit is open, either because it was already or because it has been opened with this operation. <see langword="false"/> otherwise.</returns>
	public bool TryOpen(out bool isStateChanged)
	{
		// NO CIRCUIT-BREAKER DURATION
		if (BreakDuration == TimeSpan.Zero)
		{
			isStateChanged = false;
			return false;
		}

		Interlocked.Exchange(ref _gatewayTimestamp, Stopwatch.GetTimestamp());

		// DETECT CIRCUIT STATE CHANGE
		var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateOpen);

		isStateChanged = oldCircuitState == CircuitStateClosed;
		return true;
	}

	/// <summary>
	/// Close the circuit.
	/// </summary>
	/// <param name="isStateChanged">Indicates if the circuit has been closed with this operation.</param>
	public void Close(out bool isStateChanged)
	{
		Interlocked.Exchange(ref _gatewayTimestamp, NoAnchor);

		// DETECT CIRCUIT STATE CHANGE
		var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateClosed);

		isStateChanged = oldCircuitState == CircuitStateOpen;
	}

	/// <summary>
	/// Check if the circuit is closed, or has been closed with this operation.
	/// </summary>
	/// <param name="isStateChanged">Indicates if the circuit has been closed with this operation.</param>
	/// <returns><see langword="true"/> if the circuit is closed, either because it was already closed or because it has been closed with this operation. <see langword="false"/> otherwise.</returns>
	public bool IsClosed(out bool isStateChanged)
	{
		isStateChanged = false;

		// NO CIRCUIT-BREAKER DURATION
		if (BreakDuration == TimeSpan.Zero)
			return true;

		var anchor = Interlocked.Read(ref _gatewayTimestamp);

		// NOT ENOUGH TIME IS PASSED
		if (anchor != NoAnchor && StopwatchPolyfill.GetElapsedTime(anchor) < BreakDuration)
			return false;

		if (_circuitState == CircuitStateOpen)
		{
			var oldCircuitState = Interlocked.Exchange(ref _circuitState, CircuitStateClosed);
			isStateChanged = oldCircuitState == CircuitStateOpen;
		}

		return true;
	}
}
