using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace FusionCacheTests.Stuff;

/// <summary>
/// An <see cref="IFusionCacheBackplane"/> wrapper that, for the messages matching a filter, waits for a gate to be opened before actually publishing.
/// <br/>
/// This allows to deterministically delay the delivery of a message, to simulate messages arriving out of order.
/// </summary>
internal class GatedBackplane
	: IFusionCacheBackplane
{
	private readonly IFusionCacheBackplane _innerBackplane;
	private readonly Func<BackplaneMessage, bool> _messageFilter;
	private readonly TaskCompletionSource<bool> _publishReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private int _isEnabled;

	public GatedBackplane(IFusionCacheBackplane innerBackplane, Func<BackplaneMessage, bool> messageFilter)
	{
		_innerBackplane = innerBackplane ?? throw new ArgumentNullException(nameof(innerBackplane));
		_messageFilter = messageFilter ?? throw new ArgumentNullException(nameof(messageFilter));
	}

	public Task PublishReached => _publishReached.Task;

	public bool WaitForPublishReached(TimeSpan timeout)
	{
		return ((IAsyncResult)_publishReached.Task).AsyncWaitHandle.WaitOne(timeout);
	}

	public void Enable()
	{
		Interlocked.Exchange(ref _isEnabled, 1);
	}

	public void OpenGate()
	{
		_gate.TrySetResult(true);
	}

	private bool ShouldGate(BackplaneMessage message)
	{
		return _messageFilter(message) && Interlocked.CompareExchange(ref _isEnabled, 0, 1) == 1;
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
		if (ShouldGate(message))
		{
			_publishReached.TrySetResult(true);
			_gate.Task.GetAwaiter().GetResult();
		}

		_innerBackplane.Publish(message, options, token);
	}

	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		if (ShouldGate(message))
		{
			_publishReached.TrySetResult(true);
			await _gate.Task.ConfigureAwait(false);
		}

		await _innerBackplane.PublishAsync(message, options, token).ConfigureAwait(false);
	}
}
