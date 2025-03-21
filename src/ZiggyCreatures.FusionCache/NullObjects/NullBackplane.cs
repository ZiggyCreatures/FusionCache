﻿using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCacheBackplane"/> that implements the null object pattern, meaning that it does nothing. Consider this a kind of a pass-through implementation.
/// </summary>
public class NullBackplane
	: IFusionCacheBackplane
{
	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask SubscribeAsync(BackplaneSubscriptionOptions options)
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask UnsubscribeAsync()
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		return new ValueTask();
	}
}
