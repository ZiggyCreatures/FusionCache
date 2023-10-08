using System;
using System.Diagnostics;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	// TODO: MAYBE ALSO ADD A Timestamp PROP
	[DebuggerDisplay("{" + nameof(Action) + "} - {" + nameof(CacheKey) + "} expire at {" + nameof(ExpirationTicks) + "} with {" + nameof(RetryCount) + "} retries left")]
	internal sealed class AutoRecoveryItem
	{
		public AutoRecoveryItem(string cacheKey, FusionCacheAction action, FusionCacheEntryOptions options, long? expirationTicks, int retryCount, BackplaneMessage? message)
		{
			if (message is not null && message.CacheKey != cacheKey)
				throw new ArgumentException("The cache key of the message must match the cache key of the item", nameof(message));

			CacheKey = cacheKey;
			Action = action;
			Options = options ?? throw new ArgumentNullException(nameof(options));
			ExpirationTicks = expirationTicks;
			RetryCount = retryCount;
			Message = message;
		}

		public string CacheKey { get; }
		public FusionCacheAction Action { get; }
		public FusionCacheEntryOptions Options { get; }
		public long? ExpirationTicks { get; }
		public int RetryCount { get; private set; }
		public BackplaneMessage? Message { get; }

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
