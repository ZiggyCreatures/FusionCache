﻿using System;
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
	private Action<BackplaneConnectionInfo>? _innerConnectHandler;
	private Action<BackplaneMessage>? _innerIncomingMessageHandler;

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
		MaybeChaos(token);
		_innerBackplane.Publish(message, options, token);
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		await MaybeChaosAsync(token).ConfigureAwait(false);
		await _innerBackplane.PublishAsync(message, options, token).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions options)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Subscribe");

		MaybeChaos();

		_innerConnectHandler = options.ConnectHandler;
		_innerIncomingMessageHandler = options.IncomingMessageHandler;

		var innerOptions = new BackplaneSubscriptionOptions(
			options.CacheName,
			options.CacheInstanceId,
			options.ChannelName,
			OnConnect,
			OnIncomingMessage
		);

		_innerBackplane.Subscribe(innerOptions);
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION ChaosBackplane: Unsubscribe");

		MaybeChaos();

		_innerConnectHandler = null;
		_innerIncomingMessageHandler = null;

		_innerBackplane.Unsubscribe();
	}

	/// <inheritdoc/>
	public override void SetNeverThrow()
	{
		var old = ChaosThrowProbability;

		base.SetNeverThrow();

		if (old != ChaosThrowProbability)
			OnConnect(new BackplaneConnectionInfo(true));
	}

	void OnConnect(BackplaneConnectionInfo info)
	{
		if (ShouldThrow())
			return;

		_innerConnectHandler?.Invoke(info);
	}

	void OnIncomingMessage(BackplaneMessage message)
	{
		if (ShouldThrow())
			return;

		_innerIncomingMessageHandler?.Invoke(message);
	}
}
