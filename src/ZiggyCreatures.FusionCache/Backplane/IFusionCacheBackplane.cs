using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Backplane
{
	/// <summary>
	/// The core interface to create a FusionCache backplane plugin.
	/// </summary>
	public interface IFusionCacheBackplane
	{
		/// <summary>
		/// Subscribe to receive messages from other nodes.
		/// </summary>
		/// <param name="channelName">The channel name to use.</param>
		/// <param name="handler">The backplane message handler.</param>
		void Subscribe(string channelName, Action<BackplaneMessage> handler);

		/// <summary>
		/// Unsubscribe from receiving messages from other nodes.
		/// </summary>
		void Unsubscribe();

		/// <summary>
		/// Send a notification to the other connected nodes, if any.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// /// <param name="options">The options to use.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token);

		/// <summary>
		/// Send a notification to the other connected nodes, if any.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <param name="options">The options to use.</param>
		void Publish(BackplaneMessage message, FusionCacheEntryOptions options);
	}
}
