using System;
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

		_connectionLock.Wait();
		try
		{
			if (_connection is not null)
				return;

			_connection = ConnectionMultiplexer.Connect(GetConfigurationOptions());
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

		var v = GetRedisValueFromMessage(message, _logger);

		if (v.IsNull)
			return;

		token.ThrowIfCancellationRequested();

		var receivedCount = _subscriber!.Publish(_channel, v);
		if (_options.VerifyReceivedClientsCountAfterPublish && receivedCount == 0)
		{
			throw new Exception($"An error occurred while trying to send a notification of type {message.Action} for cache key {message.CacheKey} to the Redis backplane: the received count was {receivedCount}");
		}
	}
}
