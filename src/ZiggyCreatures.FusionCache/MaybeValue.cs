﻿using System;
using System.Collections.Generic;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents maybe a value, maybe not.
/// <br/>
/// It contains a <see cref="bool"/> indicating if the value is there and, if so, the value itself.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public readonly struct MaybeValue<TValue>
	: IEquatable<MaybeValue<TValue>>
{
	private readonly TValue _value;

	/// <summary>
	/// Represents a reusable result to be used when no value is there: using this saves memory allocations.
	/// </summary>
	public static readonly MaybeValue<TValue> None;

	// CTOR
	private MaybeValue(TValue value)
	{
		HasValue = true;
		_value = value;
	}

	/// <summary>
	/// Indicates if the value is there.
	/// </summary>
	public readonly bool HasValue { get; }

	/// <summary>
	/// If the value is there (you can check <see cref="HasValue"/> to know that) the actual value is returned, otherwise an <see cref="InvalidOperationException"/> will be thrown.
	/// </summary>
	public readonly TValue Value
	{
		get
		{
			if (HasValue)
				return _value;

			throw new InvalidOperationException("A value is not available for this instance");
		}
	}

	/// <summary>
	/// Get the value underlying value if there, otherwise the default value of the type <typeparamref name="TValue"/>.
	/// </summary>
	public TValue GetValueOrDefault()
	{
#pragma warning disable CS8603 // Possible null reference return.
		return HasValue ? Value : default;
#pragma warning restore CS8603 // Possible null reference return.
	}

	/// <summary>
	/// Get the value underlying value if there, otherwise the provided <paramref name="defaultValue"/>.
	/// </summary>
	/// <param name="defaultValue">A value to return if the <see cref="MaybeValue{TValue}.HasValue"/> property is <see langword="false"/>.</param>
	public TValue GetValueOrDefault(TValue defaultValue)
	{
		return HasValue ? Value : defaultValue;
	}

	/// <inheritdoc />
	public override string? ToString()
	{
		return HasValue ? Value?.ToString() : string.Empty;
	}

	/// <summary>
	/// Implements an implicit conversion from any type of value to a <see cref="MaybeValue{TValue}"/> instance with that value.
	/// </summary>
	/// <param name="value">The value to convert to a <see cref="MaybeValue{TValue}"/> instance.</param>
	public static implicit operator MaybeValue<TValue>(TValue value)
	{
		return MaybeValue<TValue>.FromValue(value);
	}

	/// <summary>
	/// Returns <see cref="MaybeValue{TValue}"/> or, if <see cref="MaybeValue{TValue}.HasValue"/> is <see langword="false"/>, throws an <see cref="InvalidOperationException"/> exception instead.
	/// </summary>
	/// <param name="maybe">The <see cref="MaybeValue{TValue}"/> instance.</param>
	public static implicit operator TValue(MaybeValue<TValue> maybe)
	{
		return maybe.Value;
	}

	/// <inheritdoc/>
	public static bool operator ==(MaybeValue<TValue> left, MaybeValue<TValue> right)
	{
		return left.Equals(right);
	}

	/// <inheritdoc/>
	public static bool operator !=(MaybeValue<TValue> left, MaybeValue<TValue> right)
	{
		return !(left == right);
	}

	/// <summary>
	/// Creates a new <see cref="MaybeValue{TValue}"/> instance for a successful case by providing the <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The value of type <typeparamref name="TValue"/> to use.</param>
	/// <returns>The newly created <see cref="MaybeValue{TValue}"/> instance.</returns>
	public static MaybeValue<TValue> FromValue(TValue value)
	{
		return new MaybeValue<TValue>(value);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return obj is MaybeValue<TValue> value && Equals(value);
	}

	/// <inheritdoc/>
	public bool Equals(MaybeValue<TValue> other)
	{
		return EqualityComparer<TValue>.Default.Equals(_value, other._value) &&
			   HasValue == other.HasValue;
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		var hashCode = 1814622215;
		hashCode = (hashCode * -1521134295) + EqualityComparer<TValue>.Default.GetHashCode(_value!);
		hashCode = (hashCode * -1521134295) + HasValue.GetHashCode();
		return hashCode;
	}
}
