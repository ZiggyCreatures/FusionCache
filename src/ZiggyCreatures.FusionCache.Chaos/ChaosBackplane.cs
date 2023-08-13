using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IFusionCacheBackplane"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosBackplane
	: AbstractChaosComponent
	, IFusionCacheBackplane
{
	private readonly IFusionCacheBackplane _innerBackplane;
	private Action<BackplaneConnectionInfo>? _connectHandler;

	/// <summary>
	/// Initializes a new instance of the ChaosBackplane class.
	/// </summary>
	/// <param name="innerBackplane">The actual <see cref="IFusionCacheBackplane"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosBackplane(IFusionCacheBackplane innerBackplane, ILogger<ChaosBackplane>? logger = null)
		: base(logger)
	{
		_innerBackplane = innerBackplane ?? throw new ArgumentNullException(nameof(innerBackplane));
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		MaybeChaos();
		_innerBackplane.Publish(message, options, token);
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		await MaybeChaosAsync().ConfigureAwait(false);
		await _innerBackplane.PublishAsync(message, options, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Subscribe");

		MaybeChaos();

		_innerBackplane.Subscribe(options);
		_connectHandler = options.ConnectHandler;
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Unsubscribe");

		MaybeChaos();

		_connectHandler = null;
		_innerBackplane.Unsubscribe();
	}

	/// <inheritdoc/>
	public override void SetNeverThrow()
	{
		var _old = ChaosThrowProbability;

		base.SetNeverThrow();

		if (_old != ChaosThrowProbability)
			_connectHandler?.Invoke(new BackplaneConnectionInfo(true));
	}
}
