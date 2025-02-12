namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The exception thrown when a factory fails via the Fail() method and fail-safe was not enabled or possible.
/// </summary>
[Serializable]
public class FusionCacheFactoryException
	: Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheFactoryException"/> class.
	/// </summary>
	public FusionCacheFactoryException()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheFactoryException"/> class with a specified error message.</summary>
	/// <param name="message">The message that describes the error.</param>
	public FusionCacheFactoryException(string? message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheFactoryException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not a null reference (Nothing in Visual Basic), the current exception is raised in a catch block that handles the inner exception.</param>
	public FusionCacheFactoryException(string? message, FusionCacheFactoryException? innerException)
		: base(message, innerException)
	{
	}
}
