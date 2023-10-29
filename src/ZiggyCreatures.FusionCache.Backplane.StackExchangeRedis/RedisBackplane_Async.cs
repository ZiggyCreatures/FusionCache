using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public partial class RedisBackplane
	: IFusionCacheBackplane
{
	private async ValueTask EnsureConnectionAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_connection is not null)
			return;

		await _connectionLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			if (_connection is not null)
				return;

			if (_options.ConnectionMultiplexerFactory is not null)
			{
				_connection = await _options.ConnectionMultiplexerFactory().ConfigureAwait(false);
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
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		await EnsureConnectionAsync(token).ConfigureAwait(false);

		var v = GetRedisValueFromMessage(message, _logger);

		if (v.IsNull)
			return;

		token.ThrowIfCancellationRequested();

		var receivedCount = await _subscriber!.PublishAsync(_channel, v).ConfigureAwait(false);
		//if (_options.VerifyReceivedClientsCountAfterPublish && receivedCount == 0)
		//{
		//	throw new Exception($"An error occurred while trying to send a notification of type {message.Action} for cache key {message.CacheKey} to the Redis backplane: the received count was {receivedCount}");
		//}
	}
}
