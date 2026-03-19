namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The generic exception that is thrown when a backplane error occurs: the InnerException contains the original exception.
/// </summary>
[Serializable]
public class FusionCacheBackplaneException
	: Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheBackplaneException"/> class.
	/// </summary>
	public FusionCacheBackplaneException()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheBackplaneException"/> class with a specified error message.</summary>
	/// <param name="message">The message that describes the error.</param>
	public FusionCacheBackplaneException(string? message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheBackplaneException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not a null reference (Nothing in Visual Basic), the current exception is raised in a catch block that handles the inner exception.</param>
	public FusionCacheBackplaneException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
