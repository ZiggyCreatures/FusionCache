using System;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// The specific <see cref="EventArgs"/> object for events related to cache entries (eg: with a cache key).
	/// </summary>
	public class FusionCacheEntryEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheEntryEventArgs"/> class.
		/// </summary>
		/// <param name="key">The cache key related to the event.</param>
		public FusionCacheEntryEventArgs(string key)
		{
			Key = key;
		}

		/// <summary>
		/// The cache key related to the event.
		/// </summary>
		public string Key { get; }
	}
}
