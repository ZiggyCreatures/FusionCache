
namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Represents the internal strings used by FusionCache to process cache keys, tags, and more.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
/// </summary>
public class FusionCacheInternalStrings
{
	/// <summary>
	/// The default value for <see cref="CacheKeyPrefixSeparator"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md"/>
	/// </summary>
	public const string DefaultCacheKeyPrefixSeparator = ":";

	/// <summary>
	/// The default value for <see cref="TagCacheKeyPrefix"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public const string DefaultTagCacheKeyPrefix = "__fc:t:";

	/// <summary>
	/// The default value for <see cref="ClearRemoveTag"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public const string DefaultClearRemoveTag = "!";

	/// <summary>
	/// The default value for <see cref="ClearExpireTag"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public const string DefaultClearExpireTag = "*";

	/// <summary>
	/// The default value for <see cref="DistributedCacheWireFormatSeparator"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public const string DefaultDistributedCacheWireFormatSeparator = ":";

	/// <summary>
	/// The default value for <see cref="BackplaneWireFormatSeparator"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public const string DefaultBackplaneWireFormatSeparator = ":";

	/// <summary>
	/// The default value for <see cref="BackplaneChannelNameSeparator"/>.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public const string DefaultBackplaneChannelNameSeparator = ".";

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheInternalStrings"/> object.
	/// </summary>
	public FusionCacheInternalStrings()
	{
		// NOTE: CAN'T CALL SetToDefaults() HERE BECAUSE THE COMPILER WOULD ANYWAY KEEP
		// COMPLAINING ABOUT THE UNINITIALIZED MEMBERS (CacheKeyPrefixSeparator, etc.)
		CacheKeyPrefixSeparator = DefaultCacheKeyPrefixSeparator;
		TagCacheKeyPrefix = DefaultTagCacheKeyPrefix;
		ClearRemoveTag = DefaultClearRemoveTag;
		ClearExpireTag = DefaultClearExpireTag;
		DistributedCacheWireFormatSeparator = DefaultDistributedCacheWireFormatSeparator;
		BackplaneWireFormatSeparator = DefaultBackplaneWireFormatSeparator;
		BackplaneChannelNameSeparator = DefaultBackplaneChannelNameSeparator;
	}

	/// <summary>
	/// When FusionCache is instructed to setup the <see cref="FusionCacheOptions.CacheKeyPrefix"/> by the cache name, it combines the <see cref="FusionCacheOptions.CacheName"/> and this separator.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/NamedCaches.md"/>
	/// </summary>
	public string CacheKeyPrefixSeparator { get; set; }

	/// <summary>
	/// 
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	public string TagCacheKeyPrefix { get; set; }

	/// <summary>
	/// The special tag used to express a Clear(false), meaning "remove all".
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Clear.md"/>
	/// </summary>
	public string ClearRemoveTag { get; set; }

	/// <summary>
	/// The special tag used to express a Clear(true), meaning "expire all".
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Clear.md"/>
	/// </summary>
	public string ClearExpireTag { get; set; }

	/// <summary>
	/// The wire format version separator for the distributed cache wire format, used in the cache key processing.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md"/>
	/// </summary>
	public string DistributedCacheWireFormatSeparator { get; set; }

	/// <summary>
	/// The wire format version separator for the backplane wire format, used in the channel name.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public string BackplaneWireFormatSeparator { get; set; }

	/// <summary>
	/// The separator for the backplane channel name.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md"/>
	/// </summary>
	public string BackplaneChannelNameSeparator { get; set; }

	/// <summary>
	/// Set the internal strings used by FusionCache to use only a limited subset of commonly "safe" characters:
	/// <br/>
	/// - Latin alphanumeric chars (a-zA-Z0-9)
	/// <br/>
	/// - a preferred "separator" (default is "-")
	/// <br/>
	/// - a preferred "special char" (default is "_")
	/// <br/><br/>
	/// To see the end result, just call <see cref="GetAll"/>.
	/// <br/><br/>
	/// <strong>NOTE:</strong> if needed, the separator and special char can be the same.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	/// <param name="separator">The preferred separator.</param>
	/// <param name="specialChar">The preferred special character.</param>
	public void SetToLimited(char separator = '-', char specialChar = '_')
	{
		var s = separator.ToString();
		var sc = specialChar.ToString();

		CacheKeyPrefixSeparator = s;
		TagCacheKeyPrefix = $"{sc}{sc}fc{s}t{s}";
		ClearRemoveTag = $"{s}rem";
		ClearExpireTag = $"{s}exp";
		DistributedCacheWireFormatSeparator = s;
		BackplaneWireFormatSeparator = s;
		BackplaneChannelNameSeparator = s;
	}

	/// <summary>
	/// Reset the internal strings used by FusionCache to the default values.
	/// </summary>
	public void SetToDefaults()
	{
		CacheKeyPrefixSeparator = DefaultCacheKeyPrefixSeparator;
		TagCacheKeyPrefix = DefaultTagCacheKeyPrefix;
		ClearRemoveTag = DefaultClearRemoveTag;
		ClearExpireTag = DefaultClearExpireTag;
		DistributedCacheWireFormatSeparator = DefaultDistributedCacheWireFormatSeparator;
		BackplaneWireFormatSeparator = DefaultBackplaneWireFormatSeparator;
		BackplaneChannelNameSeparator = DefaultBackplaneChannelNameSeparator;
	}

	/// <summary>
	/// Get a list of all the internal strings used by FusionCache, as currently configured (usually used for debugging purposes).
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	/// <returns>A string array with all the currently configured internal strings.</returns>
	public string[] GetAll()
	{
		return [
			CacheKeyPrefixSeparator,
			TagCacheKeyPrefix,
			ClearRemoveTag,
			ClearExpireTag,
			DistributedCacheWireFormatSeparator,
			BackplaneWireFormatSeparator,
			BackplaneChannelNameSeparator
		];
	}

	/// <summary>
	/// Creates a new <see cref="FusionCacheInternalStrings"/> object by duplicating all the options of the current one.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	/// <returns>The newly created <see cref="FusionCacheInternalStrings"/> object.</returns>
	public FusionCacheInternalStrings Duplicate()
	{
		return new()
		{
			CacheKeyPrefixSeparator = CacheKeyPrefixSeparator,
			TagCacheKeyPrefix = TagCacheKeyPrefix,
			ClearRemoveTag = ClearRemoveTag,
			ClearExpireTag = ClearExpireTag,
			DistributedCacheWireFormatSeparator = DistributedCacheWireFormatSeparator,
			BackplaneWireFormatSeparator = BackplaneWireFormatSeparator,
			BackplaneChannelNameSeparator = BackplaneChannelNameSeparator,
		};
	}
}
