using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Backplane.Memory;

/// <summary>
/// An in-memory implementation of a FusionCache backplane
/// </summary>
public partial class MemoryBackplane
	: IFusionCacheBackplane
{
	private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<MemoryBackplane>>> _connections = new();

	private readonly MemoryBackplaneOptions _options;
	private BackplaneSubscriptionOptions? _subscriptionOptions;
	private readonly ILogger? _logger;

	private ConcurrentDictionary<string, List<MemoryBackplane>>? _connection;

	private string? _channelName = null;
	private List<MemoryBackplane>? _subscribers;

	private Action<BackplaneConnectionInfo>? _connectHandler;
	private Action<BackplaneMessage>? _incomingMessageHandler;
	private Func<BackplaneConnectionInfo, ValueTask>? _connectHandlerAsync;
	private Func<BackplaneMessage, ValueTask>? _incomingMessageHandlerAsync;

	/// <summary>
	/// Initializes a new instance of the MemoryBackplane class.
	/// </summary>
	/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public MemoryBackplane(IOptions<MemoryBackplaneOptions> optionsAccessor, ILogger<MemoryBackplane>? logger = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));


		// LOGGING
		if (logger is NullLogger<MemoryBackplane>)
		{
			// IGNORE NULL LOGGER (FOR BETTER PERF)
			_logger = null;
		}
		else
		{
			_logger = logger;
		}

		// CONNECTION
		if (_options.ConnectionId is null)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, "FUSION: [BP] A MemoryBackplane should be used with an explicit ConnectionId option, otherwise concurrency issues will probably happen");

			_options.ConnectionId = "_default";
		}
	}

	private void EnsureConnection()
	{
		if (_options.ConnectionId is null)
			throw new NullReferenceException("The ConnectionId is null");

		if (_connection is null)
		{
			_connection = _connections.GetOrAdd(_options.ConnectionId, _ => new ConcurrentDictionary<string, List<MemoryBackplane>>());
			_connectHandler?.Invoke(new BackplaneConnectionInfo(false));
		}

		EnsureSubscribers();
	}

	private void EnsureSubscribers()
	{
		if (_connection is null)
			throw new InvalidOperationException("No connection available");

		if (_subscribers is null)
		{
			lock (_connection)
			{
				if (_channelName is null)
					throw new NullReferenceException("The backplane channel name is null");

				_subscribers = _connection.GetOrAdd(_channelName, _ => []);
			}
		}
	}

	private void Disconnect()
	{
		_connectHandler = null;
		_connectHandlerAsync = null;

		if (_connection is null)
			return;

		_connection = null;
	}
}
