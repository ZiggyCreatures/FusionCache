using System;

namespace ZiggyCreatures.Caching.Fusion
{

	/// <summary>
	/// Represents the result of a TryGet[Async] operation: it contains a <see cref="bool"/> indicating if the value has been found, and either the found value or a default value instead.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	public struct TryGetResult<TValue>
	{

		private TValue _value;

		/// <summary>
		/// Represents a reusable result to be used when no value has been found in the cache. Using this saves memory allocations.
		/// </summary>
		public static readonly TryGetResult<TValue> NoSuccess = new TryGetResult<TValue>();

		/// <summary>
		/// Indicates if the value was in the cache.
		/// </summary>
		public bool Success { get; private set; }

		/// <summary>
		/// If the value is there (you can check <see cref="Success"/> to know that) the actual value is returned, otherwise an <see cref="InvalidOperationException"/> will be thrown.
		/// </summary>
		public TValue Value
		{
			get
			{
				if (Success)
					return _value;

				throw new InvalidOperationException("A value is not available for this instance");
			}
			private set
			{
				Success = true;
				_value = value;
			}
		}

		/// <summary>
		/// Retrieves the value of the current <see cref="TryGetResult{TValue}"/> object, or the provided <paramref name="defaultValue"/> parameter instead.
		/// </summary>
		/// <param name="defaultValue">A value to return if the <see cref="TryGetResult{TValue}.Success"/> property is false.</param>
		public TValue GetValueOrDefault(TValue defaultValue = default)
		{
			return Success ? Value : defaultValue;
		}

		/// <inheritdoc />
		public override string? ToString()
		{
			return Success ? Value?.ToString() : string.Empty;
		}

		/// <summary>
		/// Implements an implicit conversion between a <see cref="TryGetResult{TValue}"/> instance and a <see cref="bool"/>, to be easily used in boolean-based statements, like an if or a while.
		/// </summary>
		/// <param name="res">The <see cref="TryGetResult{TValue}"/> instance to convert.</param>
		public static implicit operator bool(TryGetResult<TValue> res)
		{
			return res.Success;
		}

		/// <summary>
		/// Creates a new <see cref="TryGetResult{TValue}"/> instance for a successful case by providing the <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The value of type <typeparamref name="TValue"/> to use.</param>
		/// <returns>THe newly created <see cref="TryGetResult{TValue}"/> instance.</returns>
		public static TryGetResult<TValue> CreateSuccess(TValue value)
		{
			return new TryGetResult<TValue>
			{
				Success = true,
				Value = value
			};
		}

	}

}