using System;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane
{
	internal sealed class BackplaneAutoRecoveryItem
	{
		private const int MaxRetryCount = 1_000_000;

		public BackplaneAutoRecoveryItem(BackplaneMessage message, FusionCacheEntryOptions options, long expirationTicks, int? retryCount)
		{
			Message = message ?? throw new ArgumentNullException(nameof(message));
			Options = options ?? throw new ArgumentNullException(nameof(options));
			ExpirationTicks = expirationTicks;
			RetryCount = retryCount ?? MaxRetryCount;
		}

		public BackplaneMessage Message { get; }
		public FusionCacheEntryOptions Options { get; }
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
