using System;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheEntryEventArgs : EventArgs
	{
		public FusionCacheEntryEventArgs(string key)
		{
			Key = key;
		}

		public string Key { get; }
	}
}
