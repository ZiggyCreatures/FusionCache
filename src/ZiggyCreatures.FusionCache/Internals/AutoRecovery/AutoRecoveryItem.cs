using System.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Internals.AutoRecovery;

[DebuggerDisplay("{" + nameof(Action) + "} ON {" + nameof(CacheKey) + "} AT {" + nameof(Timestamp) + "} (EXP: {" + nameof(ExpirationTicks) + "} RET: {" + nameof(RetryCount) + "})")]
internal sealed class AutoRecoveryItem
{
	public AutoRecoveryItem(string cacheKey, FusionCacheAction action, long timestamp, FusionCacheEntryOptions options, long? expirationTicks, int? maxRetryCount)
	{
		CacheKey = cacheKey;
		Action = action;
		Timestamp = timestamp;
		Options = options ?? throw new ArgumentNullException(nameof(options));
		ExpirationTicks = expirationTicks;
		RetryCount = maxRetryCount;
	}

	public string CacheKey { get; }
	public FusionCacheAction Action { get; }
	public long Timestamp { get; }
	public FusionCacheEntryOptions Options { get; }
	public long? ExpirationTicks { get; }
	public int? RetryCount { get; private set; }

	public bool IsExpired()
	{
		return ExpirationTicks <= DateTimeOffset.UtcNow.Ticks;
	}

	public void RecordRetry()
	{
		if (RetryCount is not null)
			RetryCount--;
	}

	public bool CanRetry()
	{
		if (RetryCount is null)
			return true;

		return RetryCount.Value > 0;
	}
}
