using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheEntryEvictionEventArgs
		: FusionCacheEntryEventArgs
	{
		public FusionCacheEntryEvictionEventArgs(string key, EvictionReason reason)
			: base(key)
		{
			Reason = reason;
		}

		public EvictionReason Reason { get; }
	}
}
