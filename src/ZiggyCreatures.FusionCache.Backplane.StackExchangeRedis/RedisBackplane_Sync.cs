﻿using System;
using System.Threading;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public partial class RedisBackplane
	: IFusionCacheBackplane
{
	private void EnsureConnection(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_connection is not null)
			return;

		_connectionLock.Wait(token);
		try
		{
			if (_connection is not null)
				return;

			if (_options.ConnectionMultiplexerFactory is not null)
			{
				_connection = _options.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
			}
			else
			{
				_connection = ConnectionMultiplexer.Connect(GetConfigurationOptions());
			}

			if (_connection is not null)
			{
				_connection.ConnectionRestored += OnReconnect;
				_connectHandler?.Invoke(new BackplaneConnectionInfo(false));
			}
		}
		finally
		{
			_connectionLock.Release();
		}

		if (_connection is null)
			throw new NullReferenceException("A connection to Redis is not available");

		EnsureSubscriber();
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		EnsureConnection(token);

		var value = GetRedisValueFromMessage(message, _logger);

		if (value.IsNull)
			return;

		token.ThrowIfCancellationRequested();

		_subscriber!.Publish(_channel, value);
	}
}
