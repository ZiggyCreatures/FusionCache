using System;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane
{
	internal sealed class BackplaneAutoRecoveryItem
	{
		public BackplaneAutoRecoveryItem(BackplaneMessage message, FusionCacheEntryOptions options, long expirationTicks, bool preSyncDistributedCache, int retryCount)
		{
			Message = message ?? throw new ArgumentNullException(nameof(message));
			Options = options ?? throw new ArgumentNullException(nameof(options));
			ExpirationTicks = expirationTicks;
			PreSyncDistributedCache = preSyncDistributedCache;
			RetryCount = retryCount;
		}

		public BackplaneMessage Message { get; }
		public FusionCacheEntryOptions Options { get; }
		public bool PreSyncDistributedCache { get; }
		public long ExpirationTicks { get; }
		public int RetryCount { get; private set; }

		public bool IsExpired()
		{
			return ExpirationTicks <= DateTimeOffset.UtcNow.Ticks;
		}

		public void RecordRetry()
		{
			RetryCount--;
		}

		public bool CanRetry()
		{
			return RetryCount > 0;
		}
	}
}
