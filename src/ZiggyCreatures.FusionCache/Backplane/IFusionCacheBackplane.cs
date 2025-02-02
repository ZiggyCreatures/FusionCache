using System.Threading;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Backplane;

/// <summary>
/// The core interface to create a FusionCache backplane.
/// </summary>
public interface IFusionCacheBackplane
{
	/// <summary>
	/// Subscribe to receive messages from other nodes.
	/// </summary>
	/// <param name="options">The backplane subscription options.</param>
	void Subscribe(BackplaneSubscriptionOptions options);

	/// <summary>
	/// Subscribe to receive messages from other nodes.
	/// </summary>
	/// <param name="options">The backplane subscription options.</param>
	ValueTask SubscribeAsync(BackplaneSubscriptionOptions options);

	/// <summary>
	/// Unsubscribe from receiving messages from other nodes.
	/// </summary>
	void Unsubscribe();

	/// <summary>
	/// Unsubscribe from receiving messages from other nodes.
	/// </summary>
	ValueTask UnsubscribeAsync();

	/// <summary>
	/// Send a notification to the other connected nodes, if any.
	/// </summary>
	/// <param name="message">The message to send.</param>
	/// <param name="options">The options to use.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default);

	/// <summary>
	/// Send a notification to the other connected nodes, if any.
	/// </summary>
	/// <param name="message">The message to send.</param>
	/// <param name="options">The options to use.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default);
}
