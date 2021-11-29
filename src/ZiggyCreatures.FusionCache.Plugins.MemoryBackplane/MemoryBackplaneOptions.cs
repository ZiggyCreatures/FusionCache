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
