using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace FusionCacheTests
{
	internal class ListLogger<T>
		: ILogger<T>
	{

		internal class Scope : IDisposable
		{
			public void Dispose()
			{
				// EMPTY
			}
		}

		private readonly LogLevel _minLogLevel;
		public readonly List<(LogLevel LogLevel, string message)> Items = new List<(LogLevel LogLevel, string message)>();

		public ListLogger(LogLevel minLogLevel)
		{
			this._minLogLevel = minLogLevel;
		}

		public IDisposable BeginScope<TState>(TState state)
		{
			return new Scope();
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= _minLogLevel;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Items.Add((logLevel, formatter(state, exception)));
		}
	}
}
