using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace FusionCacheTests.Stuff;

internal class LimitedCharsBackplane
	: IFusionCacheBackplane
{
	private readonly IFusionCacheBackplane _innerBackplane;
	private readonly Func<string, bool> _channelNameValidator;

	public LimitedCharsBackplane(IFusionCacheBackplane innerBackplane, Func<string, bool> channelNameValidator)
	{
		_innerBackplane = innerBackplane;
		_channelNameValidator = channelNameValidator;
	}

	private void ValidateChannelName(string? channelName)
	{
		if (channelName is null)
			throw new ArgumentNullException(nameof(channelName), "The specified channel name cannot be null.");

		if (_channelNameValidator(channelName) == false)
			throw new FusionCacheInvalidOptionsException($"The specified channel name ({channelName}) is invalid.");
	}

	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		ValidateChannelName(options.ChannelName);
		_innerBackplane.Subscribe(options);
	}

	public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions options)
	{
		ValidateChannelName(options.ChannelName);
		await _innerBackplane.SubscribeAsync(options);
	}

	public void Unsubscribe()
	{
		_innerBackplane.Unsubscribe();
	}

	public async ValueTask UnsubscribeAsync()
	{
		await _innerBackplane.UnsubscribeAsync();
	}

	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		_innerBackplane.Publish(message, options, token);
	}

	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		await _innerBackplane.PublishAsync(message, options, token);
	}
}
