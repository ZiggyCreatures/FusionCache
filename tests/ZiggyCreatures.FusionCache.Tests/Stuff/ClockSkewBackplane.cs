using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IFusionCacheBackplane"/> wrapper that shifts the timestamp of every published message by a fixed offset.
/// <br/>
/// This allows to simulate a node whose clock is not in sync with the clocks of the other nodes.
/// </summary>
internal class ClockSkewBackplane
	: IFusionCacheBackplane
{
	private readonly IFusionCacheBackplane _innerBackplane;
	private readonly TimeSpan _skew;

	public ClockSkewBackplane(IFusionCacheBackplane innerBackplane, TimeSpan skew)
	{
		_innerBackplane = innerBackplane ?? throw new ArgumentNullException(nameof(innerBackplane));
		_skew = skew;
	}

	private BackplaneMessage Skew(BackplaneMessage message)
	{
		var res = BackplaneMessage.FromByteArray(BackplaneMessage.ToByteArray(message));
		res.Timestamp += _skew.Ticks;
		return res;
	}

	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		_innerBackplane.Subscribe(options);
	}

	public ValueTask SubscribeAsync(BackplaneSubscriptionOptions options)
	{
		return _innerBackplane.SubscribeAsync(options);
	}

	public void Unsubscribe()
	{
		_innerBackplane.Unsubscribe();
	}

	public ValueTask UnsubscribeAsync()
	{
		return _innerBackplane.UnsubscribeAsync();
	}

	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		_innerBackplane.Publish(Skew(message), options, token);
	}

	public ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		return _innerBackplane.PublishAsync(Skew(message), options, token);
	}
}
