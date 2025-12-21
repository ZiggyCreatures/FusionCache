using Medallion.Threading.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZiggyCreatures.Caching.Fusion.Locking.Redis;

/// <summary>
/// A Redis based implementation of a FusionCache distributed locker.
/// </summary>
public partial class RedisDistributedLocker
	: IFusionCacheDistributedLocker
	, IDisposable
{
	private RedisDistributedLockerOptions _options;
	private readonly ILogger? _logger;

	private readonly SemaphoreSlim _connectionLock;
	private IConnectionMultiplexer? _connection;

	private RedisDistributedSynchronizationProvider? _provider = null;

	/// <summary>
	/// Initializes a new instance of the RedisDistributedLocker class.
	/// </summary>
	/// <param name="optionsAccessor">The set of options to use with this instance of the distributed locker.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If <see langword="null"/>, logging will be completely disabled.</param>
	public RedisDistributedLocker(IOptions<RedisDistributedLockerOptions> optionsAccessor, ILogger<RedisDistributedLocker>? logger = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// LOGGING
		if (logger is NullLogger<RedisDistributedLocker>)
		{
			// IGNORE NULL LOGGER (FOR BETTER PERF)
			_logger = null;
		}
		else
		{
			_logger = logger;
		}

		_connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

		EnsureConnection(CancellationToken.None);
	}

	private ConfigurationOptions GetConfigurationOptions()
	{
		if (_options.ConfigurationOptions is not null)
			return _options.ConfigurationOptions;

		if (string.IsNullOrWhiteSpace(_options.Configuration) == false)
			return ConfigurationOptions.Parse(_options.Configuration!);

		throw new InvalidOperationException("Unable to connect to Redis: no Configuration nor ConfigurationOptions have been specified");
	}

	// TODO: MOVE THIS IN THE ACCESSOR
	//private string GetLockKey(string key)
	//{
	//	return $"{key}__lock";
	//}

	private async ValueTask EnsureConnectionAsync(CancellationToken token)
	{
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
				_connection = await ConnectionMultiplexer.ConnectAsync(GetConfigurationOptions());
			}

			if (_connection is not null)
			{
				_provider = new RedisDistributedSynchronizationProvider(_connection.GetDatabase(), options =>
				{
					options.Expiry(_options.AbandonTimeout);
				});
			}
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, exc, "FUSION: [DL] An error occurred while connecting to Redis {Config}", _options.ConfigurationOptions?.ToString() ?? _options.Configuration);
		}
		finally
		{
			_connectionLock.Release();
		}

		if (_connection is null)
		{
			_ = Task.Run(async () =>
			{
				await EnsureConnectionAsync(CancellationToken.None).ConfigureAwait(false);
			});
		}
	}

	private void EnsureConnection(CancellationToken token)
	{
		if (_connection is not null)
			return;

		_ = Task.Run(async () =>
		{
			await EnsureConnectionAsync(CancellationToken.None).ConfigureAwait(false);
		});
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_connection is not null)
		{
			_connection.Dispose();
			_connection = null;
		}
	}
}
