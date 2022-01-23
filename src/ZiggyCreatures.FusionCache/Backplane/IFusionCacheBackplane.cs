using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.Backplane
{
	/// <summary>
	/// The core interface to create a FusionCache backplane plugin.
	/// </summary>
	public interface IFusionCacheBackplane
		: IFusionCachePlugin
	{
		/// <summary>
		/// Tries to send a notification to other nodes connected to the same backplane, if any.
		/// </summary>
		/// <param name="cache">The FusionCache instance on which to operate.</param>
		/// <param name="message">The message to send.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		ValueTask SendNotificationAsync(IFusionCache cache, BackplaneMessage message, CancellationToken token);

		/// <summary>
		/// Tries to send a notification to other nodes connected to the same backplane, if any.
		/// </summary>
		/// <param name="cache">The FusionCache instance on which to operate.</param>
		/// <param name="message">The message to send.</param>
		void SendNotification(IFusionCache cache, BackplaneMessage message);
	}
}
