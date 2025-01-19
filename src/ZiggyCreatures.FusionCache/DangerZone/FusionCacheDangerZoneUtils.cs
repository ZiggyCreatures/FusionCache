namespace ZiggyCreatures.Caching.Fusion.DangerZone;

/// <summary>
/// Utilities and extension methods that are dangerous to use, but may somehow be useful although only in some very <strong>very</strong> rare scenarios.
/// <br/><br/>
/// <strong>⚠️ WARNING:</strong> please, use with great care and only if you are really sure.
/// </summary>
public static class FusionCacheDangerZoneUtils
{
	/// <summary>
	/// Set the InstanceId of the cache, but please don't use this.
	/// <br/><br/>
	/// <strong>⚠ WARNING:</strong> again, this should NOT be set, basically never ever, unless you really know what you are doing. For example by using the same value for two different cache instances they will be considered as the same cache instance, and this will lead to critical errors. So again, really: you should not use this.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="instanceId">The value for <see cref="FusionCacheOptions.InstanceId"/>.</param>
	public static void SetInstanceId(this FusionCacheOptions options, string instanceId)
	{
		options.SetInstanceIdInternal(instanceId);
	}
}
