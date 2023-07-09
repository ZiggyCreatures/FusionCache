using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace FusionCacheTests.Stuff
{
	internal class XUnitLogger<T>
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
				_helper.WriteLine($"{DateTime.UtcNow}: " + formatter(state, exception));
		}
	}
}
