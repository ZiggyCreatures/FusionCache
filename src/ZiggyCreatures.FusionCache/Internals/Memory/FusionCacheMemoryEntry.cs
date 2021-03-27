using System;

namespace ZiggyCreatures.Caching.Fusion.Internals.Memory
{

	/// <summary>
	/// An entry in a <see cref="FusionCache"/> memory layer.
	/// </summary>
	internal class FusionCacheMemoryEntry
		: IFusionCacheEntry
	{

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="value">The actual value.</param>
		/// <param name="metadata">The metadata for the entry</param>
		public FusionCacheMemoryEntry(object? value, FusionCacheEntryMetadata? metadata)
		{
			Value = value;
			Metadata = metadata;
		}

		/// <summary>
		/// The value inside the entry.
		/// </summary>
		public object? Value { get; set; }

		/// <summary>
		/// Metadata about the cache entry.
		/// </summary>
		public FusionCacheEntryMetadata? Metadata { get; }

		/// <inheritdoc/>
		public TValue GetValue<TValue>()
		{
			return (TValue)Value;
		}

		/// <inheritdoc/>
		public void SetValue<TValue>(TValue value)
		{
			Value = value;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			if (Metadata is null)
				return "[]";

			return Metadata.ToString();
		}

		/// <summary>
		/// Creates a new <see cref="FusionCacheMemoryEntry"/> instance from a value and some options.
		/// </summary>
		/// <param name="value">The value to be cached.</param>
		/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
		/// <param name="isFromFailSafe">Indicates if the value comes from a fail-safe activation.</param>
		/// <returns>The newly created entry.</returns>
		public static FusionCacheMemoryEntry CreateFromOptions(object? value, FusionCacheEntryOptions options, bool isFromFailSafe)
		{
			if (options.IsFailSafeEnabled == false)
				return new FusionCacheMemoryEntry(value, null);

			// TODO: PROBABLY THROTTLE INSTEAD OF MAX?
			var exp = DateTimeOffset.UtcNow.Add(isFromFailSafe ? options.FailSafeMaxDuration : options.Duration);

			if (options.JitterMaxDuration > TimeSpan.Zero)
			{
				exp = exp.AddMilliseconds(options.GetJitterDurationMs());
			}

			return new FusionCacheMemoryEntry(value, new FusionCacheEntryMetadata(exp, isFromFailSafe));
		}
	}

}