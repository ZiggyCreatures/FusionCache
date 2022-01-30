using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Backplane
{
	/// <summary>
	/// The core interface to create a FusionCache backplane plugin.
	/// </summary>
	public interface IFusionCacheBackplane
	{
		/// <summary>
		/// Start receiving notifications.
		/// </summary>
		/// <param name="channelName">The channel name to use.</param>
		void Subscribe(string channelName);

		/// <summary>
		/// Stop receiving notifications.
		/// </summary>
		void Unsubscribe();

		/// <summary>
		/// Tries to send a notification to other nodes connected to the same backplane, if any.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// /// <param name="options">The options to use.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		ValueTask SendNotificationAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token);

		/// <summary>
		/// Tries to send a notification to other nodes connected to the same backplane, if any.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <param name="options">The options to use.</param>
		void SendNotification(BackplaneMessage message, FusionCacheEntryOptions options);

		/// <summary>
		/// The event for a new message.
		/// </summary>
		event EventHandler<FusionCacheBackplaneMessageEventArgs>? Message;
	}
}
