using System;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Models the execution context passed to a FusionCache factory. Right now it just contains the options so they can be modified based of the factory execution (see adaptive caching), but in the future this may contain more.
/// </summary>
public abstract class FusionCacheFactoryExecutionContext
{
	private FusionCacheEntryOptions _options;

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="options">The options to start from.</param>
	/// <param name="lastModified">If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	/// <param name="etag">If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.</param>
	protected FusionCacheFactoryExecutionContext(FusionCacheEntryOptions options, DateTimeOffset? lastModified, string? etag)
	{
		if (options is null)
			throw new ArgumentNullException(nameof(options));

		_options = options;
		LastModified = lastModified;
		ETag = etag;
	}

	/// <summary>
	/// The options currently used, and that can be modified or changed completely.
	/// </summary>
	public FusionCacheEntryOptions Options
	{
		get
		{
			if (_options.IsSafeForAdaptiveCaching == false)
			{
				_options = _options.EnsureIsSafeForAdaptiveCaching();
			}

			return _options;
		}
		set
		{
			if (value is null)
				throw new NullReferenceException("The new Options value cannot be null");

			_options = value.SetIsSafeForAdaptiveCaching();
		}
	}

	internal FusionCacheEntryOptions GetOptions()
	{
		return _options;
	}

	/// <summary>
	/// Tries to get the previous stale value, in present.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <returns>The stale value, if present, in the form of a <see cref="MaybeValue{TValue}"/>.</returns>
	public abstract MaybeValue<TValue> TryGetStaleValue<TValue>();

	/// <summary>
	/// If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	public DateTimeOffset? LastModified { get; set; }

	/// <summary>
	/// If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// </summary>
	public string? ETag { get; set; }
}
