using System;
using System.Runtime.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The generic exception that is thrown when a distributed cache error occurs: the InnerException contains the original exception.
/// </summary>
[Serializable]
public class FusionCacheDistributedCacheException
	: Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FusionCacheDistributedCacheException"/> class.
	/// </summary>
	public FusionCacheDistributedCacheException()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheDistributedCacheException"/> class with a specified error message.</summary>
	/// <param name="message">The message that describes the error.</param>
	public FusionCacheDistributedCacheException(string? message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheDistributedCacheException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not a null reference (Nothing in Visual Basic), the current exception is raised in a catch block that handles the inner exception.</param>
	public FusionCacheDistributedCacheException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="FusionCacheDistributedCacheException"/> class with serialized data.</summary>
	/// <param name="info">The object that holds the serialized object data.</param>
	/// <param name="context">The contextual information about the source or destination.</param>
	protected FusionCacheDistributedCacheException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
