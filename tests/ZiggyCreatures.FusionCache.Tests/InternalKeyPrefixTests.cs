using System.Globalization;
using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.DangerZone;

namespace FusionCacheTests;

/// <summary>
/// Internal keys (tags, clear) are identified by a prefix, which must be matched ordinally: under a linguistic
/// (culture-sensitive) comparison, ignorable chars and combining marks would make user keys and tags be misclassified.
/// </summary>
public partial class InternalKeyPrefixTests
	: AbstractTests
{
	public InternalKeyPrefixTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	// THE BEHAVIOR OF A LINGUISTIC COMPARISON IS THE SAME UNDER EVERY CULTURE: INVARIANT, PLUS ONE WITH SPECIAL CASING RULES
	private static readonly string[] Cultures = ["", "tr-TR"];

	// USER KEYS WITH CHARS THAT ARE IGNORED BY A LINGUISTIC COMPARISON, AROUND OR INSIDE THE INTERNAL TAG KEY PREFIX
	private static readonly string[] SpecialKeys =
	[
		"plain-key",
		"\u00AD__fc:t:foo", // SOFT HYPHEN BEFORE THE PREFIX
		"_\u00AD_fc:t:foo", // SOFT HYPHEN INSIDE THE PREFIX
		"\u200D__fc:t:foo", // ZERO WIDTH JOINER BEFORE THE PREFIX
		"__fc:t\u200B:foo", // ZERO WIDTH SPACE INSIDE THE PREFIX
		"\u0000__fc:t:foo", // NUL BEFORE THE PREFIX
	];

	// TAGS STARTING WITH A COMBINING MARK: THE INTERNAL KEY (PREFIX + TAG) ENDS THE PREFIX IN THE MIDDLE OF A COMBINING SEQUENCE
	private static readonly string[] SpecialTags =
	[
		"tag",
		"\u0301tag", // STARTS WITH A COMBINING ACUTE ACCENT
		"\u0307tag", // STARTS WITH A COMBINING DOT ABOVE
	];

	public static TheoryData<string, string> CulturesAndKeys()
	{
		var res = new TheoryData<string, string>();
		foreach (var culture in Cultures)
		{
			foreach (var key in SpecialKeys)
				res.Add(culture, key);
		}
		return res;
	}

	public static TheoryData<string, string> CulturesAndTags()
	{
		var res = new TheoryData<string, string>();
		foreach (var culture in Cultures)
		{
			foreach (var tag in SpecialTags)
				res.Add(culture, tag);
		}
		return res;
	}

	public static TheoryData<string, string, bool> CulturesAndKeysAndClearKinds()
	{
		var res = new TheoryData<string, string, bool>();
		foreach (var culture in Cultures)
		{
			foreach (var key in SpecialKeys)
			{
				res.Add(culture, key, false);
				res.Add(culture, key, true);
			}
		}
		return res;
	}

	private sealed class CultureScope
		: IDisposable
	{
		private readonly CultureInfo _oldCulture;
		private readonly CultureInfo _oldUICulture;

		public CultureScope(string name)
		{
			_oldCulture = CultureInfo.CurrentCulture;
			_oldUICulture = CultureInfo.CurrentUICulture;
			var culture = name == "" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
			CultureInfo.CurrentCulture = culture;
			CultureInfo.CurrentUICulture = culture;
		}

		public void Dispose()
		{
			CultureInfo.CurrentCulture = _oldCulture;
			CultureInfo.CurrentUICulture = _oldUICulture;
		}
	}

	private static readonly TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
	private static readonly TimeSpan MultiNodeOperationsDelay = TimeSpan.FromMilliseconds(300);

	private FusionCache CreateCache(bool useExternalMemoryCache = false)
	{
		// AN EXTERNAL MEMORY CACHE DISABLES THE RAW CLEAR, SO THAT CLEAR GOES THROUGH THE INTERNAL TAG KEYS
		var memoryCache = useExternalMemoryCache ? new MemoryCache(new MemoryCacheOptions()) : null;
		return new FusionCache(new FusionCacheOptions() { IncludeTagsInLogs = true }, memoryCache, logger: CreateXUnitLogger<FusionCache>());
	}

	private FusionCache CreateCacheWithBackplane(string backplaneConnectionId, string instanceId, string cacheName)
	{
		var options = new FusionCacheOptions
		{
			CacheName = cacheName,
			WaitForInitialBackplaneSubscribe = true,
			IncludeTagsInLogs = true,
			EnableSyncEventHandlersExecution = true,
		};
		options.SetInstanceId(instanceId);

		var cache = new FusionCache(options, logger: CreateXUnitLogger<FusionCache>());
		cache.DefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
		cache.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = backplaneConnectionId }, logger: CreateXUnitLogger<MemoryBackplane>()));
		return cache;
	}
}
