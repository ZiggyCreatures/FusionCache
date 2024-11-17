using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace FusionCacheTests.Stuff;

public abstract class AbstractTests
{
	protected AbstractTests(ITestOutputHelper output, string? testingCacheKeyPrefix)
	{
		TestOutput = output;
		TestingCacheKeyPrefix = testingCacheKeyPrefix;
	}

	protected ITestOutputHelper TestOutput { get; }

	protected string? TestingCacheKeyPrefix { get; }

	protected XUnitLogger<T> CreateXUnitLogger<T>(LogLevel minLevel = LogLevel.Trace)
	{
		return new XUnitLogger<T>(minLevel, TestOutput);
	}

	protected static ListLogger<T> CreateListLogger<T>(LogLevel minLogLevel)
	{
		return new ListLogger<T>(minLogLevel);
	}
}
