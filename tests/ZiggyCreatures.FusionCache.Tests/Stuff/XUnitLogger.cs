using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace FusionCacheTests.Stuff;

public class XUnitLogger<T>
	: ILogger<T>
{
	internal class Scope : IDisposable
	{
		public void Dispose()
		{
			// EMPTY
		}
	}

	private readonly ITestOutputHelper _helper;
	private readonly LogLevel _minLogLevel;

	public XUnitLogger(LogLevel minLogLevel, ITestOutputHelper helper)
	{
		_minLogLevel = minLogLevel;
		_helper = helper;
	}

	public IDisposable BeginScope<TState>(TState state)
		where TState : notnull
	{
		return new Scope();
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return logLevel >= _minLogLevel;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (IsEnabled(logLevel))
		{
			_helper.WriteLine(
				(logLevel >= LogLevel.Warning ? Environment.NewLine : "")
				+ $"{logLevel.ToString()[..4].ToUpper()} {DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture)}: "
				+ formatter(state, exception)
				+ (exception is null
					? ""
					: (Environment.NewLine + exception.ToString() + Environment.NewLine)
				)
				+ (logLevel >= LogLevel.Warning ? Environment.NewLine : "")
			);
		}
	}
}
