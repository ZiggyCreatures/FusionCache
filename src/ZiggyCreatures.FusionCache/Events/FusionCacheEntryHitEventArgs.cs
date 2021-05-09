namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheEntryHitEventArgs : FusionCacheEntryEventArgs
	{
		public FusionCacheEntryHitEventArgs(string key, bool isStale)
			: base(key)
		{
			IsStale = isStale;
		}

		public bool IsStale { get; }
	}
}
