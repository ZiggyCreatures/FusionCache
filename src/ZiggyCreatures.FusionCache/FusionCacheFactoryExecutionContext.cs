using System;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Models the execution context passed to a FusionCache factory.
/// <br/>
/// It contains various things, like entry options modifiable during factory execution (see Adaptive Caching), LastModified/Etag (see Conditional Refresh) and some useful methods.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md"/>
/// <br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
/// </summary>
public class FusionCacheFactoryExecutionContext<TValue>
{
	private FusionCacheEntryOptions _options;

	private FusionCacheFactoryExecutionContext(FusionCacheEntryOptions options, MaybeValue<TValue> staleValue, string? etag, DateTimeOffset? lastModified, string[]? tags)
	{
		if (options is null)
			throw new ArgumentNullException(nameof(options));

		_options = options;
		LastModified = lastModified;
		ETag = etag;
		StaleValue = staleValue;
		Tags = tags;
	}

	/// <summary>
	/// The options currently used: they can be modified to achieve Adaptive Caching.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md"/>
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

	internal bool HasFailed
	{
		get { return ErrorMessage is not null; }
	}

	internal string? ErrorMessage
	{
		get;
		private set;
	}

	/// <summary>
	/// The stale value, maybe.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public MaybeValue<TValue> StaleValue { get; }

	/// <summary>
	/// Indicates if there is a cached stale value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public bool HasStaleValue
	{
		get { return StaleValue.HasValue; }
	}

	/// <summary>
	/// If provided, it's the ETag of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-None-Match" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public string? ETag { get; set; }

	/// <summary>
	/// Indicates if there is an ETag value for the cached value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public bool HasETag
	{
		get { return string.IsNullOrWhiteSpace(ETag) == false; }
	}

	/// <summary>
	/// If provided, it's the last modified date of the entry: this may be used in the next refresh cycle (eg: with the use of the "If-Modified-Since" header in an http request) to check if the entry is changed, to avoid getting the entire value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public DateTimeOffset? LastModified { get; set; }

	/// <summary>
	/// Indicates if there is a LastModified value for the cached value.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	public bool HasLastModified
	{
		get { return LastModified.HasValue; }
	}

	/// <summary>
	/// The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public string[]? Tags { get; set; }

	/// <summary>
	/// For when the value is not modified, so that the stale value can be automatically returned.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	/// <returns>The stale value, for when it is not changed.</returns>
	public TValue NotModified()
	{
		return StaleValue.Value;
	}

	/// <summary>
	/// For when the value is modified, so that the new value can be returned and cached, along with the ETag and/or the LastModified values.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/ConditionalRefresh.md"/>
	/// </summary>
	/// <param name="value">The new value.</param>
	/// <param name="etag">The new value for the <see cref="ETag"/> property.</param>
	/// <param name="lastModified">The new value for the <see cref="LastModified"/> property.</param>
	/// <returns>The new value to be cached.</returns>
	public TValue Modified(TValue value, string? etag = null, DateTimeOffset? lastModified = null)
	{
		ETag = etag;
		LastModified = lastModified;
		return value;
	}

	/// <summary>
	/// For when a fail occurs, without the need to throw an exception.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md"/>
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <returns>A placeholder value used to keep the normal flow.</returns>
	public TValue Fail(string errorMessage)
	{
		ErrorMessage = errorMessage;
		if (string.IsNullOrWhiteSpace(ErrorMessage))
			ErrorMessage = "An error occurred while running the factory";

		return default!;
	}

	internal static FusionCacheFactoryExecutionContext<TValue> CreateFromEntries(FusionCacheEntryOptions options, FusionCacheDistributedEntry<TValue>? distributedEntry, IFusionCacheMemoryEntry? memoryEntry, string[]? tags)
	{
		MaybeValue<TValue> staleValue;
		string? etag;
		DateTimeOffset? lastModified;

		if (distributedEntry is not null)
		{
			staleValue = MaybeValue<TValue>.FromValue(distributedEntry.GetValue<TValue>());
			etag = distributedEntry.Metadata?.ETag;
			if (distributedEntry.Tags is not null)
				tags = distributedEntry.Tags;
			lastModified = distributedEntry.Metadata?.LastModified;
		}
		else if (memoryEntry is not null)
		{
			staleValue = MaybeValue<TValue>.FromValue(memoryEntry.GetValue<TValue>());
			etag = memoryEntry.Metadata?.ETag;
			if (memoryEntry.Tags is not null)
				tags = memoryEntry.Tags;
			lastModified = memoryEntry.Metadata?.LastModified;
		}
		else
		{
			staleValue = default;
			etag = null;
			lastModified = null;
		}

		return new FusionCacheFactoryExecutionContext<TValue>(options, staleValue, etag, lastModified, tags);
	}
}
