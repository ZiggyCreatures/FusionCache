using System;
using System.Diagnostics;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	[DebuggerDisplay("{" + nameof(Action) + "} ON {" + nameof(CacheKey) + "} AT {" + nameof(Timestamp) + "} (EXP: {" + nameof(ExpirationTicks) + "} RET: {" + nameof(RetryCount) + "})")]
	internal sealed class AutoRecoveryItem
	{
		public AutoRecoveryItem(string cacheKey, FusionCacheAction action, long timestamp, FusionCacheEntryOptions options, long? expirationTicks, int? maxRetryCount, BackplaneMessage? message)
		{
			if (message is not null && message.CacheKey != cacheKey)
				throw new ArgumentException("The cache key of the message must match the cache key of the item", nameof(message));

			CacheKey = cacheKey;
			Action = action;
			Timestamp = timestamp;
			Options = options ?? throw new ArgumentNullException(nameof(options));
			ExpirationTicks = expirationTicks;
			RetryCount = maxRetryCount;
			Message = message;
		}

		public string CacheKey { get; }
		public FusionCacheAction Action { get; }
		public long Timestamp { get; }
		public FusionCacheEntryOptions Options { get; }
		public long? ExpirationTicks { get; }
		public int? RetryCount { get; private set; }

		// TODO: IF THERE'S NO WAY TO USE THIS, JUST REMOVE IT
		public BackplaneMessage? Message { get; }

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
}
