using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace ZiggyCreatures.Caching.Fusion.Chaos;

/// <summary>
/// An implementation of <see cref="IFusionCacheBackplane"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
/// </summary>
public class ChaosBackplane
	: IFusionCacheBackplane
{
	private readonly IFusionCacheBackplane _innerBackplane;
	private readonly ILogger<ChaosBackplane>? _logger;
	private Action<BackplaneConnectionInfo>? _connectHandler;

	/// <summary>
	/// Initializes a new instance of the ChaosBackplane class.
	/// </summary>
	/// <param name="innerBackplane">The actual <see cref="IFusionCacheBackplane"/> used if and when chaos does not happen.</param>
	/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
	public ChaosBackplane(IFusionCacheBackplane innerBackplane, ILogger<ChaosBackplane>? logger = null)
	{
		_innerBackplane = innerBackplane ?? throw new ArgumentNullException(nameof(innerBackplane));
		_logger = logger;

		ChaosThrowProbability = 0f;
		ChaosMinDelay = TimeSpan.Zero;
		ChaosMaxDelay = TimeSpan.Zero;
	}

	/// <summary>
	/// A <see cref="float"/> value from 0.0 to 1.0 that represents the probabilty of throwing an exception: set it to 0.0 to never throw or to 1.0 to always throw.
	/// </summary>
	public float ChaosThrowProbability { get; set; }

	/// <summary>
	/// The minimum amount of randomized delay.
	/// </summary>
	public TimeSpan ChaosMinDelay { get; set; }

	/// <summary>
	/// The maximum amount of randomized delay.
	/// </summary>
	public TimeSpan ChaosMaxDelay { get; set; }

	/// <summary>
	/// Force chaos exceptions to never be thrown.
	/// </summary>
	public void SetNeverThrow()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetNeverThrow");

		var _old = ChaosThrowProbability;
		ChaosThrowProbability = 0f;

		if (_old != ChaosThrowProbability)
			_connectHandler?.Invoke(new BackplaneConnectionInfo(true));
	}

	/// <summary>
	/// Force chaos exceptions to always be thrown.
	/// </summary>
	public void SetAlwaysThrow()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetAlwaysThrow");

		ChaosThrowProbability = 1f;
	}

	/// <summary>
	/// Force chaos delays to never happen.
	/// </summary>
	public void SetNeverDelay()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetNeverDelay");

		ChaosMinDelay = TimeSpan.Zero;
		ChaosMaxDelay = TimeSpan.Zero;
	}

	/// <summary>
	/// Force chaos delays to always be of exactly this amount.
	/// </summary>
	/// <param name="delay"></param>
	public void SetAlwaysDelayExactly(TimeSpan delay)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetAlwaysDelayExactly");

		ChaosMinDelay = delay;
		ChaosMaxDelay = delay;
	}

	/// <summary>
	/// Force chaos exceptions and delays to never happen.
	/// </summary>
	public void SetNeverChaos()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetNeverChaos");

		SetNeverThrow();
		SetNeverDelay();
	}

	/// <summary>
	/// Force chaos exceptions to always throw, and chaos delays to always be of exactly this amount.
	/// </summary>
	/// <param name="delay"></param>
	public void SetAlwaysChaos(TimeSpan delay)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: SetAlwaysChaos");

		SetAlwaysThrow();
		SetAlwaysDelayExactly(delay);
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		FusionCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
		_innerBackplane.Publish(message, options, token);
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		await FusionCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
		await _innerBackplane.PublishAsync(message, options, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Subscribe");

		_innerBackplane.Subscribe(options);
		_connectHandler = options.ConnectHandler;
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Unsubscribe");

		_connectHandler = null;
		_innerBackplane.Unsubscribe();
	}
}
