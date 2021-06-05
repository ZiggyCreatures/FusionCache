using System;
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	/// <summary>
	/// The specific <see cref="EventArgs"/> object for events related to cache entries' evictions.
	/// </summary>
	public class FusionCacheEntryEvictionEventArgs
		: FusionCacheEntryEventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FusionCacheEntryEvictionEventArgs"/> class.
		/// </summary>
		/// <param name="key">The cache key related to the event.</param>
		/// <param name="reason">The reason for the eviction.</param>
		public FusionCacheEntryEvictionEventArgs(string key, EvictionReason reason)
			: base(key)
		{
			Reason = reason;
		}

		/// <summary>
		/// The reason for the eviction.
		/// </summary>
		public EvictionReason Reason { get; }
	}
}
