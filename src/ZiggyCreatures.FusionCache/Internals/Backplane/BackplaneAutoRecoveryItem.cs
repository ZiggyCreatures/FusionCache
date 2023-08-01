using System;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Internals.Backplane
{
	internal sealed class BackplaneAutoRecoveryItem
	{
		public BackplaneAutoRecoveryItem(BackplaneMessage message, FusionCacheEntryOptions options, long expirationTicks)
		{
			Message = message ?? throw new ArgumentNullException(nameof(message));
			Options = options ?? throw new ArgumentNullException(nameof(options));
			ExpirationTicks = expirationTicks;
		}

		public BackplaneMessage Message { get; }
		public FusionCacheEntryOptions Options { get; }
		public long ExpirationTicks { get; }
	}
}
