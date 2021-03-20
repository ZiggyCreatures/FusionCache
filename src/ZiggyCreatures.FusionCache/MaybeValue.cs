using System;

namespace ZiggyCreatures.Caching.Fusion
{

	/// <summary>
	/// Represents maybe a value, maybe not (typically the result of a TryGet[Async] operation).
	/// <br/>
	/// It contains a <see cref="bool"/> indicating if the value is there and, if so, the value itself.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	public struct MaybeValue<TValue>
	{

		/// <summary>
		/// DEPRECATED: Represents a reusable result to be used when no value is there: using this saves memory allocations.
		/// </summary>
		[Obsolete("Please use None instead")]
		public static readonly MaybeValue<TValue> NoSuccess = new MaybeValue<TValue>();

		/// <summary>
		/// Represents a reusable result to be used when no value is there: using this saves memory allocations.
		/// </summary>
		public static readonly MaybeValue<TValue> None = new MaybeValue<TValue>();

		/// <summary>
		/// DEPRECATED: Indicates if the value is there.
		/// </summary>
		[Obsolete("Please use HasValue instead")]
		public bool Success { get { return HasValue; } }

		/// <summary>
		/// Indicates if the value is there.
		/// </summary>
		public bool HasValue { get; private set; }

		/// <summary>
		/// If the value is there (you can check <see cref="HasValue"/> to know that) the actual value is returned, otherwise an <see cref="InvalidOperationException"/> will be thrown.
		/// </summary>
		public TValue Value { get; private set; }

		/// <summary>
		/// Implements an implicit conversion between a <see cref="MaybeValue{TValue}"/> instance and a <see cref="bool"/>, to be easily used in boolean-based statements, like an if or a while.
		/// </summary>
		/// <param name="res">The <see cref="MaybeValue{TValue}"/> instance to convert.</param>
		public static implicit operator bool(MaybeValue<TValue> res)
		{
			return res.HasValue;
		}

		/// <summary>
		/// DEPRECATED: Creates a new <see cref="MaybeValue{TValue}"/> instance for a successful case by providing the <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The value of type <typeparamref name="TValue"/> to use.</param>
		/// <returns>The newly created <see cref="MaybeValue{TValue}"/> instance.</returns>
		[Obsolete("Please use FromValue instead")]
		public static MaybeValue<TValue> CreateSuccess(TValue value)
		{
			return MaybeValue<TValue>.FromValue(value);
		}

		/// <summary>
		/// Creates a new <see cref="MaybeValue{TValue}"/> instance for a successful case by providing the <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The value of type <typeparamref name="TValue"/> to use.</param>
		/// <returns>The newly created <see cref="MaybeValue{TValue}"/> instance.</returns>
		public static MaybeValue<TValue> FromValue(TValue value)
		{
			return new MaybeValue<TValue>
			{
				HasValue = true,
				Value = value
			};
		}

	}

}