using System;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The exception that is thrown when the time allotted for a process or operation has expired.
/// </summary>
[Serializable]
public class SyntheticTimeoutException
	: TimeoutException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SyntheticTimeoutException"/> class.
	/// </summary>
	public SyntheticTimeoutException()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="SyntheticTimeoutException"/> class with the specified error message.</summary>
	/// <param name="message">The message that describes the error.</param>
	public SyntheticTimeoutException(string? message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="SyntheticTimeoutException"/> class with the specified error message and inner exception.</summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
	public SyntheticTimeoutException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
