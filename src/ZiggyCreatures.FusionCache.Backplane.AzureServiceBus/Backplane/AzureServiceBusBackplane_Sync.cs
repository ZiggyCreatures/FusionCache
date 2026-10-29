namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus;

public partial class AzureServiceBusBackplane
{
	// UNLIKE RedisBackplane, THE Azure.Messaging.ServiceBus SDK IS ASYNC-ONLY: THERE IS NO NATIVE SYNC API TO CALL INTO,
	// SO THESE METHODS BLOCK ON THEIR ASYNC COUNTERPARTS RATHER THAN BEING INDEPENDENT IMPLEMENTATIONS.

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		PublishAsync(message, options, token).GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		SubscribeAsync(options).GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		UnsubscribeAsync().GetAwaiter().GetResult();
	}
}
