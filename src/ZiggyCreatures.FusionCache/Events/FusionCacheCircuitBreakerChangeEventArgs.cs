namespace ZiggyCreatures.Caching.Fusion.Events;

/// <summary>
/// The specific <see cref="EventArgs"/> object for events related to opening/closing of a circuit breaker.
/// </summary>
public class FusionCacheCircuitBreakerChangeEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheCircuitBreakerChangeEventArgs"/> class.
	/// </summary>
	/// <param name="isClosed">A flag that indicates if the circuit breaker has been opened or closed.</param>
	public FusionCacheCircuitBreakerChangeEventArgs(bool isClosed)
	{
		IsClosed = isClosed;
	}

	/// <summary>
	/// A flag that indicates if the circuit breaker has been opened or closed.
	/// </summary>
	public bool IsClosed { get; }
}
