using System;
using System.Runtime.Serialization;

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed
{
	/// <summary>
	/// An entry in a <see cref="FusionCache"/> distributed layer.
	/// </summary>
	/// <typeparam name="TValue">The type of the entry's value</typeparam>
	[DataContract]
	internal sealed class FusionCacheDistributedEntry<TValue>
		: IFusionCacheEntry
	{
		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="value">The actual value.</param>
		/// <param name="metadata">The metadata for the entry</param>
		public FusionCacheDistributedEntry(TValue? value, FusionCacheEntryMetadata? metadata)
		{
			Value = value;
			Metadata = metadata;
		}

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		public FusionCacheDistributedEntry()
		{
			Value = default;
		}

		/// <summary>
		/// The value inside the entry.
		/// </summary>
		[DataMember(Name = "v", EmitDefaultValue = false)]
		public TValue? Value { get; set; }

		/// <summary>
		/// Metadata about the cache entry.
		/// </summary>
		[DataMember(Name = "m", EmitDefaultValue = false)]
		public FusionCacheEntryMetadata? Metadata { get; set; }

		/// <inheritdoc/>
		public TValue1? GetValue<TValue1>()
		{
			return (TValue1?)(object?)Value;
		}

		/// <inheritdoc/>
		public void SetValue<TValue1>(TValue1? value)
		{
			Value = (TValue?)(object?)value;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			if (Metadata is null)
				return "[]";

			return Metadata.ToString();
		}

		/// <summary>
		/// Creates a new <see cref="FusionCacheDistributedEntry{TValue}"/> instance from a value and some options.
		/// </summary>
		/// <param name="value">The value to be cached.</param>
		/// <param name="options">The <see cref="FusionCacheEntryOptions"/> object to configure the entry.</param>
		/// <param name="isFromFailSafe">Indicates if the value comes from a fail-safe activation.</param>
		/// <returns>The newly created entry.</returns>
		public static FusionCacheDistributedEntry<TValue> CreateFromOptions(TValue? value, FusionCacheEntryOptions options, bool isFromFailSafe)
		{
			if (options.IsFailSafeEnabled == false)
				return new FusionCacheDistributedEntry<TValue>(value, null);

			var exp = DateTimeOffset.UtcNow.Add(isFromFailSafe ? options.FailSafeThrottleDuration : options.Duration);

			return new FusionCacheDistributedEntry<TValue>(value, new FusionCacheEntryMetadata(exp, isFromFailSafe));
		}
	}
}
