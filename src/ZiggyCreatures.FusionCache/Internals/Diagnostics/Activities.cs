using System.Collections.Generic;
using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

internal static class Activities
{
	public static readonly ActivitySource Source = new ActivitySource(FusionCacheDiagnostics.ActivitySourceName, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly ActivitySource SourceMemoryLevel = new ActivitySource(FusionCacheDiagnostics.ActivitySourceNameMemoryLevel, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly ActivitySource SourceDistributedLevel = new ActivitySource(FusionCacheDiagnostics.ActivitySourceNameDistributedLevel, FusionCacheDiagnostics.FusionCacheVersion);
	public static readonly ActivitySource SourceBackplane = new ActivitySource(FusionCacheDiagnostics.ActivitySourceNameBackplane, FusionCacheDiagnostics.FusionCacheVersion);

	internal static class Names
	{
		// HIGH-LEVEL
		public const string Set = "set to cache";
		public const string TryGet = "get from cache";
		public const string GetOrDefault = "get from cache";
		public const string GetOrSet = "get or set from cache";
		public const string Remove = "remove from cache";
		public const string Expire = "expire from cache";
		public const string RemoveByTag = "remove from cache by tag";
		public const string Clear = "clear cache";

		// MEMORY
		public const string MemorySet = "set to cache level";
		public const string MemoryGet = "get from cache level";
		public const string MemoryExpire = "expire from cache level";
		public const string MemoryRemove = "remove from cache level";

		// DISTRIBUTED
		public const string DistributedSet = "set to cache level";
		public const string DistributedGet = "get from cache level";
		public const string DistributedRemove = "remove from cache level";

		// BACKPLANE
		public const string BackplanePublish = "publish to backplane";
		public const string BackplaneReceive = "receive from backplane";

		// FACTORY
		public const string ExecuteFactory = "execute factory";

		// AUTO-RECOVERY
		public const string AutoRecoveryProcessQueue = "process auto-recovery queue";
		public const string AutoRecoveryProcessItem = "process auto-recovery item";
	}

	internal static class EventNames
	{
		public const string FactoryBackgroundMove = "factory moved to the background";
		public const string FactoryBackgroundMoveNotAllowed = "factory not allowed to be moved to the background";
		public const string BackplaneIncomingMessageInvalid = "incoming message invalid";
		public const string BackplaneIncomingMessageConflicts = "incoming message with conflicts";
		public const string BackplaneIncomingMessageUnknownAction = "incoming message with unknown action";
	}

	public static IEnumerable<KeyValuePair<string, object?>> GetCommonTags(string? cacheName, string? cacheInstanceId, string? key, string? operationId, CacheLevelKind? levelKind)
	{
		var res = new List<KeyValuePair<string, object?>>
		{
			new KeyValuePair<string, object?>(Tags.Names.CacheName, cacheName),
			new KeyValuePair<string, object?>(Tags.Names.CacheInstanceId, cacheInstanceId),
			new KeyValuePair<string, object?>(Tags.Names.OperationKey, key),
			new KeyValuePair<string, object?>(Tags.Names.OperationId, operationId),
		};

		if (levelKind is not null)
			res.Add(new KeyValuePair<string, object?>(Tags.Names.OperationLevel, levelKind.ToString()?.ToLowerInvariant()));

		return res;
	}

	public static Activity? StartActivityWithCommonTags(this ActivitySource source, string activityName, string? cacheName, string? cacheInstanceId, string? key, string? operationId, CacheLevelKind? levelKind = null)
	{
		if (source.HasListeners() == false)
			return null;

		return source.StartActivity(
			ActivityKind.Internal,
			tags: GetCommonTags(cacheName, cacheInstanceId, key, operationId, levelKind),
			name: activityName
		);
	}
}
