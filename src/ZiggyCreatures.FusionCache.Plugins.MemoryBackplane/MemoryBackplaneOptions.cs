using System;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Plugins.MemoryBackplane
{
	/// <summary>
	/// Represents the options available for the memory backplane.
	/// </summary>
	public class MemoryBackplaneOptions
		: IOptions<MemoryBackplaneOptions>
	{
		/// <summary>
		/// A simulated delay that will pass between sending and receiving notifications
		/// </summary>
		public TimeSpan? NotificationsDelay { get; set; }

		/// <summary>
		/// The prefix that will be used to construct the notification channel name.
		/// <br/><br/>
		/// NOTE: if not specified, the <see cref="IFusionCache.CacheName"/> will be used.
		/// </summary>
		public string? ChannelPrefix { get; set; }

		MemoryBackplaneOptions IOptions<MemoryBackplaneOptions>.Value
		{
			get { return this; }
		}
	}
}
